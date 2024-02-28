using Agent.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.RegularExpressions;
using FluentResults;

namespace Agent.Services
{
    public delegate Task RepositoryQueryFunction();

    public class RepositoryQueryEntry
    {

        private RepositoryQueryFunction _queryFunction;

        public RepositoryQueryEntry(RepositoryQueryFunction queryFunction)
        {
            _queryFunction = queryFunction;
        }

        public void WriteToConsole()
        {
            _queryFunction.Invoke();
        }
    }

    /// <summary>
    /// Handles a code query session for a specific local repository.
    /// </summary>
    [AgentApi]
    public class RepositoryQuerySession
    {
        public string LocalRepoPath { get; internal set; }

        public List<RepositoryQueryEntry> RepositoryQueryEntries { get; internal set; }

        private readonly RepositorySummaryDataStore _repositorySummaryDataStore;
        private readonly RepositoryQueryService _repositoryQueryService;
        private readonly GitService _gitService;
        private List<RepositoryFile> _repoFiles;
        private Dictionary<string, string[]> _fileLinesCache = new Dictionary<string, string[]>();

        public RepositoryQuerySession(RepositoryQueryService repositoryQueryService, GitService gitService, RepositorySummaryDataStore repositorySummaryDataStore, string localRepoPath)
        {
            _repositoryQueryService = repositoryQueryService;
            _gitService = gitService;
            _repositorySummaryDataStore = repositorySummaryDataStore;
            LocalRepoPath = localRepoPath;
            RepositoryQueryEntries = new List<RepositoryQueryEntry>();
        }

        // Prints a summary about either a folder or file in the repository.  Use "" for a summary of the entire repository.
        [AgentApi]
        public async Task PrintRepositoryPathSummary(string path)
        {
            RepositoryQueryFunction workingSetAction = async () =>
            {
                await PrintRepositoryPathSummary(path);
            };
            RepositoryQueryEntries.Add(new RepositoryQueryEntry(workingSetAction));

            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintRepositoryPathSummary)}(path = {path}):");

            // Logic to print module summary
            var absolutePath = Path.Combine(LocalRepoPath, path);
            var repositorySummary = await _repositorySummaryDataStore.Get(absolutePath);
            PrintEndOutputWithMessage($"{repositorySummary?.Summary}");
        }

        // Prints the full C# code contents of the specified file without any modifications
        [AgentApi]
        [WorkingSet]
        public async Task PrintFileContents(string fileName)
        {
            RepositoryQueryFunction workingSetAction = async () =>
            {
                await PrintFileContents(fileName);
            };
            RepositoryQueryEntries.Add(new RepositoryQueryEntry(workingSetAction));

            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintFileContents)}(fileName = {fileName}):");

            var repoFile = FindFileInRepo(fileName);
            if (repoFile == null)
            {
                return;
            }

            await PrintContentAll(repoFile.Contents, 1);
            PrintEndOutputWithMessage();
        }

        // Prints the file at the specified lineNumber with linesToInclude above the specified lineNumber, as well as linesToInclude below.
        [AgentApi]
        public async Task PrintFileContentsAroundLine(string fileName, int lineNumber, int linesToInclude)
        {
            RepositoryQueryFunction workingSetAction = async () =>
            {
                await PrintFileContentsAroundLine(fileName, lineNumber, linesToInclude);
            };
            RepositoryQueryEntries.Add(new RepositoryQueryEntry(workingSetAction));

            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintFileContents)}(fileName = {fileName}):");

            var repoFile = FindFileInRepo(fileName);
            if (repoFile == null)
            {
                return;
            }

            await PrintContentAroundLine(repoFile.Contents, lineNumber, linesToInclude);
            PrintEndOutputWithMessage();
        }

        // Prints lines from files matching the specified pattern that contain the specified text.
        // Similar to Visual Studio's "Find in Files" functionality.
        [AgentApi]
        public async Task PrintMatchingSourceLines(string fileMatchingPattern, string text, bool caseSensitive = false, bool matchWholeWord = false)
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintMatchingSourceLines)}(fileMatchingPattern = {fileMatchingPattern}, text = {text}, caseSensitive = {caseSensitive}, matchWholeWord = {matchWholeWord}):");

            GetAllRepoFiles();

            // TODO gsemple: this will cause problems for files with the same name, we can check for conflicts later
            var regexPattern = Regex.Escape(fileMatchingPattern).Replace("\\*", ".*").Replace("\\?", ".");
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

        [AgentApi]
        public async Task PrintFunctionSourceCode(string className, string functionName)
        {
            Console.WriteLine($"BEGIN OUTPUT from {nameof(PrintFunctionSourceCode)}(className = {className}, functionName = {functionName}):");

            var repoFiles = GetAllRepoFiles();
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
                PrintEndOutputWithMessage($"ERROR: Could not find class '{className}' in any repository file.");
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

        public async Task<Result<RepositoryFile>> AddFileToRepo(string fileName, string fileContents)
        {
            var result = await _gitService.AddFile(LocalRepoPath, fileName, fileContents);
            if (result.IsFailed)
            {
                Console.WriteLine($"Failed to add file to repository: {result.Errors[0].Message}");
                return Result.Fail(result.Errors[0].Message);
            }

            // Update the internal cache to include the new file
            var newRepoFile = new RepositoryFile
            {
                FileName = Path.Combine(LocalRepoPath, fileName),
                Contents = fileContents
            };
            _repoFiles.Add(newRepoFile);

            // Update file lines cache
            var lines = fileContents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            _fileLinesCache[fileName] = lines;

            return newRepoFile;
        }

        public async Task<Result> UpdateFileInRepo(string fileName, string fileContents)
        {
            // Attempt to find the file within the repository.
            var repoFile = FindFileInRepo(fileName, logError: false);
            if (repoFile == null)
            {
                return Result.Fail($"File '{fileName}' not found in the repository.");
            }

            try
            {
                // Write the new contents to the file.
                var filePath = Path.Combine(LocalRepoPath, fileName);
                await File.WriteAllTextAsync(filePath, fileContents);

                // Use the Git service to add and commit the changes.
                var addResult = await _gitService.AddFile(LocalRepoPath, fileName, fileContents);
                if (addResult.IsFailed)
                {
                    return Result.Fail(addResult.Errors[0].Message);
                }

                // Update the internal cache.
                _fileLinesCache[fileName] = fileContents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                repoFile.Contents = fileContents;

                return Result.Ok();
            }
            catch (Exception ex)
            {
                return Result.Fail($"Failed to update file '{fileName}' in the repository. Error: {ex.Message}");
            }
        }

        public RepositoryFile FindFileInRepo(string fileName, bool logError = true)
        {
            var repoFiles = GetAllRepoFiles();
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
            var repositoryFiles = GetAllRepoFiles();
            foreach (var repositoryFile in repositoryFiles)
            {
                if (repositoryFile.FileName.Contains(fileName))
                {
                    return repositoryFile;
                }
            }

            return null;
        }

        public List<RepositoryFile> GetAllRepoFiles()
        {
            if (_repoFiles == null)
            {
                _repoFiles = new List<RepositoryFile>();

                var listResult = _gitService.ListRepositoryFiles(LocalRepoPath).GetAwaiter().GetResult();
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
}
