using System.Collections.Concurrent;

namespace Agent.Core.Gameplay
{
    /// <summary>
    /// Provides cached <see cref="ISlotDescriptor"/> instances for list based containers.
    /// </summary>
    public class ListSlotDescriptorTemplate
    {
        private readonly string _template;
        private readonly ConcurrentDictionary<int, ISlotDescriptor> _cache = new();

        public ListSlotDescriptorTemplate(string template)
        {
            _template = template;
        }

        public ISlotDescriptor GetSlot(int index)
        {
            return _cache.GetOrAdd(index, i => new SlotDescriptor($"{_template}{i}"));
        }
    }
}
