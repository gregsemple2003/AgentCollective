using System.Xml.Linq;

namespace BizDevAgent.Agents
{
    public class ProjectFile
    { 
        public string FileName { get; set; }
        public string Contents {  get; set; }
    }

    public class VisualStudioAgent : Agent
    {
        public Task<List<ProjectFile>> LoadProjectFiles(string projectFilePath)
        {
            // Load the .csproj file
            var results = new List<ProjectFile>();
            XDocument csprojDocument = XDocument.Load(projectFilePath);

            // Define the namespace to access the elements
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

            // Iterate over all 'Compile', 'Content', and 'None' included files
            var includedFiles = csprojDocument
                .Element(msbuild + "Project")
                ?.Elements(msbuild + "ItemGroup")
                .SelectMany(itemGroup => itemGroup.Elements())
                .Where(el => el.Name.LocalName == "Compile" || el.Name.LocalName == "Content" || el.Name.LocalName == "None")
                .Select(el => el.Attribute("Include")?.Value);

            // Perform an action with each included file path
            foreach (var filePath in includedFiles)
            {
                Console.WriteLine(filePath);
            }

            return Task.FromResult(results);
        }
    }
}
