using System;
using System.Collections.Generic;
using System.Linq;

public class Hunk
{
	public List<string> OriginalSeq { get; set; } = new List<string>(); // Context + remove lines
	public List<string> NewSeq { get; set; } = new List<string>();      // Context + add lines
}

/*
Patch format uses context lines to locate the change, then + or - to modify.
 @@ @@
 def foo():
     print("Hello")
-    print("World")
+    print("Universe")
 */
public static class PatchUsingContext
{
	public static string ApplyPatch(string originalFile, string patchString)
	{
		// Split the original file into lines
		var originalLines = originalFile.Split('\n').ToList();
		// Parse the patch into hunks
		var hunks = ParsePatch(patchString);
		// Apply the hunks to get the modified lines
		var resultLines = ApplyPatch(originalLines, hunks);
		// Join lines back into a string with Unix-style line endings
		return string.Join("\n", resultLines);
	}

	private static List<Hunk> ParsePatch(string patchString)
	{
		var hunks = new List<Hunk>();
		var lines = patchString.Split('\n').ToList();
		int index = 0;

		while (index < lines.Count)
		{
			if (lines[index].StartsWith("@@ @@"))
			{
				var hunk = new Hunk();
				index++;
				while (index < lines.Count && !lines[index].StartsWith("@@ @@"))
				{
					string line = lines[index];
					if (string.IsNullOrEmpty(line))
					{
						index++;
						continue; // Skip empty lines within hunk
					}

					char prefix = line[0];
					string content = line.Substring(1);

					if (prefix == ' ')
					{
						// Context line goes into both sequences
						hunk.OriginalSeq.Add(content);
						hunk.NewSeq.Add(content);
					}
					else if (prefix == '-')
					{
						// Remove line goes into original sequence
						hunk.OriginalSeq.Add(content);
					}
					else if (prefix == '+')
					{
						// Add line goes into new sequence
						hunk.NewSeq.Add(content);
					}
					else
					{
						throw new Exception($"Invalid line in hunk: {line}");
					}
					index++;
				}
				hunks.Add(hunk);
			}
			else
			{
				index++; // Skip lines outside hunks
			}
		}
		return hunks;
	}

	private static List<string> ApplyPatch(List<string> originalLines, List<Hunk> hunks)
	{
		var result = new List<string>();
		int i = 0; // Current position in originalLines

		foreach (var hunk in hunks)
		{
			// Find where the original sequence matches
			int p = FindSequence(originalLines, hunk.OriginalSeq, i);
			if (p == -1)
			{
				throw new Exception("Hunk context not found in original file");
			}

			// Copy lines before the match
			result.AddRange(originalLines.GetRange(i, p - i));
			// Apply the new sequence
			result.AddRange(hunk.NewSeq);
			// Move position past the matched sequence
			i = p + hunk.OriginalSeq.Count;
		}

		// Append any remaining lines
		result.AddRange(originalLines.GetRange(i, originalLines.Count - i));
		return result;
	}

	private static int FindSequence(List<string> lines, List<string> seq, int start)
	{
		for (int k = start; k <= lines.Count - seq.Count; k++)
		{
			if (lines.Skip(k).Take(seq.Count).SequenceEqual(seq))
			{
				return k;
			}
		}
		return -1; // Not found
	}
}