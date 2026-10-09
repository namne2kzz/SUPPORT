using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using NSubstitute;
using SUPPORT.Application.Chat.Commands.AskQuestion;
using SUPPORT.Application.Chat.DTOs;
using SUPPORT.Application.Common.Exceptions;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Application.Common.Settings;
using SUPPORT.Domain.Entities;
using SUPPORT.Domain.Enums;

namespace SUPPORT.UnitTests.Chat;

public sealed class AskQuestionHandlerTests
{
    private const string Product = "dashboard";
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly ChatSettings _settings = new() { MinCosineSimilarity = 0.45f };
    private readonly FakeConversationRepository _conversations = new();
    private readonly IKnowledgeSearch _search = Substitute.For<IKnowledgeSearch>();
    private readonly IKnowledgeCatalog _catalog = Substitute.For<IKnowledgeCatalog>();
    private readonly FakeAssistant _assistant = new();
    private readonly IStreamGate _gate = Substitute.For<IStreamGate>();
    private readonly ICallerContext _caller = Substitute.For<ICallerContext>();

    public AskQuestionHandlerTests()
    {
        _caller.Product.Returns(Product);
        _caller.OrgId.Returns(OrgId);
        _caller.UserId.Returns(UserId);
        _gate.TryAcquire(Arg.Any<string>()).Returns(Substitute.For<IDisposable>());
    }

    [Fact]
    public async Task NoRelevantKnowledge_DeclinesWithoutCallingTheModel()
    {
        Hits(Hit(0.20f, fullText: false));

        var events = await RunAsync(new AskQuestionCommand(null, "Thời tiết hôm nay?", "/sprints"));

        _assistant.Calls.ShouldBe(0);
        events.Select(e => e.Name).ShouldBe(["meta", "delta", "done"]);
        Payload(events[1], "text").ShouldBe(_settings.NoAnswerText);
        Payload(events[2], "answered").ShouldBe(false);

        var answer = _conversations.Messages.Single(m => m.Role == MessageRole.Assistant);
        answer.Model.ShouldBeNull();
        answer.TopScore.ShouldBe(0.20f);
    }

    [Fact]
    public async Task FullTextHit_CountsAsGroundedEvenWithLowSimilarity()
    {
        Hits(Hit(0.10f, fullText: true));
        _assistant.Reply("Bấm **Promote to Sprint**.");

        var events = await RunAsync(new AskQuestionCommand(null, "Promote to Sprint là gì", null));

        _assistant.Calls.ShouldBe(1);
        Payload(events[^1], "answered").ShouldBe(true);
    }

    [Fact]
    public async Task GroundedQuestion_StreamsAnswerAndSavesIt()
    {
        Hits(Hit(0.80f, fullText: false));
        _assistant.Reply("Vào ", "Sprint Planning ", "rồi bấm **Close**.");

        var events = await RunAsync(new AskQuestionCommand(null, "Làm sao đóng sprint?", "/sprints"));

        events.Select(e => e.Name).ShouldBe(["meta", "delta", "delta", "delta", "citations", "done"]);

        var conversation = _conversations.Conversations.Single();
        conversation.Title.ShouldBe("Làm sao đóng sprint?");
        Payload(events[0], "conversationId").ShouldBe(conversation.Id);

        var answer = _conversations.Messages.Single(m => m.Role == MessageRole.Assistant);
        answer.Content.ShouldBe("Vào Sprint Planning rồi bấm **Close**.");
        answer.Citations.Count.ShouldBe(1);
        answer.IsInterrupted.ShouldBeFalse();
        answer.Model.ShouldBe(_assistant.ChatModel);
        Payload(events[^1], "messageId").ShouldBe(answer.Id);
    }

    [Fact]
    public async Task ExistingConversation_PassesHistoryToTheModel()
    {
        var conversation = Conversation.Start(Product, OrgId, UserId, "Câu đầu");
        _conversations.Conversations.Add(conversation);
        _conversations.Messages.Add(ConversationMessage.UserQuestion(conversation.Id, "Câu đầu", null));
        Hits(Hit(0.80f, fullText: false));
        _assistant.Reply("Ok.");

        await RunAsync(new AskQuestionCommand(conversation.Id, "Câu thứ hai", null));

        _assistant.LastRequest!.History.Select(t => t.Content).ShouldBe(["Câu đầu"]);
        _assistant.LastRequest.Question.ShouldBe("Câu thứ hai");
    }

