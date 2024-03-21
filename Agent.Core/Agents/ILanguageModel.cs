using Newtonsoft.Json;
using OpenAI_API.Chat;

namespace Agent.Core
{
    public class ModelDescriptor
    {
        public string Id { get; set; }
        public string OwnerId { get; set; }
    }

    public class Usage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

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
        public Usage Usage { get; set; }
        public Conversation Conversation { get; set; }

        public string ToString()
        {
            if (Choices == null) return string.Empty;
            if (Choices.Count == 0) return string.Empty;

            return string.Join(", ", Choices);
        }
    }

    public interface ILanguageModel
    {
        Task<ChatCompletionResult> ChatCompletion(string prompt, bool allowCaching = true, ModelDescriptor? modelOverride = null);

        ILanguageParser CreateLanguageParser();

        ModelDescriptor GetLowTierModel();

    }
}
