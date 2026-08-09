using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WareConnect.Api.AI.Configuration;
using WareConnect.Api.AI.Context;
using WareConnect.Api.AI.Memory;
using WareConnect.Api.AI.Prompts;
using WareConnect.Api.AI.Services;
using WareConnect.Api.AI.Tools;

namespace WareConnect.Api.AI;

/// <summary>Extension method that registers all copilot services with the DI container.</summary>
public static class CopilotServiceRegistration
{
    public static IServiceCollection AddWareConnectCopilot(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.AddOptions<CopilotOptions>()
            .BindConfiguration(CopilotOptions.SectionName)
            .ValidateDataAnnotations();

        // Named HttpClient used by ToolDispatcher to call internal APIs
        services.AddHttpClient("CopilotInternal", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<CopilotOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseApiUrl);
            client.Timeout     = TimeSpan.FromSeconds(opts.OpenAI.TimeoutSeconds);
        });

        // Context builder
        services.AddScoped<IContextBuilder, ContextBuilder>();

        // Conversation memory – SQL-backed when EnableSqlPersistence=true, otherwise in-memory
        var enableSql = configuration.GetValue<bool>("Copilot:Memory:EnableSqlPersistence");
        if (enableSql)
        {
            services.AddScoped<IConversationRepository, SqlConversationRepository>();
            services.AddScoped<IConversationMemory, SqlConversationMemory>();
        }
        else
        {
            services.AddSingleton<IConversationMemory, InMemoryConversationMemory>();
        }

        // Core AI services
        services.AddSingleton<IPromptBuilder,      PromptBuilder>();
        services.AddScoped<IToolDispatcher,        ToolDispatcher>();
        services.AddScoped<ICopilotResponseService, CopilotResponseService>();
        services.AddScoped<ICopilotOrchestrator,   CopilotOrchestrator>();

        return services;
    }
}
