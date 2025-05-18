using System;
using Agent.Core.Gameplay;

namespace Agent.Tests
{
    [TestFixture]
    public class StatsContainerTests
    {
        [Test]
        public void IterateStats_PrintsNameAndValue()
        {
            var container = new StatsContainer
            {
                Strength = 5,
                Intelligence = 7,
                Dexterity = 9
            };
            string[] names = { "strength", "intelligence", "dexterity" };
            float[] values = { 5f, 7f, 9f };
            int index = 0;
            foreach (var (slot, value) in container)
            {
                Assert.AreEqual(names[index], slot.Name);
                Assert.AreEqual(values[index], value);
                index++;
            }
            Assert.AreEqual(names.Length, index);
        }

        [Test]
        public void IterateBothContainers_ViaInterface()
        {
            var stats = new StatsContainer
            {
                Strength = 1,
                Intelligence = 2,
                Dexterity = 3
            };

            var items = new ItemContainer<string>("item")
            {
            };
            items.Add("sword");
            items.Add("shield");

            IContainer<float> statsContainer = stats;
            IContainer<string> itemContainer = items;

            string[] statNames = { "strength", "intelligence", "dexterity" };
            float[] statValues = { 1f, 2f, 3f };
            int idx = 0;
            foreach (var (slot, value) in statsContainer)
            {
                Assert.AreEqual(statNames[idx], slot.Name);
                Assert.AreEqual(statValues[idx], value);
                idx++;
            }
            Assert.AreEqual(statNames.Length, idx);

            string[] itemNames = { "item0", "item1" };
            string[] itemValues = { "sword", "shield" };
            idx = 0;
            foreach (var (slot, value) in itemContainer)
            {
                Assert.AreEqual(itemNames[idx], slot.Name);
                Assert.AreEqual(itemValues[idx], value);
                idx++;
            }
            Assert.AreEqual(itemNames.Length, idx);
        }
    }
}
