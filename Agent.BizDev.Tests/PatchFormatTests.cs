using Agent.Agents;
using Agent.DataStore;
using Agent.Flow;
using Agent.Jobs;
using Agent.Services;
using Agent.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Rystem.OpenAi;
using System.Text;

public interface ICodeLinesProcessor
{
    void ModifyCode(ref List<string> codeLines);
}

public interface ICodeProcessor
{
    public string ModifyCode(string code);
}

namespace Agent.Tests
{
    [TestFixture]
    internal class PatchFormatTests
    {
        private ServiceProvider _serviceProvider;
        private AssetDataStore _assetDataStore;
        private LanguageModelService _languageModelService;
        private VisualStudioService _visualStudioService;

        [OneTimeSetUp]
        public void SetUp()
        {
            var serviceCollection = ServiceConfiguration.ConfigureServices();
            _serviceProvider = serviceCollection.BuildServiceProvider();
            _assetDataStore = _serviceProvider.GetService<AssetDataStore>();
            _languageModelService = _serviceProvider.GetService<LanguageModelService>();
            _visualStudioService = _serviceProvider.GetService<VisualStudioService>();
        }

        /// <summary>
        /// Does minimizing the context window help with the quality of the response?  Yes, it seems to improve
        ///     - ability to follow implicit coding convention in the file
        ///     - ability to think of the de facto standard (e.g. Microsoft.Logging) instead of just make its own puny Logger class
        /// However the follow-up method of then converting the good answer into a custom patch does not work very well,
        /// in this case it's gibberish.
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task Patch_UsingFollowupPrompt()
        {
            var promptText = _assetDataStore.GetHardRef<PromptAsset>("FixBuildError1_Initial").Evaluate(null);
            var createPatchText = _assetDataStore.GetHardRef<PromptAsset>("FixBuildError1_CreatePatch").Evaluate(null);
            var result = await _languageModelService.ChatCompletion(promptText);
            result = await _languageModelService.ChatCompletion(createPatchText, result.Conversation);
        }

        /// <summary>
        /// Slightly larger context window than minimally necessary, generates similar quality code as above
        /// though perhaps slightly less.  However has errors in the custom patch format, off-by-one line errors,
        /// it may not understand that when you insert a line you have to increment the index.
        /// </summary>
        class TestPatchAListPrompt_PromptContext : AgentPromptContext
        {
            public string CodeAsList { get; set; }
        }
        [Test]
        public async Task Patch_UsingCodeAsList()
        {
            var testCodeText = _assetDataStore.GetHardRef<TextAsset>("CompanyDataStore");
            var sb = new System.Text.StringBuilder();
            var lines = testCodeText.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            sb.AppendLine(@"List<string> myList = new List<string> {");
            for (int i = 0; i < lines.Count; i++)
            {
                // Escape double quotes and wrap the line in double quotes
                string processedLine = $"\"{lines[i].Replace("\"", "\\\"")}\"";

                sb.Append(processedLine);

                // If not the last line, add a comma
                if (i < lines.Count - 1)
                {
                    sb.AppendLine(", ");
                }
            }

            sb.AppendLine(@"};");
            var promptContext = new TestPatchAListPrompt_PromptContext();
            promptContext.CodeAsList = sb.ToString();

            // Run the prompt completion
            var promptTextAsset = _assetDataStore.GetHardRef<PromptAsset>("FixBuildError2_Initial");
            var promptText = promptTextAsset.Evaluate(promptContext);
            var result = await _languageModelService.ChatCompletion(promptText);

            // Extract the code and run it
            var response = result.ChatResult.ToString();
            var responseParser = _languageModelService.CreateLanguageParser();
            var snippets = responseParser.ExtractSnippets(response);
            foreach (var snippet in snippets)
            {
                if (snippet.LanguageId == "csharp")
                {
                    var assembly = _visualStudioService.InjectCode(snippet.Contents);
                    var assemblyType = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("CodeLinesProcessor"));
                    var codeLinesProcessor = (ICodeLinesProcessor)ActivatorUtilities.CreateInstance(_serviceProvider, assemblyType);
                    codeLinesProcessor.ModifyCode(ref lines);
                    var updatedCode = string.Join(Environment.NewLine, lines);
                }
            }

        }

        /// <summary>
        /// Could not evaluate, produced Roslyn code that does not compile.
        /// </summary>
        class Patch_UsingRosalyn_PromptContext : AgentPromptContext
        {
            public string Code { get; set; }
        }
        [Test]
        public async Task Patch_UsingRosalyn()
        {
            //var testCodeText = _assetDataStore.GetHardRef<TextAsset>("CompanyDataStore");

            //// Build the prompt context
            //var promptContext = new Patch_UsingRosalyn_PromptContext();
            //promptContext.Code = testCodeText.Text;

            //// Run the prompt completion
            //var promptTextAsset = _assetDataStore.GetHardRef<PromptAsset>("FixBuildError3_Initial");
            //var prompt = promptTextAsset.Evaluate(promptContext);
            //var result = await _languageModelService.ChatCompletion(prompt);

            //// Extract the code and run it
            //var response = result.ChatResult.ToString();
            //var responseParser = _languageModelService.CreateResponseParser();
            //var snippets = responseParser.ExtractSnippets(response);
            //foreach (var snippet in snippets)
            //{
            //    if (snippet.LanguageId == "csharp")
            //    {
            //        var assembly = _visualStudioService.InjectCode(snippet.Contents);
            //        var assemblyType = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("CodeProcessor"));
            //        var codeLinesProcessor = (ICodeProcessor)ActivatorUtilities.CreateInstance(_serviceProvider, assemblyType);
            //        codeLinesProcessor.ModifyCode(promptContext.Code);
            //        var updatedCode = string.Join(Environment.NewLine, promptContext.Code);
            //    }
            //}
        }

        class Patch_UsingChunks_PromptContext : AgentPromptContext
        {
            public string Code { get; set; }
        }
        [Test]
        public async Task Patch_UsingChunks()
        {
            var testCodeText = _assetDataStore.GetHardRef<TextAsset>("CompanyDataStore");

            // Build the prompt context
            var promptContext = new Patch_UsingRosalyn_PromptContext();
            promptContext.Code = DiffUtils.AddChunkMarkers(testCodeText.Text, 5);

            // Run the prompt completion
            var promptTextAsset = _assetDataStore.GetHardRef<PromptAsset>("FixBuildError4_Initial");
            var prompt = promptTextAsset.Evaluate(promptContext);
            var result = await _languageModelService.ChatCompletion(prompt, allowCaching: false);

            // Extract the code and run it
            var response = result.ChatResult.ToString();
            var responseParser = _languageModelService.CreateLanguageParser();
            var snippets = responseParser.ExtractSnippets(response);
            var correctedChunks = new List<string>();
            foreach (var snippet in snippets)
            {
                if (snippet.LanguageId == "csharp")
                {
                    correctedChunks.Add(snippet.Contents);
                }
            }

            var patchedCode = DiffUtils.ApplyPatchWithChunks(promptContext.Code, correctedChunks);
            patchedCode = DiffUtils.RemoveChunkMarkers(patchedCode);
        }

    }
}
