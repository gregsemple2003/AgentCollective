using Newtonsoft.Json;
using OpenAI_API.Chat;

namespace Agent.Core
{
    public class ModelDescriptor
    {
        public string Id { get; set; }
        public string OwnerId { get; set; }
    }

    // TODO gsemple: some leakage here using openai data structures
    public class ChatCompletionResult
    {
        /// <summary>
        /// The list of choices that the user was presented with during the chat interaction
        /// </summary>
        [JsonProperty("choices")]
        public IReadOnlyList<ChatChoice> Choices { get; set; }

        /// <summary>
        /// The usage statistics for the chat interaction
        /// </summary>
        [JsonProperty("usage")]
        public ChatUsage Usage { get; set; }
    }

    public interface ILanguageModel
    {
        Task<ChatCompletionResult> ChatCompletion(string prompt, bool allowCaching = true, ModelDescriptor? modelOverride = null);

        ILanguageParser CreateLanguageParser();

    }
}
