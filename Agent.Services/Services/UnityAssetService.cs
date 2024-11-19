using System;
using System.IO;
using System.Collections.Generic;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Agent.Services
{
    public class UnityAssetService
    {
        private readonly string _classDataPath;
        private readonly string _inputDataPath;
        private readonly string _outputDataPath;

        public UnityAssetService(string classDataPath, string inputDataPath, string outputDataPath)
        {
            // Validate the classdata.tpk path
            if (string.IsNullOrEmpty(classDataPath) || !File.Exists(classDataPath))
            {
                throw new FileNotFoundException("Class data package (classdata.tpk) not found.", classDataPath);
            }

            // Validate the input directory path
            if (string.IsNullOrEmpty(inputDataPath) || !Directory.Exists(inputDataPath))
            {
                throw new DirectoryNotFoundException($"Input data directory not found: {inputDataPath}");
            }

            // Ensure the output directory exists
            if (!Directory.Exists(outputDataPath))
            {
                Directory.CreateDirectory(outputDataPath);
            }

            _classDataPath = classDataPath;
            _inputDataPath = inputDataPath;
            _outputDataPath = outputDataPath;
        }

        public void ProcessAllAssets()
        {
            // Get all .assets files in the input directory recursively
            var assetsFiles = Directory.GetFiles(_inputDataPath, "*.assets", SearchOption.AllDirectories);

            Console.WriteLine($"Found {assetsFiles.Length} .assets files to process.");

            foreach (var inputAssetsFilePath in assetsFiles)
            {
                // Compute the relative path from the input directory
                var relativePath = Path.GetRelativePath(_inputDataPath, inputAssetsFilePath);

                // Compute the corresponding output file path
                var outputAssetsFilePath = Path.Combine(_outputDataPath, relativePath);

                // Ensure that the output directory exists
                var outputDir = Path.GetDirectoryName(outputAssetsFilePath);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                try
                {
                    // Process the assets file
                    RemoveCosmeticAssets(inputAssetsFilePath, outputAssetsFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {inputAssetsFilePath}: {ex.Message}");
                }
            }
        }

        public void RemoveCosmeticAssets(string inputAssetsFilePath, string outputAssetsFilePath)
        {
            if (string.IsNullOrEmpty(inputAssetsFilePath) || !File.Exists(inputAssetsFilePath))
            {
                throw new FileNotFoundException("Assets file not found.", inputAssetsFilePath);
            }

            // Initialize AssetsManager
            AssetsManager assetsManager = new AssetsManager();

            // Load the class package (classdata.tpk)
            assetsManager.LoadClassPackage(_classDataPath);

            // Load the assets file
            AssetsFileInstance assetsFileInstance = assetsManager.LoadAssetsFile(inputAssetsFilePath, true);

            // Load the appropriate class database from the package
            assetsManager.LoadClassDatabaseFromPackage(assetsFileInstance.file.Metadata.UnityVersion);

            // Define the asset types to remove
            var typesToRemove = new HashSet<AssetClassID>
            {
                AssetClassID.ParticleSystem,
                AssetClassID.Mesh,
                AssetClassID.SkinnedMeshRenderer,
                AssetClassID.Animator
            };

            // List to hold the assets to remove
            var assetsToRemove = new List<AssetFileInfo>();

            // Get the assets file
            AssetsFile assetsFile = assetsFileInstance.file;

            // Collect assets to remove
            foreach (var typeId in typesToRemove)
            {
                var assetInfos = assetsFile.GetAssetsOfType(typeId);
                assetsToRemove.AddRange(assetInfos);
            }

            Console.WriteLine($"Found {assetsToRemove.Count} cosmetic assets to remove in {inputAssetsFilePath}.");

            // Remove the assets
            foreach (var assetInfo in assetsToRemove)
            {
                assetsFile.Metadata.RemoveAssetInfo(assetInfo);
            }

            // Save the modified assets file
            using (var assetsFileWriter = new AssetsFileWriter(outputAssetsFilePath))
            {
                assetsFile.Write(assetsFileWriter);
            }

            Console.WriteLine($"Processed {inputAssetsFilePath} and saved to {outputAssetsFilePath}.");
        }
    }
}
