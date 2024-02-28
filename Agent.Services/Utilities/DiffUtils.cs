using AngleSharp.Dom;
using Agent.Services;
using FluentResults;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Differencing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;

namespace Agent.Services
{
    public class RepositoryFilePatch
    {
        public string FileName { get; set; }
        public string Patch { get; set; }
        public bool IsNewFile {  get; set; }
    }

    public class DiffUtils
    {
        public static string RemoveChunkMarkers(string contents)
        {
            // Define the pattern to match "// CHUNK <n>" lines
            string pattern = @"// CHUNK \d+\s*\r?\n";
            // Replace all occurrences of the pattern with an empty string
            string cleanedContents = Regex.Replace(contents, pattern, "");
            return cleanedContents;
        }

        public static string AddChunkMarkers(string code, int chunkSize)
        {
            // Split the input code into lines
            string[] lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            StringBuilder modifiedCode = new StringBuilder();
            int chunkCounter = 1; // Start chunk numbering from 1

            // Insert the first chunk marker before any code
            modifiedCode.AppendLine($"// CHUNK {chunkCounter}");

            for (int i = 0; i < lines.Length; i++)
            {
                // Add the line to the modified code
                modifiedCode.AppendLine(lines[i]);

                // Check if it's time to insert a new chunk marker
                // We also check if we're not at the end of the file to avoid adding a chunk marker at the very end
                if ((i + 1) % chunkSize == 0 && (i + 1) < lines.Length)
                {
                    modifiedCode.AppendLine($"// CHUNK {++chunkCounter}");
                }
            }

            return modifiedCode.ToString();
        }

        /// <summary>
        /// Given a file that's already been divided into chunks via "// CHUNK x" comments, apply corrected chunks.
        /// </summary>
        public static string ApplyPatchWithChunks(string contents, List<string> correctedChunks)
        {
            string pattern = @"(// CHUNK \d+[\s\S]*?)(?=// CHUNK \d+|$)";
            var matches = Regex.Matches(contents, pattern);
            Dictionary<int, string> originalChunks = new Dictionary<int, string>();

            // Extract original chunks and index them
            foreach (Match match in matches)
            {
                string chunkHeader = match.Value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                int chunkNumber = int.Parse(Regex.Match(chunkHeader, @"\d+").Value);
                originalChunks[chunkNumber] = match.Value;
            }

            // Replace original chunks with corrected ones
            foreach (string correctedChunk in correctedChunks)
            {
                string correctedChunkHeader = correctedChunk.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                int correctedChunkNumber = int.Parse(Regex.Match(correctedChunkHeader, @"\d+").Value);

                if (originalChunks.ContainsKey(correctedChunkNumber))
                {
                    originalChunks[correctedChunkNumber] = correctedChunk;
                }
                else
                {
                    // Handle the case where a new chunk is introduced or there's an error in chunk numbering
                    Console.WriteLine($"Warning: Corrected chunk {correctedChunkNumber} does not exist in original content and will be ignored.");
                }
            }

            // Reconstruct the content with corrected chunks
            string patchedContent = "";
            foreach (var chunk in originalChunks)
            {
                patchedContent += chunk.Value + Environment.NewLine;
            }

            return patchedContent;
        }

