using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace IntelligenceTaskTracker.Web.Services.AI;

public class GeminiProvider(HttpClient http, Microsoft.Extensions.Options.IOptions<AiOptions> options) : IAiProvider
{
    private readonly HttpClient _http = http;
    private readonly AiOptions _opts = options.Value;

    public async Task<string?> GenerateJsonAsync(string systemPrompt, string userPrompt, TimeSpan timeout, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.Gemini.ApiKey)) return null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_opts.Gemini.Model}:generateContent?key={_opts.Gemini.ApiKey}";

        var content = new
        {
            contents = new[]
            {
                new { role = "model", parts = new[] { new { text = systemPrompt } } },
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new { temperature = 0.2 }
        };
        var json = JsonSerializer.Serialize(content);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        
        try
        {
            var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) 
            {
                // Si es rate limiting (429), lanzar excepción específica
                if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new InvalidOperationException("API rate limit exceeded - too many requests");
                }
                return null;
            }
            
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            
            // Extraer el texto de la respuesta de Gemini
            using var doc = JsonDocument.Parse(body);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                var responseContent = firstCandidate.GetProperty("content");
                var parts = responseContent.GetProperty("parts");
                if (parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0)
                {
                    var text = parts[0].GetProperty("text").GetString();
                    return text;
                }
            }
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("API timeout - request took too long");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Network error: {ex.Message}");
        }
        
        return null;
    }
}
