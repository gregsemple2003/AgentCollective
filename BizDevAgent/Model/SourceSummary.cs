namespace BizDevAgent.Model
{
    public class SourceSummary
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string DetailedSummary { get; set; }
        public string BriefSummary { get; set; }
        public List<string> ChildKeys { get; set; }
        public DateTime LastModified { get; set; }
        public int Version { get; set; }
    }
}
