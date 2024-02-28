namespace Agent.Tests
{
    public class Paths
    {
        public static string GetTestDataFolder()
        {
            return Path.Combine(Agent.Utilities.Paths.GetProjectPath(), "Data");
        }
    }
}
