using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using FluentResults;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Chat;

namespace BizDevAgent.Services
{
    public class PromptResponseCacheEntry
    {
        public string Prompt { get; set; }
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
            return entity.FirstOrDefault()?.Prompt ?? throw new InvalidOperationException("Cannot get key for empty entry list.");
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

        private static string DataPath => Path.Combine(Paths.GetDataPath(), "PromptCacheDB");

        public LanguageModelService(IConfiguration configuration)
        {
            var apiKey = configuration.GetValue<string>("OpenAiApiKey");
            _api = new OpenAIAPI(apiKey);
            _promptResponseCache = new PromptResponseCacheDataStore(DataPath);
        }

        public async Task<ChatConversationResult> ChatCompletion(string prompt, Conversation conversation = null, bool allowCaching = true)
        {
            var cachedResponses = allowCaching ? await _promptResponseCache.Get(prompt) : null;
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
                    //conversation.Model = OpenAI_API.Models.Model.GPT4;
                    conversation.Model = OpenAI_API.Models.Model.ChatGPTTurbo;
                    conversation.RequestParameters.Temperature = 0.5;
                }
                conversation.AppendUserInput(prompt);

                string message = await conversation.GetResponseFromChatbotAsync();
                var result = conversation.MostRecentApiResult;

                // Check if the response is already cached
                var isResponseUnique = cachedResponses == null || !cachedResponses.Any(r => r.Response == message);
                if (isResponseUnique)
                {
                    // Cache the new response if it's unique
                    var newEntry = new PromptResponseCacheEntry { Prompt = prompt, Response = message, TimeGenerated = DateTime.UtcNow };
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
