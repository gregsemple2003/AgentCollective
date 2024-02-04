using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace BizDevAgent.Agents
{
    /// <summary>
    /// An agent that is called by LLMs with a simple API that dumps information to the console, and can 
    /// be piped back into prompts for the LLM to make decisions.  This isn't normally used directly by
    /// application code.  It is used by LLMs when preparing an implementation plan to modify source
    /// code.
    /// </summary>
    public class CodeQueryAgent : Agent
    {
        private readonly SourceSummaryDataStore _sourceSummaryDataStore;
        private readonly VisualStudioAgent _visualStudioAgent;
        private List<ProjectFile> _projectFiles;
        private Dictionary<string, string[]> _fileLinesCache = new Dictionary<string, string[]>();

        public CodeQueryAgent(SourceSummaryDataStore sourceSummaryDataStore, VisualStudioAgent visualStudioAgent)
        {
            _sourceSummaryDataStore = sourceSummaryDataStore;
            _visualStudioAgent = visualStudioAgent;
        }

        // Prints the names of all files and a short description about each
        public async Task PrintModuleSummary()
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintModuleSummary)}:");

            // Logic to print module summary
            var sourceSummary = await _sourceSummaryDataStore.Get(Paths.GetProjectPath());
            PrintEndOutputWithMessage($"{sourceSummary.DetailedSummary}");
        }

        // Prints the C# code skeleton of the specified file, stripped of method bodies but including comments
        public async Task PrintFileSkeleton(string fileName)
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintFileSkeleton)}(fileName = {fileName}):");

            var projectFile = await GetCachedProjectFile(fileName);
            if (projectFile == null) 
            {
                PrintEndOutputWithMessage($"ERROR: Could not find file in project named '{fileName}'");
                return;
            }

            var sourceSummary = await _sourceSummaryDataStore.Get(projectFile.FileName);
            PrintEndOutputWithMessage($"{sourceSummary.DetailedSummary}");
        }

        // Prints the full C# code contents of the specified file without any modifications
        public async Task PrintFileContents(string fileName)
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintFileContents)}(fileName = {fileName}):");

            var projectPath = Paths.GetProjectPath();
            var projectFiles = await GetCachedProjectFiles(projectPath);
            var projectFile = projectFiles.Find(x => Path.GetFileName(x.FileName) == Path.GetFileName(fileName)); // TODO gsemple: source code relative path?
            if (projectFile == null)
            {
                PrintEndOutputWithMessage($"ERROR: Could not find file in project named '{fileName}'");
                return;
            }

            PrintEndOutputWithMessage(projectFile.Contents);
        }

        // Prints lines from files matching the specified pattern that contain the specified text.
        // Similar to Visual Studio's "Find in Files" functionality.
        public async Task PrintMatchingSourceLines(string fileMatchingPattern, string text, bool caseSensitive = false, bool matchWholeWord = false)
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintMatchingSourceLines)}(fileMatchingPattern = {fileMatchingPattern}, text = {text}, caseSensitive = {caseSensitive}, matchWholeWord = {matchWholeWord}):");

            if (_projectFiles == null || !_projectFiles.Any())
            {
                await GetCachedProjectFiles(Paths.GetProjectPath());
            }

            var regexPattern = "^" + Regex.Escape(fileMatchingPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

            string wordPattern = matchWholeWord ? $"\\b{text}\\b" : Regex.Escape(text);
            RegexOptions options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            Regex textRegex = new Regex(wordPattern, options);

            foreach (var projectFile in _projectFiles)
            {
                if (regex.IsMatch(projectFile.FileName))
                {
                    if (!_fileLinesCache.TryGetValue(projectFile.FileName, out var lines))
                    {
                        lines = projectFile.Contents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        _fileLinesCache[projectFile.FileName] = lines;
                    }

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (textRegex.IsMatch(lines[i])) // Adjusted for case sensitivity and whole word matching
                        {
                            var relativePath = Path.GetRelativePath(Paths.GetSourceControlRootPath(), projectFile.FileName);
                            Console.WriteLine($"{relativePath}({i + 1}): {lines[i]}");
                        }
                    }
                }
            }

            PrintEndOutputWithMessage();
        }

        public async Task PrintFunctionSourceCode(string className, string functionName)
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintFunctionSourceCode)}(className = {className}, functionName = {functionName}):");

            var projectFiles = await GetCachedProjectFiles(Paths.GetProjectPath());
            ProjectFile targetFile = null;

            // Attempt to find the file containing the class
            foreach (var projectFile in projectFiles)
            {
                if (projectFile.Contents.Contains($"class {className}"))
                {
                    targetFile = projectFile;
                    break;
                }
            }

            if (targetFile == null)
            {
                PrintEndOutputWithMessage($"ERROR: Could not find class '{className}' in any project file.");
                return;
            }

            // Assuming we have a method to extract the function's source code from the file
            string functionSourceCode = ExtractFunctionSourceCode(targetFile.Contents, className, functionName);

            if (string.IsNullOrEmpty(functionSourceCode))
            {
                PrintEndOutputWithMessage($"ERROR: Could not find function '{functionName}' in class '{className}'.");
                return;
            }

            PrintEndOutputWithMessage(functionSourceCode);
        }

        // This method needs to be implemented to parse the file's content and extract the specific function's source code.
        private string ExtractFunctionSourceCode(string fileContents, string className, string functionName)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(fileContents);
            var root = syntaxTree.GetRoot() as CompilationUnitSyntax;

            // Find the class declaration within the file
            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration == null)
            {
                return ""; // Class not found
            }

            // Find the method declaration within the class
            var methodDeclaration = classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == functionName);

            if (methodDeclaration == null)
            {
                return ""; // Method not found
            }

            // Extract the method's source code, including leading and trailing trivia (whitespace, comments)
            var sourceCode = methodDeclaration.ToFullString();

            return sourceCode;
        }

        private async Task<ProjectFile> GetCachedProjectFile(string fileName)
        {
            var projectFiles = await GetCachedProjectFiles(Paths.GetProjectPath());
            foreach (var projectFile in projectFiles)
            {
                if (projectFile.FileName.Contains(fileName))
                {
                    return projectFile;
                }
            }

            return null;
        }

        private async Task<List<ProjectFile>> GetCachedProjectFiles(string projectPath)
        {
            if (_projectFiles == null)
            {
                _projectFiles = new List<ProjectFile>();

                var projectFiles = await _visualStudioAgent.LoadProjectFiles(projectPath);
                foreach (var projectFile in projectFiles)
                {
                    _projectFiles.Add(projectFile);
                }
            }

            return _projectFiles;
        }

        private void PrintEndOutputWithMessage(string message = null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Console.WriteLine(message);
            }
            Console.WriteLine("END OUTPUT");
            Console.WriteLine();
        }
    }
}
