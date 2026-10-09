using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.IntegrationTests.Fakes;
using SUPPORT.IntegrationTests.Infrastructure;

namespace SUPPORT.IntegrationTests.Api;

/// <summary>Boots the real API against the test database with Gemini replaced by fakes.</summary>
/// <param name="fixture">Database fixture.</param>
/// <param name="knowledgeRoot">Knowledge folder for the <c>dashboard</c> product.</param>
public sealed class SupportApiFactory(PostgresFixture fixture, string knowledgeRoot) : WebApplicationFactory<Program>
{
    /// <summary>Token the fake DASHBOARD sends.</summary>
    public const string DashboardToken = "test-dashboard-token-0123456789abcdef";

    /// <summary>The fake assistant, so tests can script its reply.</summary>
    public FakeAssistant Assistant { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, not ConfigureAppConfiguration: Program reads these while building the service collection,
        // before app-configuration callbacks would apply.
        builder.UseSetting("ConnectionStrings:Support", fixture.ConnectionString);
        builder.UseSetting("InternalClients:0:Product", "dashboard");
        builder.UseSetting("InternalClients:0:Token", DashboardToken);
        builder.UseSetting("Llm:ApiKey", "fake-key");
        builder.UseSetting("Knowledge:Sources:dashboard", knowledgeRoot);
        builder.UseSetting("Knowledge:IngestOnStartup", "false");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        // The threshold is a calibration of the embedding model. The bag-of-words fake scores on-topic questions
        // around 0.3–0.5 (Gemini: ≈ 0.8), so it needs its own value; off-topic still scores ≈ 0.
        builder.UseSetting("Chat:MinCosineSimilarity", "0.3");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(new BagOfWordsEmbeddingGenerator());
            services.RemoveAll<ISupportAssistant>();
            services.AddSingleton<ISupportAssistant>(Assistant);
        });
    }

    /// <summary>Scripted assistant: replies with fixed parts and records what it was asked.</summary>
    public sealed class FakeAssistant : ISupportAssistant
    {
        /// <summary>Parts streamed for every answer.</summary>
        public string[] Reply { get; set; } = ["Bấm ", "**Close Sprint**."];

        /// <summary>Last request received.</summary>
        public AssistantRequest? LastRequest { get; private set; }

        /// <inheritdoc />
        public bool IsConfigured => true;

        /// <inheritdoc />
        public string ChatModel => "fake-model";

        /// <inheritdoc />
        public async IAsyncEnumerable<AssistantUpdate> StreamAnswerAsync(AssistantRequest request, [EnumeratorCancellation] CancellationToken ct)
        {
            LastRequest = request;
            foreach (var part in Reply)
            {
                await Task.Yield();
                yield return new AssistantUpdate(part);
            }
        }
    }
}
