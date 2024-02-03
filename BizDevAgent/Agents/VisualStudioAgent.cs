using System.Xml.Linq;

namespace BizDevAgent.Agents
{
    public class ProjectFile
    {
        public string FileName { get; set; }
        public string Contents { get; set; }
    }

    public class VisualStudioAgent : Agent
    {
        public Task<List<ProjectFile>> LoadProjectFiles(string csprojPath)
        {
            var results = new List<ProjectFile>();

            // In .NET 5 and beyond, all source files are implicitly contained in the project folder recursively
            // without needing to explicitly mention each file.
            var csFiles = Directory.GetFiles(csprojPath, "*.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                var normalizedPath = Path.GetFullPath(file);
                Console.WriteLine(normalizedPath); // Or any operation you want to perform with the file path
                var contents = File.ReadAllText(normalizedPath);
                results.Add(new ProjectFile { FileName = normalizedPath, Contents = contents});
            }

            return Task.FromResult(results);
        }
    }
}