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

        public static string GetProjectPath()
        {
            return Path.Combine(Environment.CurrentDirectory, "..", "..", "..");
        }

        public static string GetSourceControlRootPath()
        {
            return Path.Combine(Paths.GetProjectPath(), "..");
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
