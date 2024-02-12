﻿using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using FluentResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using Rystem.OpenAi;

namespace BizDevAgent.Services
{
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
        private readonly OpenAI_API.Models.Model _model;

        private static string DataPath => Path.Combine(Paths.GetDataPath(), "PromptCacheDB");

        public LanguageModelService(IConfiguration configuration)
        {
            var apiKey = configuration.GetValue<string>("OpenAiApiKey");
            _api = new OpenAIAPI(apiKey);
            _promptResponseCache = new PromptResponseCacheDataStore(DataPath);
            _model = new OpenAI_API.Models.Model("gpt-4-0125-preview") { OwnedBy = "openai" };
        }

        public async Task<ChatConversationResult> ChatCompletion(string prompt, Conversation conversation = null, bool allowCaching = true)
        {
            var temperature = 0.0;
            string cacheKey = $"{_model.ModelID}_{temperature}_{prompt}";

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
                    conversation.Model = _model;
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
                    var newEntry = new PromptResponseCacheEntry { ModelId = _model.ModelID, Temperature = temperature, Prompt = prompt, Response = message, TimeGenerated = DateTime.UtcNow };
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