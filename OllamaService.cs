using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace whisperMeOff;

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private string _baseUrl = "http://localhost:1337";
    private string _model = "";
    private bool _useOpenAIFormat = true; // Use OpenAI-compatible API by default

    public OllamaService()
    {
        _httpClient = new HttpClient();
    }

    public void Configure(string baseUrl, string model)
    {
        // Detect if using OpenAI-compatible endpoint (has /v1 in path)
        _useOpenAIFormat = baseUrl.Contains("/v1");
        
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
    }

    public async Task<string> SendMessageAsync(string userMessage, List<(string sender, string message)> conversationHistory)
    {
        try
        {
            string responseText;

            if (_useOpenAIFormat)
            {
                responseText = await SendOpenAICompatibleMessageAsync(userMessage, conversationHistory);
            }
            else
            {
                responseText = await SendOllamaNativeMessageAsync(userMessage, conversationHistory);
            }
            
            return responseText;
        }
        catch (HttpRequestException ex)
        {
            return $"Error: Could not connect to AI server at {_baseUrl}. Is it running?\n\nDetails: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> SendOpenAICompatibleMessageAsync(string userMessage, List<(string sender, string message)> conversationHistory)
    {
        var messages = new List<object>();
        
        // Add conversation history
        foreach (var (sender, message) in conversationHistory)
        {
            messages.Add(new
            {
                role = sender == "You" ? "user" : "assistant",
                content = message
            });
        }
        
        // Add current message
        messages.Add(new
        {
            role = "user",
            content = userMessage
        });

        var requestBody = new
        {
            model = _model,
            messages = messages,
            stream = false
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        
        // Try to parse as OpenAI format
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString() ?? "No response from AI";
                }
            }
        }
        catch
        {
            // Fallback: return raw response
        }
        
        return responseJson;
    }

    private async Task<string> SendOllamaNativeMessageAsync(string userMessage, List<(string sender, string message)> conversationHistory)
    {
        var messages = new List<object>();
        
        foreach (var (sender, message) in conversationHistory)
        {
            messages.Add(new
            {
                role = sender == "You" ? "user" : "assistant",
                content = message
            });
        }
        
        messages.Add(new
        {
            role = "user",
            content = userMessage
        });

        var requestBody = new
        {
            model = _model,
            messages = messages,
            stream = false
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        var responseText = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
        
        return responseText ?? "No response from AI";
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            string url;
            if (_useOpenAIFormat)
            {
                url = $"{_baseUrl}/models";
            }
            else
            {
                url = $"{_baseUrl}/api/tags";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(responseJson);
            var models = new List<string>();
            
            if (_useOpenAIFormat)
            {
                // OpenAI-compatible format
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    foreach (var model in data.EnumerateArray())
                    {
                        if (model.TryGetProperty("id", out var id))
                        {
                            models.Add(id.GetString() ?? "");
                        }
                    }
                }
            }
            else
            {
                // Ollama native format
                foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
                {
                    models.Add(model.GetProperty("name").GetString() ?? "");
                }
            }
            
            return models;
        }
        catch
        {
            return new List<string>();
        }
    }
}
