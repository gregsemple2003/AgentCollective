namespace Agent.Tests
{
    public class Paths
    {
        public static string GetTestDataFolder()
        {
            return Path.Combine(Agent.Core.Paths.GetProjectPath(), "Data");
        }
    }
}
