namespace Agent.Core
{
    public class TextAsset : Asset
    {
        public string Text { get; set; }
    }

    public class TextAssetFactory : IAssetFactory
    {
        public TextAssetFactory()
        {
        }

        public object Create(string filePath)
        {
            using (var reader = File.OpenText(filePath))
            {
                var fileContent = reader.ReadToEnd();
                return new TextAsset { Text = fileContent };
            }
        }
    }
}
