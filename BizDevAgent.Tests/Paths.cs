namespace BizDevAgent.Tests
{
    public class Paths
    {
        public static string GetTestDataFolder()
        {
            return Path.Combine(BizDevAgent.Utilities.Paths.GetProjectPath(), "Data");
        }
    }
}
