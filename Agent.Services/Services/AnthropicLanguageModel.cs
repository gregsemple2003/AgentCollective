using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Agent.Core;
using System.Net.Http.Headers;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Net.Http;
using System.Text;

namespace Agent.Services
{
    public class AnthropicResponseParser : ILanguageParser
    {
        public List<string> ExtractResponseTokens(string response)
        {
            var tokens = new List<string>();

            // Regular expression to match tokens starting with @ and followed by alphanumeric characters
            var regex = new Regex(@"@(\w+)");

            // Find matches in the input text
            var matches = regex.Matches(response);

            foreach (Match match in matches)
            {
                // Add the matched token to the list, excluding the @ symbol
                tokens.Add(match.Groups[1].Value);
            }

            return tokens;
        }

        public List<ResponseSnippet> ExtractSnippets(string response)
        {
            var snippets = new List<ResponseSnippet>();
            // Regular expression to match fenced code blocks with optional language identifiers
            var regex = new Regex(@"```(.*?)\n(.*?)```", RegexOptions.Singleline);

            var matches = regex.Matches(response);

            foreach (Match match in matches)
            {
                var snippet = new ResponseSnippet
                {
                    LanguageId = match.Groups[1].Value.Trim(),
                    Contents = match.Groups[2].Value.Trim()
                };
                snippets.Add(snippet);
            }

            return snippets;
        }
    }

    public class AnthropicLanguageModel : Service, ILanguageModel
    {
        private readonly HttpClient _httpClient;
        private readonly PromptResponseCacheDataStore _promptResponseCache;
        private readonly ModelDescriptor _defaultModel;
        private readonly ModelDescriptor _lowTierModel;
        private readonly string _apiKey;

        private static string DataPath => Path.Combine(Paths.GetDataPath(), "AnthropicPromptCacheDB");

        public AnthropicLanguageModel(IConfiguration configuration)
        {
            _apiKey = configuration.GetValue<string>("AnthropicApiKey");
            _promptResponseCache = new PromptResponseCacheDataStore(DataPath);
            _defaultModel = new ModelDescriptor { Id = "claude-3-opus-20240229" };
            _lowTierModel = new ModelDescriptor { Id = "claude-3-opus-20240229" };

            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.anthropic.com/");
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public ILanguageParser CreateLanguageParser()
        {
            return new AnthropicResponseParser();
        }

        public ModelDescriptor GetLowTierModel()
        {
            return _lowTierModel;
        }

        public async Task<ChatCompletionResult> ChatCompletion(string prompt, bool allowCaching = true, ModelDescriptor? modelOverride = null)
        {
            var temperature = 0.7;
            var model = modelOverride ?? _defaultModel;
            string cacheKey = $"{model.Id}_{temperature}_{prompt}";

            var cachedResponses = allowCaching ? await _promptResponseCache.Get(cacheKey) : null;
            if (cachedResponses != null && cachedResponses.Count >= 1)
            {
                // Return a random cached response
                var random = new Random();
                var randomResponse = cachedResponses[random.Next(cachedResponses.Count)].Response;
                return new ChatCompletionResult
                {
                    Choices = new List<ChatChoice>
                    {
                        new ChatChoice { Message = new ChatMessage { TextContent = randomResponse } }
                    },
                    Conversation = null
                };
            }
            else
            {
                // Perform the remote request
                var apiResponse = await GetChatCompletionFromApiAsync(prompt);

                if (string.IsNullOrEmpty(apiResponse))
                {
                    // Handle case where API response is null or empty
                    return null;
                }

                // Deserialize and process the response as needed
                var responseJson = JsonConvert.DeserializeObject<dynamic>(apiResponse);
                var message = responseJson?.content?[0]?.text.ToString();

                // Parse usage data
                var usage = new Core.Usage
                {
                    InputTokens = responseJson?.usage?.input_tokens,
                    OutputTokens = responseJson?.usage?.output_tokens
                };

                // Check if the response is already cached
                var isResponseUnique = cachedResponses == null || !cachedResponses.Any(r => r.Response == message);
                if (isResponseUnique)
                {
                    // Cache the new response if it's unique
                    var newEntry = new PromptResponseCacheEntry { ModelId = model.Id, Temperature = temperature, Prompt = prompt, Response = message, TimeGenerated = DateTime.UtcNow };
                    var entries = cachedResponses ?? new List<PromptResponseCacheEntry>();
                    entries.Add(newEntry);
                    _promptResponseCache.Add(entries, shouldOverwrite: true);
                }

                // Use `message` as needed, for example, caching it or returning it as part of ChatConversationResult
                return new ChatCompletionResult
                {
                    Choices = new List<ChatChoice>
                    {
                        new ChatChoice { Message = new ChatMessage { TextContent = message } }
                    },
                    Usage = usage
                };
            }
        }

        private async Task<string> GetChatCompletionFromApiAsync(string prompt)
        {
            var payload = new
            {
                model = _defaultModel.Id,
                max_tokens = 1024,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var response = await _httpClient.PostAsync("v1/messages", new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                // Handle non-success status code appropriately
                return null;
            }

            var contentString = await response.Content.ReadAsStringAsync();
            return contentString;
        }

    }
}
