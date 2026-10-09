using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SUPPORT.IntegrationTests.Fakes;
using SUPPORT.IntegrationTests.Infrastructure;

namespace SUPPORT.IntegrationTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class InternalApiTests : IAsyncLifetime
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly KnowledgeFolder _folder = new();
    private readonly SupportApiFactory _factory;

    public InternalApiTests(PostgresFixture fixture) => _factory = new SupportApiFactory(fixture, _folder.Root);

    public async Task InitializeAsync()
    {
        var reindex = await Client(_userId).PostAsync("/internal/v1/knowledge/reindex", null);
        reindex.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Internal_WithoutValidToken_Is401()
    {
        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/internal/v1/status")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        anonymous.DefaultRequestHeaders.Add("X-Internal-Token", "wrong-token");
        (await anonymous.GetAsync("/internal/v1/status")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_NeedsNoToken()
    {
        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await anonymous.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Chat_WithoutUserHeaders_Is400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Token", SupportApiFactory.DashboardToken);

        var response = await client.PostAsJsonAsync("/internal/v1/chat", new { message = "Hỏi" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chat_EmptyMessage_Is400BeforeStreaming()
    {
        var response = await Client(_userId).PostAsJsonAsync("/internal/v1/chat", new { message = "  " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task Chat_StreamsSse_ThenHistoryAndFeedbackWork()
    {
        var client = Client(_userId);
        var response = await client.PostAsJsonAsync("/internal/v1/chat", new
        {
            message = "Đóng sprint thì task chưa xong đi đâu?",
            context = new { route = "/acme/DASH/sprint-planning" },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");

        var events = ParseSse(await response.Content.ReadAsStringAsync());
        events.Select(e => e.Name).ShouldBe(["meta", "delta", "delta", "citations", "done"]);
        events[3].Data.EnumerateArray().First().GetProperty("headingPath").GetString().ShouldBe("Đóng sprint › Task chưa xong");
        _factory.Assistant.LastRequest!.Knowledge.ShouldNotBeEmpty();

        var conversationId = events[0].Data.GetProperty("conversationId").GetGuid();
        var answerId = events[^1].Data.GetProperty("messageId").GetGuid();

        var messages = await client.GetFromJsonAsync<JsonElement>($"/internal/v1/conversations/{conversationId}/messages");
        messages.GetArrayLength().ShouldBe(2);
        messages[1].GetProperty("content").GetString().ShouldBe("Bấm **Close Sprint**.");

        (await client.PutAsJsonAsync($"/internal/v1/messages/{answerId}/feedback", new { rating = 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        messages = await client.GetFromJsonAsync<JsonElement>($"/internal/v1/conversations/{conversationId}/messages");
        messages[1].GetProperty("rating").GetInt16().ShouldBe((short)1);

        var list = await client.GetFromJsonAsync<JsonElement>("/internal/v1/conversations");
        list.GetProperty("items").EnumerateArray().ShouldContain(c => c.GetProperty("id").GetGuid() == conversationId);

        // Another user of the same product cannot read, rate or delete it.
        var intruder = Client(Guid.NewGuid());
        (await intruder.GetAsync($"/internal/v1/conversations/{conversationId}/messages")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.PutAsJsonAsync($"/internal/v1/messages/{answerId}/feedback", new { rating = -1 })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.DeleteAsync($"/internal/v1/conversations/{conversationId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await client.DeleteAsync($"/internal/v1/conversations/{conversationId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/internal/v1/conversations/{conversationId}/messages")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Chat_OffTopicQuestion_IsDeclinedWithoutTheModel()
    {
        _factory.Assistant.Reply = ["không được gọi"];

        var response = await Client(_userId).PostAsJsonAsync("/internal/v1/chat", new { message = "zzz qqq xyzzy" });
        var events = ParseSse(await response.Content.ReadAsStringAsync());

        events.Select(e => e.Name).ShouldBe(["meta", "delta", "done"]);
        events[^1].Data.GetProperty("answered").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task StatusAndSuggestions_ReflectIndexedKnowledge()
    {
        var client = Client(_userId);

        var status = await client.GetFromJsonAsync<JsonElement>("/internal/v1/status");
        status.GetProperty("available").GetBoolean().ShouldBeTrue();
        status.GetProperty("activeDocuments").GetInt32().ShouldBe(3);

        var suggestions = await client.GetFromJsonAsync<string[]>("/internal/v1/suggestions?route=/acme/DASH/backlog");
        suggestions.ShouldBe(["Promote to Sprint là gì?"]);
    }

    [Fact]
    public async Task PurgeUserData_RemovesOnlyThatUsersConversations()
    {
        var other = Client(Guid.NewGuid());
        await (await other.PostAsJsonAsync("/internal/v1/chat", new { message = "Làm sao đóng sprint?" })).Content.ReadAsStringAsync();
        var mine = Client(_userId);
        await (await mine.PostAsJsonAsync("/internal/v1/chat", new { message = "Làm sao tạo sprint?" })).Content.ReadAsStringAsync();

        var purge = await mine.DeleteFromJsonAsync<JsonElement>($"/internal/v1/users/{_userId}/data");
        purge.GetProperty("deletedConversations").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        (await mine.GetFromJsonAsync<JsonElement>("/internal/v1/conversations")).GetProperty("total").GetInt32().ShouldBe(0);
        (await other.GetFromJsonAsync<JsonElement>("/internal/v1/conversations")).GetProperty("total").GetInt32().ShouldBe(1);
    }

    private HttpClient Client(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Token", SupportApiFactory.DashboardToken);
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Org-Id", OrgId.ToString());
        return client;
    }

    private static List<(string Name, JsonElement Data)> ParseSse(string body) =>
        [.. body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(block =>
        {
            var lines = block.Split('\n');
            var name = lines.Single(l => l.StartsWith("event: ", StringComparison.Ordinal))["event: ".Length..];
            var data = lines.Single(l => l.StartsWith("data: ", StringComparison.Ordinal))["data: ".Length..];
            return (name, JsonDocument.Parse(data).RootElement.Clone());
        })];

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        _folder.Dispose();
    }
}
