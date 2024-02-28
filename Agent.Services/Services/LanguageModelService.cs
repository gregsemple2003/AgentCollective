using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Text.RegularExpressions;
using Agent.Core;

namespace Agent.Services
{
    public class OpenAiResponseParser : IResponseParser
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

    public class PromptResponseCacheEntry
    {
        public string Prompt { get; set; }
        public string ModelId { get; set; }
        public double Temperature { get; set; }
        public string Response { get; set; }
        public DateTime TimeGenerated { get; set; }
    }

    public class PromptResponseCacheDataStore : RocksDbDataStore<List<PromptResponseCacheEntry>>
    {
        public PromptResponseCacheDataStore(string path) : base(path, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }, shouldWipe: false)
        {
        }

        protected override string GetKey(List<PromptResponseCacheEntry> entity)
        {
            var entry = entity.FirstOrDefault();
            if (entry == null) throw new InvalidOperationException("Cannot get key for empty entry list.");

            string cacheKey = $"{entry.ModelId}_{entry.Temperature}_{entry.Prompt}";
            return cacheKey;
        }
    }

    public class ChatConversationResult
    {
        public ChatResult ChatResult { get; set; }
        public Conversation Conversation { get; set; }
    }

    public class LanguageModelService : Service
    {
        private readonly OpenAIAPI _api;
        private readonly PromptResponseCacheDataStore _promptResponseCache;
        private readonly OpenAI_API.Models.Model _defaultModel;
        private readonly OpenAI_API.Models.Model _lowTierModel;

        private static string DataPath => Path.Combine(Paths.GetDataPath(), "PromptCacheDB");

        public LanguageModelService(IConfiguration configuration)
        {
            var apiKey = configuration.GetValue<string>("OpenAiApiKey");
            _api = new OpenAIAPI(apiKey);
            _promptResponseCache = new PromptResponseCacheDataStore(DataPath);
            _defaultModel = new OpenAI_API.Models.Model("gpt-4-0125-preview") { OwnedBy = "openai" };
            _lowTierModel = new OpenAI_API.Models.Model("gpt-3.5-turbo-0125") { OwnedBy = "openai" };
        }

        public IResponseParser CreateResponseParser()
        {
            return new OpenAiResponseParser();
        }

        public OpenAI_API.Models.Model GetLowTierModel()
        {
            return _lowTierModel;
        }

        public async Task<ChatConversationResult> ChatCompletion(string prompt, Conversation conversation = null, bool allowCaching = true, OpenAI_API.Models.Model modelOverride = null)
        {
            var temperature = 0.7;
            var model = modelOverride ?? _defaultModel;
            string cacheKey = $"{model.ModelID}_{temperature}_{prompt}";

            var cachedResponses = allowCaching ? await _promptResponseCache.Get(cacheKey) : null;
            if (cachedResponses != null && cachedResponses.Count >= 1)
            {
                // Return a random cached response
                var random = new Random();
                var randomResponse = cachedResponses[random.Next(cachedResponses.Count)].Response;
                return new ChatConversationResult
                {
                    ChatResult = new ChatResult
                    {
                        Choices = new List<ChatChoice>
                        {
                            new ChatChoice { Message = new ChatMessage { TextContent = randomResponse } }
                        }
                    },
                    Conversation = null // TODO gsemple handle conversations with prompt cache??
                };
            }
            else
            {
                if (conversation == null)
                {
                    // Call OpenAI API for a response
                    conversation = _api.Chat.CreateConversation();
                    conversation.Model = model;
                    conversation.RequestParameters.Temperature = temperature;
                }
                conversation.AppendUserInput(prompt);

                string message = await conversation.GetResponseFromChatbotAsync();
                var result = conversation.MostRecentApiResult;

                // Check if the response is already cached
                var isResponseUnique = cachedResponses == null || !cachedResponses.Any(r => r.Response == message);
                if (isResponseUnique)
                {
                    // Cache the new response if it's unique
                    var newEntry = new PromptResponseCacheEntry { ModelId = model.ModelID, Temperature = temperature, Prompt = prompt, Response = message, TimeGenerated = DateTime.UtcNow };
                    var entries = cachedResponses ?? new List<PromptResponseCacheEntry>();
                    entries.Add(newEntry);
                    _promptResponseCache.Add(entries, shouldOverwrite: true);
                }

                return new ChatConversationResult
                {
                    ChatResult = result,
                    Conversation = conversation
                };
            }
        }

    }
}
