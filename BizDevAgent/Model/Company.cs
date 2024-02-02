namespace BizDevAgent.Model
{
    public class Company
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Type { get; set; }
        public string Location { get; set; }
        public List<string> Emails { get; set; }
        public string LinkedInUrl { get; set; }
        public string LinkedInFounderUrl { get; set; } // likely ceo of company, or founder
        public List<string> Tags { get; set; } = new List<string>();
        public int Index { get; set; }
    }
}