    [Fact]
    public async Task SomeoneElsesConversation_IsNotFound()
    {
        var foreign = Conversation.Start(Product, OrgId, Guid.NewGuid(), "Của người khác");
        _conversations.Conversations.Add(foreign);

        await Should.ThrowAsync<NotFoundException>(() => RunAsync(new AskQuestionCommand(foreign.Id, "Xem trộm", null)));
        _conversations.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task SecondConcurrentStream_IsRejected()
    {
        _gate.TryAcquire(Arg.Any<string>()).Returns((IDisposable?)null);

        var ex = await Should.ThrowAsync<ConflictException>(() => RunAsync(new AskQuestionCommand(null, "Hỏi", null)));
        ex.Code.ShouldBe(AssistantErrorCodes.StreamInProgress);
    }

    [Fact]
    public async Task ModelNotConfigured_IsUnavailable()
    {
        _assistant.IsConfigured = false;

        await Should.ThrowAsync<AssistantUnavailableException>(() => RunAsync(new AskQuestionCommand(null, "Hỏi", null)));
    }

    [Fact]
    public async Task ProviderFailsMidStream_KeepsPartialAnswerAndEmitsError()
    {
        Hits(Hit(0.80f, fullText: false));
        _assistant.Reply("Phần đầu ");
        _assistant.FailAfterReply = new AssistantUnavailableException(AssistantErrorCodes.QuotaExceeded, "Hết quota");

        var events = await RunAsync(new AskQuestionCommand(null, "Hỏi", null));

        events.Select(e => e.Name).ShouldBe(["meta", "delta", "error"]);
        Payload(events[^1], "code").ShouldBe(AssistantErrorCodes.QuotaExceeded);

        var answer = _conversations.Messages.Single(m => m.Role == MessageRole.Assistant);
        answer.Content.ShouldBe("Phần đầu ");
        answer.IsInterrupted.ShouldBeTrue();
    }

    private async Task<List<ChatStreamEvent>> RunAsync(AskQuestionCommand command)
    {
        var handler = new AskQuestionHandler(_caller, _conversations, _search, _catalog, _assistant, _gate, Options.Create(_settings));
        var events = new List<ChatStreamEvent>();
        await foreach (var e in handler.Handle(command, CancellationToken.None)) events.Add(e);
        return events;
    }

    private void Hits(params KnowledgeHit[] hits) =>
        _search.SearchAsync(Arg.Any<KnowledgeQuery>(), Arg.Any<CancellationToken>()).Returns(hits);

    private static KnowledgeHit Hit(float cosine, bool fullText) =>
        new(Guid.NewGuid(), "Quản lý Sprint", "Đóng sprint", "Bấm Close.", "/sprints", cosine, fullText, 0.03);

    private static object? Payload(ChatStreamEvent e, string property) =>
        e.Payload.GetType().GetProperty(property)!.GetValue(e.Payload);

    private sealed class FakeAssistant : ISupportAssistant
    {
        private string[] _reply = [];

        public bool IsConfigured { get; set; } = true;
        public string ChatModel => "fake-model";
        public int Calls { get; private set; }
        public AssistantRequest? LastRequest { get; private set; }
        public AssistantUnavailableException? FailAfterReply { get; set; }

        public void Reply(params string[] parts) => _reply = parts;

        public async IAsyncEnumerable<AssistantUpdate> StreamAnswerAsync(AssistantRequest request, [EnumeratorCancellation] CancellationToken ct)
        {
            Calls++;
            LastRequest = request;
            foreach (var part in _reply)
            {
                await Task.Yield();
                yield return new AssistantUpdate(part);
            }

            if (FailAfterReply is not null) throw FailAfterReply;
        }
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public List<Conversation> Conversations { get; } = [];
        public List<ConversationMessage> Messages { get; } = [];

        public Task<Conversation?> GetAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Conversations.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<ConversationMessage>> GetRecentMessagesAsync(Guid conversationId, int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ConversationMessage>>([.. Messages.Where(m => m.ConversationId == conversationId).TakeLast(take)]);

        public Task<IReadOnlyList<(ConversationMessage Message, short? Rating)>> GetMessagesWithRatingsAsync(Guid conversationId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<Conversation> Items, int Total)> ListAsync(string product, Guid orgId, Guid userId, int page, int pageSize, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<(ConversationMessage Message, Conversation Conversation)?> GetMessageAsync(Guid messageId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<MessageFeedback?> GetFeedbackAsync(Guid messageId, CancellationToken ct) => throw new NotSupportedException();

        public void Add(Conversation conversation) => Conversations.Add(conversation);
        public void Add(ConversationMessage message) => Messages.Add(message);
        public void Add(MessageFeedback feedback) => throw new NotSupportedException();

        public Task<int> PurgeUserAsync(string product, Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
