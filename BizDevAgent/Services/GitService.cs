using FluentResults;
using System.Diagnostics;
using System.Text;
using BizDevAgent.Utilities;

namespace BizDevAgent.Services
{
    public class RepositoryFile
    {
        public string FileName { get; set; }
        public string Contents { get; set; }
    }

    public class GitService : Service
    {
        public async Task<Result<string>> Pull(string localRepoPath)
        {
            await ExecuteGitCommand(@$"git fetch");
            return await ExecuteGitCommand(@$"git pull", localRepoPath);
        }

        public async Task<Result<string>> ApplyDiff(string localRepoPath, string diffFilePath)
        {
            string command = @$"git apply ""{diffFilePath}""";
            return await ExecuteGitCommand(command, localRepoPath);
        }

        public async Task<Result<List<RepositoryFile>>> ListRepositoryFiles(string localRepoPath)
        {
            var result = await ExecuteGitCommand("git ls-files", localRepoPath);
            if (result.IsFailed)
            {
                return Result.Fail<List<RepositoryFile>>(result.Errors[0].Message);
            }

            var output = result.Value;
            var files = new List<RepositoryFile>();
            var isReading = false;
            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("ls-files"))
                    {
                        isReading = true;
                        continue;
                    }

                    if (isReading)
                    {
                        var filePath = Path.Combine(localRepoPath, line);

                        // Check if the path is a file and not a directory
                        if (File.Exists(filePath))
                        {
                            var file = new RepositoryFile
                            {
                                FileName = line,
                                Contents = await File.ReadAllTextAsync(filePath) // Potentially expensive for large repos
                            };
                            files.Add(file);
                        }
                    }
                }
            }

            return Result.Ok(files);
        }

        public async Task<Result<string>> RevertAllChanges(string localRepoPath)
        {
            // First, reset the repository to the last commit's state for tracked files
            var resetResult = await ExecuteGitCommand(@"git reset --hard", localRepoPath);
            if (resetResult.IsFailed)
            {
                return Result.Fail<string>(resetResult.Errors[0].Message);
            }

            // Then, clean the working directory to remove untracked files and directories
            var cleanResult = await ExecuteGitCommand(@"git clean -fd", localRepoPath);
            if (cleanResult.IsFailed)
            {
                return Result.Fail<string>(cleanResult.Errors[0].Message);
            }

            return Result.Ok("All local changes have been reverted successfully.");
        }

        public async Task<Result<string>> CloneRepository(string gitRepoUrl, string localRepoPath)
        {
            Paths.EnsureDirectoryExists(localRepoPath);

            string command = @$"git clone ""{gitRepoUrl}"" ""{localRepoPath}""";
            // For clone, working directory should be the parent of localRepoPath or a generic temporary directory if localRepoPath doesn't exist yet
            string workingDirectory = Directory.Exists(localRepoPath) ? localRepoPath : Path.GetTempPath();
            var result = await ExecuteGitCommand(command, workingDirectory);
            return result;
        }

        private Task<Result<string>> ExecuteGitCommand(string command, string workingDirectory = null)
        {
            string tempBatchFilePath = Path.GetTempFileName() + ".bat";
            string batchCommands = command;

            File.WriteAllText(tempBatchFilePath, batchCommands);

            var startInfo = new ProcessStartInfo("cmd.exe", $"/c \"{tempBatchFilePath}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Paths.GetSourceControlRootPath(),
            };

            StringBuilder outputBuilder = new StringBuilder();
            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                // Asynchronously read the standard output of the spawned process.
                process.BeginOutputReadLine();
                process.OutputDataReceived += (sender, args) =>
                {
                    outputBuilder.AppendLine(args.Data); // Capture the output in a string
                };

                // Asynchronously read the standard error of the spawned process.
                process.BeginErrorReadLine();
                process.ErrorDataReceived += (sender, args) =>
                {
                    outputBuilder.AppendLine(args.Data); // Also capture the error output
                };

                process.WaitForExit();

                // Check the exit code to determine success or failure
                if (process.ExitCode != 0)
                {
                    return Task.FromResult(Result.Fail<string>(outputBuilder.ToString()));
                }
            }

            File.Delete(tempBatchFilePath);

            // On success, return the captured output
            return Task.FromResult(Result.Ok(outputBuilder.ToString()));
        }
    }
}
