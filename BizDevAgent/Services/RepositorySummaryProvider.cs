using System.Text;
using BizDevAgent.DataStore;
using Newtonsoft.Json;

namespace BizDevAgent.Services
{
    public enum RepositoryNodeType
    {
        File,
        Directory
    }

    [TypeId("RepositoryNode")]
    [EntityReferenceType]
    public class RepositoryNode : JsonAsset
    {
        public string Name { get; set; }
        public RepositoryNodeType Type { get; set; }
        [JsonIgnore]
        public RepositoryNode Parent { get; set; }
        [JsonIgnore]
        public List<RepositoryNode> Children { get; set; } = new List<RepositoryNode>();
        public List<string> ChildKeys { get; set; } = new List<string>();
        public int TotalFiles => Type == RepositoryNodeType.Directory ? Children.Sum(child => child.TotalFiles) : 1;
        public string Summary { get; set; }
        [JsonIgnore]
        public RepositoryFile File { get; set; }

        // Constructor for the node
        public RepositoryNode(string name, RepositoryNodeType type, RepositoryNode parent = null)
        {
            Name = name;
            Type = type;
            Parent = parent;
        }

        // Method to add a child node to this node
        public void AddChild(RepositoryNode child)
        {
            Children.Add(child);
            child.Parent = this;
        }

        public int CountTotalNodes()
        {
            int count = 1; // Count this node
            foreach (var child in Children)
            {
                count += child.CountTotalNodes(); // Recursively count the nodes in each child
            }
            return count;
        }

        public string GetFullPath()
        {
            var parts = new List<string>();
            var currentNode = this; // Start with the current node

            while (currentNode != null)
            {
                // Only add the node's name to the parts list if it is not an empty string
                if (!string.IsNullOrEmpty(currentNode.Name))
                {
                    parts.Add(currentNode.Name);
                }
                currentNode = currentNode.Parent; // Move up to the parent node
            }

            parts.Reverse(); // Reverse the list to get the correct order from root to the current node

            // If the parts list is empty (which shouldn't happen unless the tree is improperly constructed),
            // or the first element is not the root (empty string), return a path that starts without a slash.
            // Otherwise, join the parts with slashes.
            var path = parts.Count > 0 ? string.Join("/", parts) : "";

            return path;
        }

        public async Task BuildSummary(LanguageModelService languageModelService, RepositorySummaryDataStore repositorySummaryDataStore, string localRepoPath)
        {
            // Build summary for children
            foreach (var child in Children)
            {
                // TODO gsemple: parallelize
                await child.BuildSummary(languageModelService, repositorySummaryDataStore, localRepoPath);
            }

            Key = Path.Combine(localRepoPath, GetFullPath());
            ChildKeys = Children.Select(node => node.Key).ToList();

            if (Type == RepositoryNodeType.File) 
            {
                // Build summary for self
                Console.WriteLine($"Summarizing file: {File.FileName}");

                string fileSummaryPrompt = $"Summarize this file '{File.FileName}' in 1-3 sentences (60 words max):\n\n{File.Contents}";
                var chatModel = languageModelService.GetLowTierModel();
                var chatResult = await languageModelService.ChatCompletion(fileSummaryPrompt, modelOverride: chatModel);
                Summary = $"{GetFullPath()}: {chatResult.ChatResult.ToString()}";
            }
            else if (Type == RepositoryNodeType.Directory)
            {
                // Summarize immediate children
                var path = GetFullPath();
                Console.WriteLine($"Summarizing directory: {path}");

                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine($"Summarize this directory '{path}' in 1-3 sentences (60 words max):\n\n");
                foreach (var child in Children)
                {
                    promptBuilder.AppendLine(child.Summary);
                }
                
                var chatModel = languageModelService.GetLowTierModel();
                var chatResult = await languageModelService.ChatCompletion(promptBuilder.ToString(), modelOverride: chatModel);
                var childRelativePaths = ChildKeys.Select(key => Path.GetRelativePath(localRepoPath, key)).ToList();
                Summary = $"{GetFullPath()}: {chatResult.ChatResult.ToString()}";
                Summary += $"  Direct child files or directories: {string.Join(", ", childRelativePaths)}.";
            }

            repositorySummaryDataStore.Add(this, shouldOverwrite: true);
        }
    }

    public class RepositorySummaryProvider
    {
        private readonly RepositoryQuerySession _repositoryQuerySession;
        private readonly LanguageModelService _languageModelService;
        private readonly RepositorySummaryDataStore _repositorySummaryDataStore;

        public RepositorySummaryProvider(RepositoryQuerySession repositoryQuerySession, RepositorySummaryDataStore repositorySummaryDataStore, LanguageModelService languageModelService)
        {
            _repositoryQuerySession = repositoryQuerySession;
            _repositorySummaryDataStore = repositorySummaryDataStore;
            _languageModelService = languageModelService;
        }

        public async Task Refresh()
        {
            var root = await BuildRepositoryTree();
            var rootNodeCount = root.CountTotalNodes();
            await root.BuildSummary(_languageModelService, _repositorySummaryDataStore, _repositoryQuerySession.LocalRepoPath);
        }

        internal async Task<RepositoryNode> BuildRepositoryTree()
        {
            var root = new RepositoryNode("", RepositoryNodeType.Directory); // Assume root is the base directory
            var repositoryFiles = _repositoryQuerySession.GetAllRepoFiles();

            foreach (var repoFile in repositoryFiles)
            {
                // Split the file path into parts
                var parts = repoFile.FileName.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                AddFileToTree(root, parts, 0, repoFile); 
            }

            return root;
        }

        private void AddFileToTree(RepositoryNode currentNode, string[] parts, int index, RepositoryFile repoFile)
        {
            if (index >= parts.Length) return; // Base case: if index exceeds parts length

            var part = parts[index];
            var isFile = index == parts.Length - 1; // Check if the current part is a file (last part of the path)
            var existingChild = currentNode.Children.FirstOrDefault(c => c.Name == part);

            if (existingChild == null)
            {
                // If the child does not exist, create it and add it to the current node
                var newNode = new RepositoryNode(part, isFile ? RepositoryNodeType.File : RepositoryNodeType.Directory, currentNode);

                if (isFile)
                {
                    // If this node is a file, set its File property to the current repoFile
                    newNode.File = repoFile;
                }

                currentNode.AddChild(newNode);
                existingChild = newNode;
            }

            if (!isFile)
            {
                // If it's not a file, recursively add the rest of the path
                AddFileToTree(existingChild, parts, index + 1, repoFile);
            }
        }
    }
}
