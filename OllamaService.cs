using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace whisperMeOff;

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private string _baseUrl = "http://localhost:1337";
    private string _model = "";
    private string _apiKey = "";
    private string _token = "";
    private string _agentId = "";
    private string _profileName = "";
    private bool _useOpenAIFormat = true; // Use OpenAI-compatible API by default
    private bool _useWebSocket = false; // Use WebSocket for OpenClaw

    public OllamaService()
    {
        _httpClient = new HttpClient();
    }

    public void Configure(string baseUrl, string model, string apiKey = "", string token = "", string agentId = "", string profileName = "")
    {
        _profileName = profileName ?? "";
        
        // Detect if using OpenClaw (WebSocket mode)
        _useWebSocket = _profileName.Equals("OpenClaw", StringComparison.OrdinalIgnoreCase);
        
        if (_useWebSocket)
        {
            // For OpenClaw, baseUrl is the WebSocket URL (e.g., ws://127.0.0.1:18789)
            // but we need HTTP URL for REST API
            _baseUrl = baseUrl.TrimEnd('/');
            
            // Add http:// if no protocol specified
            if (!_baseUrl.StartsWith("http://") && !_baseUrl.StartsWith("https://") && 
                !_baseUrl.StartsWith("ws://") && !_baseUrl.StartsWith("wss://"))
            {
                _baseUrl = "http://" + _baseUrl;
            }
        }
        else
        {
            // Detect if using OpenAI-compatible endpoint
            // Check for /v1 in path OR if using known OpenAI-compatible ports (LM Studio: 1234, Jan: 1337)
            _useOpenAIFormat = baseUrl.Contains("/v1") || baseUrl.Contains(":1234") || baseUrl.Contains(":1337");
            
            _baseUrl = baseUrl.TrimEnd('/');
            
            // For OpenAI-compatible servers on known ports, ensure /v1 prefix is present
            if (_useOpenAIFormat && !_baseUrl.Contains("/v1"))
            {
                _baseUrl = _baseUrl + "/v1";
            }
        }
        
        _model = model;
        _apiKey = apiKey;
        _token = token;
        _agentId = agentId;
    }

    public async Task<string> SendMessageAsync(string userMessage, List<(string sender, string message)> conversationHistory)
    {
        try
        {
            string responseText;

            if (_useWebSocket)
            {
                responseText = await SendOpenClawMessageAsync(userMessage, conversationHistory);
            }
            else if (_useOpenAIFormat)
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

    private async Task<string> SendOpenClawMessageAsync(string userMessage, List<(string sender, string message)> conversationHistory)
    {
        try
        {
            // Build URL - convert ws:// to http:// if needed
            var baseUrl = _baseUrl;
            if (baseUrl.StartsWith("ws://"))
            {
                baseUrl = "http://" + baseUrl.Substring(3);
            }
            else if (baseUrl.StartsWith("wss://"))
            {
                baseUrl = "https://" + baseUrl.Substring(4);
            }
            
            var url = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";
            
            // Build messages
            var messages = new List<object>();
            foreach (var (sender, message) in conversationHistory)
            {
                messages.Add(new { role = sender == "You" ? "user" : "assistant", content = message });
            }
            messages.Add(new { role = "user", content = userMessage });

            // Build request body
            var modelId = string.IsNullOrEmpty(_model) ? "openclaw" : _model;
            if (!string.IsNullOrEmpty(_agentId))
            {
                modelId = $"openclaw:{_agentId}";
            }
            
            // Debug output
            System.Diagnostics.Debug.WriteLine($"[OpenClaw] URL: {url}");
            System.Diagnostics.Debug.WriteLine($"[OpenClaw] Token: {(_token ?? "").Length} chars");
            System.Diagnostics.Debug.WriteLine($"[OpenClaw] AgentId: {_agentId}");
            System.Diagnostics.Debug.WriteLine($"[OpenClaw] Model: {modelId}");
            
            var requestBody = new
            {
                model = modelId,
                messages = messages,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Build headers
            _httpClient.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrEmpty(_token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            }
            if (!string.IsNullOrEmpty(_agentId))
            {
                _httpClient.DefaultRequestHeaders.Add("x-openclaw-agent-id", _agentId);
            }

            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                {
                    return "Error: Authentication required. Please check your Token in settings.";
                }
                return $"Error: HTTP {(int)response.StatusCode} - {response.ReasonPhrase}\nResponse: {errorContent}";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            
            // Parse response
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentProp))
                    {
                        return contentProp.GetString() ?? "No response from OpenClaw";
                    }
                }
            }
            catch
            {
                // Fallback
            }
            
            return responseJson;
        }
        catch (Exception ex)
        {
            return $"OpenClaw error: {ex.Message}\n\nToken length: {(_token ?? "").Length}\nAgentId: {_agentId}";
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

        // Add API key if provided, otherwise clear any previous header
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        var fullUrl = $"{_baseUrl}/chat/completions";
        var response = await _httpClient.PostAsync(fullUrl, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return $"Error: HTTP {(int)response.StatusCode} - {response.ReasonPhrase}\nURL: {fullUrl}\nResponse: {errorContent}";
        }

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
            // For OpenClaw, try to get models via WebSocket or return a placeholder
            if (_useWebSocket)
            {
                return await GetOpenClawModelsAsync();
            }

            string url;
            if (_useOpenAIFormat)
            {
                url = $"{_baseUrl}/models";
            }
            else
            {
                url = $"{_baseUrl}/api/tags";
            }

            // Add API key if provided, otherwise clear any previous header
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
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

    private async Task<List<string>> GetOpenClawModelsAsync()
    {
        try
        {
            // Build URL - convert ws:// to http:// if needed
            var baseUrl = _baseUrl;
            if (baseUrl.StartsWith("ws://"))
            {
                baseUrl = "http://" + baseUrl.Substring(3);
            }
            else if (baseUrl.StartsWith("wss://"))
            {
                baseUrl = "https://" + baseUrl.Substring(4);
            }
            
            var url = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";
            
            // Build headers
            _httpClient.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrEmpty(_token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            }
            if (!string.IsNullOrEmpty(_agentId))
            {
                _httpClient.DefaultRequestHeaders.Add("x-openclaw-agent-id", _agentId);
            }

            // Send a minimal request to check connectivity
            var modelId = string.IsNullOrEmpty(_model) ? "openclaw" : _model;
            if (!string.IsNullOrEmpty(_agentId))
            {
                modelId = $"openclaw:{_agentId}";
            }
            
            var requestBody = new
            {
                model = modelId,
                messages = new[] { new { role = "user", content = "ping" } },
                max_tokens = 1
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                return new List<string> { "OpenClaw (connected)" };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
            {
                return new List<string> { "OpenClaw (auth required)" };
            }
            else
            {
                return new List<string> { $"OpenClaw (error: {(int)response.StatusCode})" };
            }
        }
        catch (Exception ex)
        {
            return new List<string> { $"OpenClaw error: {ex.Message}" };
        }
    }
}
