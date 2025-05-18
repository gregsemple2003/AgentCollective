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

            foreach (var (slot, value) in container)
            {
                Console.WriteLine($"{slot.Name}: {value}");
            }
        }
    }
}
