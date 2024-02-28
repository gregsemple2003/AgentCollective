using Agent.Services;

namespace Agent.Tests
{
    [TestFixture]
    public class DiffTests
    {
        /// <summary>
        /// Our fixing routines that are intended to heal GPT's mistakes should not fix .diffs generated directly from git.
        /// </summary>
        [Test]
        public void Patch_ShouldNotModifyGitDiff()
        {
            TestApplyDiffPatch("FileDataStore.txt", "actual_double_replace2.diff", out var actualDiffFileContents, out var fixedDiffFileContents);

            Assert.AreEqual(actualDiffFileContents, fixedDiffFileContents);
        }

        [Test]
        public void Patch_CustomShouldApply()
        {
            TestApplyCustomPatch("FileDataStore.txt", "gpt_patch.txt", out var actualDiffFileContents, out var modifiedFileContents);

            //Assert.AreEqual(actualDiffFileContents, fixedDiffFileContents);
        }

        private void TestApplyDiffPatch(string fileName, string diffFileName, out string actualDiffFileContents, out string fixedDiffFileContents)
        {
            var originalFilePath = Path.Combine(Paths.GetTestDataFolder(), fileName);
            var originalFileContents = File.ReadAllText(originalFilePath);
            var diffFilePath = Path.Combine(Paths.GetTestDataFolder(), diffFileName);
            actualDiffFileContents = File.ReadAllText(diffFilePath);

            fixedDiffFileContents = DiffUtils.FixDiffPatch(actualDiffFileContents, originalFileContents);
            //var modifiedFileContents = DiffUtils.ApplyPatch(fixedDiffFileContents, originalFileContents);
        }

        private void TestApplyCustomPatch(string fileName, string diffFileName, out string actualDiffFileContents, out string modifiedFileContents)
        {
            var originalFilePath = Path.Combine(Paths.GetTestDataFolder(), fileName);
            var originalFileContents = File.ReadAllText(originalFilePath);
            var diffFilePath = Path.Combine(Paths.GetTestDataFolder(), diffFileName);
            actualDiffFileContents = File.ReadAllText(diffFilePath);

            modifiedFileContents = DiffUtils.ApplyCustomPatch(actualDiffFileContents, originalFileContents);
        }
    }
}
