using AngleSharp.Dom;
using BizDevAgent.Goals;
using BizDevAgent.Utilities;
using System.Runtime;

namespace BizDevAgent.DataStore
{
    /// <summary>
    /// An object that is mapped to a data file on disk, kept in source control.
    /// </summary>
    [TypeId("Asset")]
    public class Asset
    {
        internal virtual void PostLoad(AssetDataStore assetDataStore)
        {

        }
    }

    public class AssetDataStore : MultiFileDataStore<Asset>
    {
        public AssetDataStore(string rootPath, IServiceProvider serviceProvider) : base(rootPath, serviceProvider)
        {
            RegisterFactory(".txt", new TextAssetFactory());
            RegisterFactory(".prompt", new PromptAssetFactory());
        }

        /// <summary>
        /// Asset is being loaded as a hard reference, which means a synchronous and immediate load.
        /// </summary>
        public TAsset GetHardRef<TAsset>(string assetName)
            where TAsset : Asset
        {
            return (TAsset)Get(assetName).GetAwaiter().GetResult();
        }

        protected override void PostLoad(Asset asset)
        {
            asset.PostLoad(this);
        }
    }
}
