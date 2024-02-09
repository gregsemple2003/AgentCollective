using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentResults;

namespace BizDevAgent.Agents
{
    public class BuildError : Error
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; } // Added for completeness
        public string ErrorCode { get; set; } // Added to capture the error code
        public string ErrorMessage { get; set; }
        public string RawMessage { get; set; }

        // Constructor for ease of creation
        public BuildError(string filePath, int lineNumber, int columnNumber, string errorCode, string errorMessage, string rawMessage)
        {
            FilePath = filePath;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            RawMessage = rawMessage;
        }
    }

    public class MsBuildErrorParser
    {
        public static List<BuildError> ParseErrors(string output)
        {
            List<BuildError> errors = new List<BuildError>();
            string pattern = @"^(?<filePath>.+)\((?<lineNumber>\d+),(?<columnNumber>\d+)\): error (?<errorCode>[^:]+): (?<errorMessage>.+)$";
            Regex regex = new Regex(pattern);

            string[] lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    errors.Add(new BuildError(
                        match.Groups["filePath"].Value,
                        int.Parse(match.Groups["lineNumber"].Value),
                        int.Parse(match.Groups["columnNumber"].Value),
                        match.Groups["errorCode"].Value,
                        match.Groups["errorMessage"].Value, 
                        line
                    ));
                }
            }

            return errors;
        }
    }

    public class BatchFileBuildAgent : IBuildAgent
    {
        public string ScriptPath { get; set; }

        public BatchFileBuildAgent()
        {
        }

        public async Task<Result<BuildResult>> Build(string rootRepoPath)
        {
            if (string.IsNullOrEmpty(ScriptPath))
            {
                return Result.Fail<BuildResult>("Script path is not set.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{ScriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = rootRepoPath,
            };

            var outputBuilder = new StringBuilder();
            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var errors = MsBuildErrorParser.ParseErrors(outputBuilder.ToString());
                    var errorResult = Result.Fail<BuildResult>($"Build script failed with exit code {process.ExitCode}.");
                    errors.ForEach(error => errorResult.WithError(error));
                    return errorResult;
                }
            }

            var buildResult = new BuildResult { Output = outputBuilder.ToString() };
            return Result.Ok(buildResult);
        }
    }
}
