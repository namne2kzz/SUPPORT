using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Infrastructure.AI;
using SUPPORT.Infrastructure.Knowledge;
using SUPPORT.Infrastructure.Persistence;
using SUPPORT.Infrastructure.Services;
using SUPPORT.Infrastructure.Settings;

namespace SUPPORT.Infrastructure;

/// <summary>Registers infrastructure services (persistence, AI clients, knowledge ingestion).</summary>
public static class DependencyInjection
{
    // The OpenAI SDK refuses an empty key. A placeholder lets the API boot without one: IsConfigured stays false
    // and every entry point checks it before calling the provider.
    private const string MissingKeyPlaceholder = "not-configured";

    /// <summary>Adds infrastructure services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSupportInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Support");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Support is not configured (use user-secrets or .env).");

        services.AddOptions<LlmSettings>()
            .Bind(configuration.GetSection(LlmSettings.SectionName))
            .Validate(s => s.EmbeddingDimensions == SupportDbContext.EmbeddingDimensions,
                $"Llm:EmbeddingDimensions must equal the schema's vector({SupportDbContext.EmbeddingDimensions}); changing it needs a migration.")
            .ValidateOnStart();
        services.AddOptions<KnowledgeSettings>().Bind(configuration.GetSection(KnowledgeSettings.SectionName));

        // Factory, not a plain AddDbContext: the background services are singletons and create their own
        // short-lived contexts. AddDbContextFactory also registers a scoped context for handlers.
        services.AddDbContextFactory<SupportDbContext>(o => SupportDbContextOptions.Configure(o, connectionString));

        AddLlmClients(services, configuration);

        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IKnowledgeSearch, HybridKnowledgeSearch>();
        services.AddScoped<IKnowledgeCatalog, KnowledgeCatalog>();
        services.AddScoped<ISupportAssistant, NMateAssistant>();
        services.AddSingleton<IStreamGate, InMemoryStreamGate>();
        services.AddSingleton<EmbeddingService>();

        services.AddSingleton<KnowledgeIngestionService>();
        services.AddSingleton<IKnowledgeIngestion>(sp => sp.GetRequiredService<KnowledgeIngestionService>());
        services.AddHostedService(sp => sp.GetRequiredService<KnowledgeIngestionService>());
        services.AddHostedService<ConversationRetentionService>();

        services.AddHealthChecks()
            .AddDbContextCheck<SupportDbContext>("postgres", tags: ["ready"]);

        return services;
    }

    /// <summary>Applies pending EF Core migrations — a dev/self-host convenience, mirroring HUB.</summary>
    /// <param name="services">Root service provider.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the schema is current.</returns>
    public static async Task MigrateSupportDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var factory = services.GetRequiredService<IDbContextFactory<SupportDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }

    private static void AddLlmClients(IServiceCollection services, IConfiguration configuration)
    {
        var llm = configuration.GetSection(LlmSettings.SectionName).Get<LlmSettings>() ?? new LlmSettings();
        var key = new ApiKeyCredential(llm.IsConfigured ? llm.ApiKey : MissingKeyPlaceholder);

        // Chat: no automatic retries. The user is waiting, and on a free tier a 429 will not clear in seconds —
        // fail fast and let the widget say "try again later".
        var chatOpenAi = new OpenAIClient(key, new OpenAIClientOptions
        {
            Endpoint = new Uri(llm.Endpoint),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        });
        services.AddChatClient(chatOpenAi.GetChatClient(llm.ChatModel).AsIChatClient())
            .UseOpenTelemetry();

        // Embeddings: the SDK's default retry policy (exponential backoff, honours Retry-After) suits ingestion.
        var embeddingOpenAi = new OpenAIClient(key, new OpenAIClientOptions { Endpoint = new Uri(llm.Endpoint) });
        services.AddDistributedMemoryCache();
        services.AddEmbeddingGenerator(sp =>
                new DistributedCachingEmbeddingGenerator<string, Embedding<float>>(
                    embeddingOpenAi.GetEmbeddingClient(llm.EmbeddingModel).AsIEmbeddingGenerator(llm.EmbeddingDimensions),
                    sp.GetRequiredService<IDistributedCache>()))
            .UseOpenTelemetry();
    }
}