        /// <summary>
        /// Given a text response from an LLM containing file patches, parse them into a list of patches.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static List<RepositoryFilePatch> ParseCustomPatches(string input)
        {
            var patches = new List<RepositoryFilePatch>();

            // Handle both PATCHED and ADDED blocks
            var patchBlocks = Regex.Split(input, @"@(PATCHED|ADDED)\(");

            for (int i = 1; i < patchBlocks.Length; i += 2)
            {
                var patchType = patchBlocks[i];
                var block = patchBlocks[i + 1];

                if (string.IsNullOrWhiteSpace(block))
                    continue;

                var endIndex = block.IndexOf(")");
                if (endIndex > 0)
                {
                    var fileName = block.Substring(0, endIndex);
                    var patchContent = block.Substring(endIndex + 1).Trim();

                    patches.Add(new RepositoryFilePatch
                    {
                        FileName = fileName,
                        Patch = patchContent,
                        IsNewFile = patchType.Equals("ADDED", StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            return patches;
        }

        /// <summary>
        /// Given a diff that indicates a new file should be added, return the contents of the new file.
        /// </summary>
        public static string NewFileCustomPatch(string patch)
        {
            // Split the patch into lines
            var patchLines = patch.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // List to hold new lines or modifications, handling <ADDED> tags
            var newFileLines = new List<string>();

            foreach (var line in patchLines)
            {
                // Check if the line contains <ADDED>, indicating it's part of the new content to be added
                if (line.Contains("<ADDED>"))
                {
                    // Remove all instances of <ADDED> from the line and add it to the new file lines
                    var cleanedLine = Regex.Replace(line, @"\<ADDED\>", "").Trim();
                    newFileLines.Add(cleanedLine);
                }
            }

            // Reconstruct the new file contents without the <ADDED> markers
            return string.Join(Environment.NewLine, newFileLines);
        }

        /// <summary>
        /// As a fallback for when GPT isn't able to express the code changes as a valid .diff file, we revert
        /// to just prompting it for the chunk of code that changes and all line numbers.  This routine applies
        /// the patch of code over top of existing file contents.
        /// </summary>
        public static string ApplyCustomPatch(string patch, string fileContents)
        {
            // Split the file contents and patch into lines
            var fileLines = fileContents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            var patchLines = patch.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // List to hold new lines or modifications including handling for <ADDED> tags
            var newFileLines = new List<string>();

            int currentFileLineIndex = 0;
            foreach (var line in patchLines)
            {
                if (line.EndsWith("<ADDED>"))
                {
                    // Directly add <ADDED> lines to the new file lines
                    newFileLines.Add(line.Replace(" <ADDED>", ""));
                }
                else
                {
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        if (int.TryParse(line.Substring(0, colonIndex), out int lineNumber))
                        {
                            // Adjust for 0-based indexing
                            lineNumber -= 1;

                            // Add lines from the original file up to the current patch line if not already added
                            while (currentFileLineIndex < lineNumber)
                            {
                                newFileLines.Add(fileLines[currentFileLineIndex++]);
                            }

                            // Handle <DELETED> lines by skipping the addition
                            if (!line.EndsWith("<DELETED>"))
                            {
                                newFileLines.Add(line.Substring(colonIndex + 1).TrimStart());
                            }
                            // Increment file line index to skip over the original line being replaced or deleted
                            currentFileLineIndex++;
                        }
                    }
                }
            }

            // Add any remaining lines from the original file
            while (currentFileLineIndex < fileLines.Count)
            {
                newFileLines.Add(fileLines[currentFileLineIndex++]);
            }

            // Reconstruct the file contents
            return string.Join(Environment.NewLine, newFileLines);
        }

        // TODO gsemple: should format according to file specifier or fix gpt4's odd indentation
        public static string FormatRepositoryFile(string fileContents)
        {
            // Parse the code into a syntax tree
            var syntaxTree = CSharpSyntaxTree.ParseText(fileContents);

            // Get the root node of the syntax tree
            var root = syntaxTree.GetRoot();

            // Create a workspace
            var workspace = new AdhocWorkspace();

            // Get the formatting rules for the workspace
            var options = workspace.Options;

            // Format the syntax tree
            var formattedRoot = Formatter.Format(root, workspace, options);

            // Convert the formatted syntax tree back to string
            var formattedCode = formattedRoot.ToFullString();

            return formattedCode;
        }

        /// <summary>
        /// As a fallback when the LLM generates a diff with incorrect line numbers, we attempt to determine the correct line numbers
        /// based on the lines in the original file.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="diff"></param>
        /// <returns></returns>
        public static string ApplyPatch(string diff, string original)
        {
            var lines = original.Split('\n');
            var diffLines = diff.Split('\n');
            var patchedLines = new List<string>(lines);
            int offset = 0;

            foreach (var line in diffLines)
            {
                if (line.StartsWith("@@"))
                {
                    // Example: @@ -92,10 +92,6 @@
                    var match = Regex.Match(line, @"@@ -(\d+),(\d+) \+(\d+),(\d+) @@");
                    if (match.Success)
                    {
                        int startLine = int.Parse(match.Groups[1].Value) - 1; // 0-based index
                        int lineCount = int.Parse(match.Groups[2].Value);
                        int newLineCount = int.Parse(match.Groups[4].Value);
                        int endLine = startLine + lineCount;

                        // Remove lines from original
                        for (int i = startLine; i < endLine; i++)
                        {
                            patchedLines.RemoveAt(startLine + offset);
                        }

                        offset -= lineCount;
                    }
                }
                else if (line.StartsWith("-"))
                {
                    // Lines starting with "-" are already handled in the context block
                    continue;
                }
                else if (line.StartsWith("+"))
                {
                    // Find the insertion point
                    var match = Regex.Match(diffLines[Array.IndexOf(diffLines, line) - 1], @"@@ -(\d+),(\d+) \+(\d+),(\d+) @@");
                    if (match.Success)
                    {
                        int insertAt = int.Parse(match.Groups[3].Value) - 1; // 0-based index
                        patchedLines.Insert(insertAt + offset, line.Substring(1));
                        offset++;
                    }
                }
                else
                {
                    // Context lines or other information - ignore
                }
            }

            return string.Join("\n", patchedLines);
        }

        /// <summary>
        /// Fixup the line numbers in the .diff file, since we can derive this information from looking at the code it is
        /// modifying, as long as the line is relatively unique.  We fail if there is too much ambiguity in the context lines
        /// around the change. GPT4 sometimes hallucinates the line numbers.
        /// </summary>
        public static string FixDiffPatch(string diffFileContents, string originalFileContents)
        {
            string result = string.Empty;
            try
            {
                // Read all lines of the diff file
                var lines = diffFileContents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var originalLines = originalFileContents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                // Regex to match the line count part of a diff header
                Regex headerRegex = new Regex(@"@@ -(\d+),(\d+) \+(\d+),(\d+) @@(.*)", RegexOptions.Compiled);

                bool corrected = false;
                int linesAfterHeaderCount = 0;
                int addedLineCount = 0;
                int removedLineCount = 0;
                int headerLineIndex = -1; // To keep track of where the current header is
                int originalLineStart = 0;
                int originalLineCount = 0;
                int modifiedLineStart = 0;
                int modifiedLineCount = 0;
                string context = string.Empty;


                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    // Count lines after header
                    if (headerLineIndex != -1 && !string.IsNullOrEmpty(line))
                    {
                        linesAfterHeaderCount++;
                    }

                    // Check if the current line is a header line
                    var match = headerRegex.Match(line);
                    if (match.Success)
                    {
                        // Overwrite the header with corrected line starts
                        if (headerLineIndex != -1)
                        {
                            var originalCount = linesAfterHeaderCount - addedLineCount - 1;
                            var modifiedCount = linesAfterHeaderCount - removedLineCount - 1;
                            lines[headerLineIndex] = $"@@ -{originalLineStart},{originalCount} +{modifiedLineStart},{modifiedCount} @@{context}";
                        }

                        // Reset counts
                        corrected = false;
                        addedLineCount = 0;
                        removedLineCount = 0;
                        linesAfterHeaderCount = 0; // Reset actual count for the new section
                        headerLineIndex = i; // Mark the index of the current header
                        context = string.Empty;

                        // Extract numbers from the match and convert them to integers
                        // These are captured in the regex groups based on the parentheses in the regex pattern
                        originalLineStart = int.Parse(match.Groups[1].Value); // Starting line number in the original file
                        originalLineCount = int.Parse(match.Groups[2].Value); // Number of lines in the original file section
                        modifiedLineStart = int.Parse(match.Groups[3].Value); // Starting line number in the modified file
                        modifiedLineCount = int.Parse(match.Groups[4].Value); // Number of lines in the modified file section
                        context = match.Groups[5].Value; // This contains the trailing context text
                    }
                    else if (headerLineIndex != -1 && line.StartsWith("+"))
                    {
                        // Count the lines being added, ignoring headers
                        addedLineCount++;
                    }
                    else if (headerLineIndex != -1 && line.StartsWith("-"))
                    {
                        // Count the lines being added, ignoring headers
                        removedLineCount++;
                    }

                    if (headerLineIndex != -1 && !corrected)
                    {
                        var correctedOriginalLine = FindUniqueOriginalLine(originalLines, headerLineIndex, i, line);
                        if (correctedOriginalLine != -1)
                        {
                            originalLineStart = correctedOriginalLine;

                            corrected = true;
                        }
                    }
                }

                // Overwrite the header with corrected line starts
                if (headerLineIndex != -1)
                {
                    var originalCount = linesAfterHeaderCount - addedLineCount;
                    var modifiedCount = linesAfterHeaderCount - removedLineCount;
                    lines[headerLineIndex] = $"@@ -{originalLineStart},{originalCount} +{modifiedLineStart},{modifiedCount} @@{context}";
                }

                result = String.Join("\n", lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// If this line uniquely identifies a line in the file, we can correct the line number.
        /// Otherwise we should just fail, and fix the case accordingly.
        /// </summary>
        private static int FindUniqueOriginalLine(string[] originalLines, int headerLineIndex, int currentIndex, string line)
        {
            // See if we can uniquely identify the line number in the original file.
            char[] trimChars = { ' ', '\t', '\n', '\r' }; // specify the characters you want to trim
            string trimmed = line.Trim(trimChars);

            int correctedOriginalLine = -1;
            for (int originalIndex = 0; originalIndex < originalLines.Length; ++originalIndex)
            {
                var originalLine = originalLines[originalIndex];
                if (originalLine.Contains(trimmed))
                {
                    // More than one line matches this line, not unique
                    if (correctedOriginalLine != -1)
                    {
                        return -1;
                    }

                    correctedOriginalLine = originalIndex + (currentIndex - headerLineIndex);
                }
            }

            return correctedOriginalLine;
        }

        /// <summary>
        /// Sometimes the LLM doesn't get the line counts right when generating the .diff file.  So we fix that
        /// with this utility method.
        /// </summary>
        public static string FixDiffLineCountsOnAdd(string diffFileContents)
        {
            string result = string.Empty;
            try
            {
                // Read all lines of the diff file
                var lines = diffFileContents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                List<string> correctedLines = new List<string>();

                // Regex to match the line count part of a diff header
                Regex headerRegex = new Regex(@"@@ -(\d+),(\d+) \+(\d+),(\d+) @@", RegexOptions.Compiled);

                int actualCount = 0;
                bool collecting = false;
                int headerLineIndex = -1; // To keep track of where the current header is

                foreach (var line in lines)
                {
                    // Check if the current line is a header line
                    var match = headerRegex.Match(line);
                    if (match.Success)
                    {
                        // If we were already collecting, update the previous header with the actual count
                        if (collecting && headerLineIndex != -1)
                        {
                            correctedLines[headerLineIndex] = UpdateHeaderLineCount(correctedLines[headerLineIndex], actualCount);
                        }

                        collecting = true;
                        actualCount = 0; // Reset actual count for the new section
                        headerLineIndex = correctedLines.Count; // Mark the index of the current header
                        correctedLines.Add(line); // Add the header line now, update it later
                    }
                    else if (collecting && line.StartsWith("+"))
                    {
                        // Count the lines being added, ignoring headers
                        actualCount++;
                        correctedLines.Add(line);
                    }
                    else
                    {
                        correctedLines.Add(line);
                    }
                }

                // Update the last header if there was one
                if (collecting && headerLineIndex != -1)
                {
                    correctedLines[headerLineIndex] = UpdateHeaderLineCount(correctedLines[headerLineIndex], actualCount);
                }

                // Write the corrected lines back to the file
                result = String.Join("\n", correctedLines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            return result;
        }

        private static string UpdateHeaderLineCount(string header, int actualCount)
        {
            return Regex.Replace(header, @"@@ -(\d+),(\d+) \+(\d+),(\d+) @@",
                m => $"@@ -{m.Groups[1].Value},{m.Groups[2].Value} +{m.Groups[3].Value},{actualCount} @@");
        }
    }
}
