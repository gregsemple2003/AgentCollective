namespace BizDevAgent.Model
{
    public class Company
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Type { get; set; }
        public string Location { get; set; }
        public List<string> Emails { get; set; }
        public int Index { get; set; }
    }
}
