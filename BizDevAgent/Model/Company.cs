namespace BizDevAgent.Model
{
    public class Company
    {
        public string Name { get; internal set; }
        public string Url { get; internal set; }
        public string Type { get; internal set; }
        public string Location { get; internal set; }
        public List<string> Emails { get; set; }
        public int Index { get; set; }
    }
}
