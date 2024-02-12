using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing.Printing;

namespace BizDevAgent.Services
{
    /// <summary>
    /// Handles a code query session for a specific local repository.
    /// </summary>
    public class RepositoryQuerySession
    {
        public string LocalRepoPath { get; internal set; }

        private readonly SourceSummaryDataStore _sourceSummaryDataStore;
        private readonly RepositoryQueryService _repositoryQueryService;
        private readonly GitService _gitService;
        private List<RepositoryFile> _repoFiles;
        private Dictionary<string, string[]> _fileLinesCache = new Dictionary<string, string[]>();

        public RepositoryQuerySession(RepositoryQueryService repositoryQueryService, GitService gitService, SourceSummaryDataStore sourceSummaryDataStore, string localRepoPath)
        {
            _repositoryQueryService = repositoryQueryService;
            _gitService = gitService;
            _sourceSummaryDataStore = sourceSummaryDataStore;
            LocalRepoPath = localRepoPath;
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

            var repoFile = await FindFileInRepo(fileName);
            if (repoFile == null)
            {
                return;
            }

            var sourceSummary = await _sourceSummaryDataStore.Get(repoFile.FileName);
            PrintEndOutputWithMessage($"{sourceSummary.DetailedSummary}");
        }

        // Prints the full C# code contents of the specified file without any modifications
        public async Task PrintFileContents(string fileName)
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintFileContents)}(fileName = {fileName}):");

            var repoFile = await FindFileInRepo(fileName);
            if (repoFile == null)
            {
                return;
            }

            await PrintContentAll(repoFile.Contents, 1);
            PrintEndOutputWithMessage();
        }

        // Prints the file at the specified lineNumber with linesToInclude above the specified lineNumber, as well as linesToInclude below.
        public async Task PrintFileContentsAroundLine(string fileName, int lineNumber, int linesToInclude)
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintFileContents)}(fileName = {fileName}):");

            var repoFile = await FindFileInRepo(fileName);
            if (repoFile == null)
            {
                return;
            }

            await PrintContentAroundLine(repoFile.Contents, lineNumber, linesToInclude);
            PrintEndOutputWithMessage();
        }

        // Prints lines from files matching the specified pattern that contain the specified text.
        // Similar to Visual Studio's "Find in Files" functionality.
        public async Task PrintMatchingSourceLines(string fileMatchingPattern, string text, bool caseSensitive = false, bool matchWholeWord = false)
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintMatchingSourceLines)}(fileMatchingPattern = {fileMatchingPattern}, text = {text}, caseSensitive = {caseSensitive}, matchWholeWord = {matchWholeWord}):");

            await GetAllRepoFiles();

            var regexPattern = "^" + Regex.Escape(fileMatchingPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

            string wordPattern = matchWholeWord ? $"\\b{text}\\b" : Regex.Escape(text);
            RegexOptions options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            Regex textRegex = new Regex(wordPattern, options);

            foreach (var repositoryFile in _repoFiles)
            {
                if (regex.IsMatch(repositoryFile.FileName))
                {
                    if (!_fileLinesCache.TryGetValue(repositoryFile.FileName, out var lines))
                    {
                        lines = repositoryFile.Contents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        _fileLinesCache[repositoryFile.FileName] = lines;
                    }

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (textRegex.IsMatch(lines[i])) // Adjusted for case sensitivity and whole word matching
                        {
                            var relativePath = Path.GetRelativePath(Paths.GetSourceControlRootPath(), repositoryFile.FileName);
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

            var repoFiles = await GetAllRepoFiles();
            RepositoryFile targetFile = null;

            // Attempt to find the file containing the class
            foreach (var repoFile in repoFiles)
            {
                if (repoFile.Contents.Contains($"class {className}"))
                {
                    targetFile = repoFile;
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

        private Task PrintContentAroundLine(string content, int targetLineNo, int linesToInclude)
        {
            // Split the content into lines
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Calculate the range of lines to print
            int startLine = Math.Max(targetLineNo - linesToInclude, 1);
            int endLine = Math.Min(targetLineNo + linesToInclude, lines.Length);

            // Iterate through each line within the range and print it with the line number
            for (int i = startLine - 1; i < endLine; i++) // Adjust for zero-based index
            {
                // Print the line number followed by the line content
                Console.WriteLine($"{i + 1}: {lines[i]}");
            }

            return Task.CompletedTask;
        }

        private Task PrintContentAll(string content, int startingLineNo)
        {
            // Split the content into lines
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Iterate through each line and print it with the line number
            for (int i = 0; i < lines.Length; i++)
            {
                // Calculate the current line number
                int currentLineNo = startingLineNo + i;

                // Print the line number followed by the line content
                Console.WriteLine($"{currentLineNo}: {lines[i]}");
            }

            return Task.CompletedTask;
        }

        // TODO gsemple: hide this from the research api
        public async Task<RepositoryFile> FindFileInRepo(string fileName, bool logError = true)
        {
            var repoFiles = await GetAllRepoFiles();
            var strings = new List<string>();
            foreach(var repoFile2 in repoFiles)
            {
                strings.Add(repoFile2.FileName);
            }
            var repoFile = repoFiles.Find(x => Path.GetFileName(x.FileName) == Path.GetFileName(fileName));
            if (repoFile == null)
            {
                if (logError)
                {
                    PrintEndOutputWithMessage($"ERROR: Could not find file in repository named '{fileName}'");
                }
                return null;
            }

            return repoFile;
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

        private async Task<RepositoryFile> GetCachedProjectFile(string fileName)
        {
            var repositoryFiles = await GetAllRepoFiles();
            foreach (var repositoryFile in repositoryFiles)
            {
                if (repositoryFile.FileName.Contains(fileName))
                {
                    return repositoryFile;
                }
            }

            return null;
        }

        // TODO gsemple: hide this from the research api
        public async Task<List<RepositoryFile>> GetAllRepoFiles()
        {
            if (_repoFiles == null)
            {
                _repoFiles = new List<RepositoryFile>();

                // TODO gsemple: this should really be drawn from the git agent

                var listResult = await _gitService.ListRepositoryFiles(LocalRepoPath);
                if (listResult.IsFailed)
                {
                    throw new InvalidOperationException("Could not list files in git repository.");
                }

                foreach (var repoFile in listResult.Value)
                {
                    _repoFiles.Add(repoFile);
                }
            }

            return _repoFiles;
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

    /// <summary>
    /// An agent that is called by LLMs with a simple API that dumps information to the console, and can 
    /// be piped back into prompts for the LLM to make decisions.  This isn't normally used directly by
    /// application code.  It is used by LLMs when preparing an implementation plan to modify source
    /// code.
    /// </summary>
    public class RepositoryQueryService : Service
    {
        private readonly SourceSummaryDataStore _sourceSummaryDataStore;
        private readonly VisualStudioService _visualStudioService;
        private readonly GitService _gitService;
        private readonly Dictionary<string, RepositoryQuerySession> _sessionsCache = new Dictionary<string, RepositoryQuerySession>();

        public RepositoryQueryService(SourceSummaryDataStore sourceSummaryDataStore, VisualStudioService visualStudioService, GitService gitService)
        {
            _sourceSummaryDataStore = sourceSummaryDataStore;
            _visualStudioService = visualStudioService;
            _gitService = gitService;
        }

        public RepositoryQuerySession CreateSession(string localRepoPath)
        {
            // Check if a session for the given path already exists in the cache
            if (!_sessionsCache.TryGetValue(localRepoPath, out RepositoryQuerySession session))
            {
                // If it doesn't exist, create a new session and add it to the cache
                session = new RepositoryQuerySession(this, _gitService, _sourceSummaryDataStore, localRepoPath);
                _sessionsCache[localRepoPath] = session;
            }

            // Return the existing or new session
            return session;
        }

    }
}
