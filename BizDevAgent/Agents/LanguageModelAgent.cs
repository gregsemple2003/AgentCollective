using Microsoft.Extensions.Configuration;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using OpenAI_API.Models;
using OpenAI_API.Moderation;


namespace BizDevAgent.Agents
{
    public class LanguageModelAgent : Agent
    {
        private readonly OpenAIAPI _api;

        public LanguageModelAgent(IConfiguration configuration)
        {
            var apiKey = configuration.GetValue<string>("OpenAiApiKey");
            _api = new OpenAIAPI(apiKey);
        }

        public async Task<ChatResult> ChatCompletion(string prompt)
        {
            var conversation = _api.Chat.CreateConversation();
            conversation.Model = OpenAI_API.Models.Model.ChatGPTTurbo;
            conversation.RequestParameters.Temperature = 0;
            conversation.AppendUserInput(prompt);

            string message = await conversation.GetResponseFromChatbotAsync();
            return conversation.MostRecentApiResult;
        }

    }
}
