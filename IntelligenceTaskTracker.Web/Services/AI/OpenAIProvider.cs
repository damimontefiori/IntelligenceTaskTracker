using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace IntelligenceTaskTracker.Web.Services.AI;

public class OpenAIProvider(HttpClient http, Microsoft.Extensions.Options.IOptions<AiOptions> options) : IAiProvider
{
    private readonly HttpClient _http = http;
    private readonly AiOptions _opts = options.Value;

    public async Task<string?> GenerateJsonAsync(string systemPrompt, string userPrompt, TimeSpan timeout, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.OpenAI.ApiKey)) return null;
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var url = "https://api.openai.com/v1/chat/completions";

        var requestContent = new
        {
            model = _opts.OpenAI.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            max_tokens = 2000,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestContent);
        
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.OpenAI.ApiKey);
        
        try
        {
            var resp = await _http.SendAsync(req, cts.Token);
            
            if (!resp.IsSuccessStatusCode) 
            {
                // Si es rate limiting (429), lanzar excepción específica
                if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new InvalidOperationException("OpenAI API rate limit exceeded - too many requests");
                }
                
                // Para otros errores, leer el contenido si es posible para debugging
                var errorContent = await resp.Content.ReadAsStringAsync(cts.Token);
                throw new InvalidOperationException($"OpenAI API error {resp.StatusCode}: {errorContent}");
            }
            
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            
            // Extraer el contenido de la respuesta de OpenAI
            using var doc = JsonDocument.Parse(body);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                var message = firstChoice.GetProperty("message");
                var content = message.GetProperty("content").GetString();
                return content;
            }
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("OpenAI API timeout - request took too long");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Network error: {ex.Message}");
        }
        
        return null;
    }
}
