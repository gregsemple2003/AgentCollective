namespace BizDevAgent.Utilities
{
    public class Paths
    {
        /// <summary>
        /// Storage for DBs, flat json files, etc.
        /// </summary>
        /// <returns></returns>
        public static string GetDataPath()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "BizDevAgent", "Data");
        }

        public static string GetLogPath()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "BizDevAgent", "Logs");
        }

        public static string GetConfigPath()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "BizDevAgent", "Config");
        }

        public static string GetAssetsPath()
        {
            string projectPath = GetProjectPath();
            return Path.Combine(projectPath, "Assets");
        }

        public static string GetProjectPath()
        {
            var path = Path.Combine(Environment.CurrentDirectory, "..", "..", "..");
            return Path.GetFullPath(path);
        }

        public static string GetSourceControlRootPath()
        {
            var path = Path.Combine(Paths.GetProjectPath(), "..");
            return Path.GetFullPath(path);
        }

        public static void EnsureDirectoryExists(string path)
        {
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
        }
    }
}
