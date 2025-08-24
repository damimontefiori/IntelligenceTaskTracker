namespace IntelligenceTaskTracker.Web.Services.AI;

public interface IAiProvider
{
    Task<string?> GenerateJsonAsync(string systemPrompt, string userPrompt, TimeSpan timeout, CancellationToken ct);
}

public class AiOptions
{
    public string Provider { get; set; } = "OpenAI"; // OpenAI | Gemini | AzureOpenAI
    public OpenAIOptions OpenAI { get; set; } = new();
    public GeminiOptions Gemini { get; set; } = new();
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();
    public LimitsOptions Limits { get; set; } = new();
}

public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o";
}

public class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-flash";
}

public class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class LimitsOptions
{
    public int MaxCommentsPerTask { get; set; } = 20;
    public int MaxTasksPerUser { get; set; } = 20;
    public int CacheTtlHours { get; set; } = 24; // Aumentar cache para evitar rate limiting
    public int TimeoutSeconds { get; set; } = 15; // Aumentar timeout
}
