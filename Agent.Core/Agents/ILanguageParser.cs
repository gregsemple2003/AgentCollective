namespace Agent.Core
{
    /// <summary>
    /// A snippet in Markdown format, which is ```<languageid> // contents go here```
    /// </summary>
    public class ResponseSnippet
    {
        public string LanguageId;
        public string Contents;
    }

    /// <summary>
    /// Abstracts how information such as code snippets are presented across different language models.
    /// </summary>
    public interface ILanguageParser
    {
        List<string> ExtractResponseTokens(string response);
        List<ResponseSnippet> ExtractSnippets(string response);
    }
}
