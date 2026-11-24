using Microsoft.Extensions.Configuration;

namespace Sedgewright.Tests.Configurations;

public static class Configs
{
    private static IConfiguration instance = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddUserSecrets(typeof(Configs).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();

    public static IConfiguration Instance { get => instance; }

    public static string OpenAIKey => Instance["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key is not configured.");

    public static string OpenRouterKey => Instance["OpenRouter:ApiKey"] ?? throw new InvalidOperationException("OpenRouter API key is not configured.");
}
