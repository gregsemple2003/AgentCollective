using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
