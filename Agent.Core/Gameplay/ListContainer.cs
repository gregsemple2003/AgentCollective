using System.Collections;
using System.Collections.Generic;

namespace Agent.Core.Gameplay
{
    /// <summary>
    /// Container backed by a dynamic list of values. Each item is exposed via a
    /// slot name composed from a template and its index.
    /// </summary>
    public abstract class ListContainer<T> : IContainer<T>
    {
        private readonly ListSlotDescriptorTemplate _slotTemplate;
        protected List<T> Items { get; } = new();

        protected ListContainer(string slotTemplate)
        {
            _slotTemplate = new ListSlotDescriptorTemplate(slotTemplate);
        }

        public Enumerator GetEnumerator() => new Enumerator(Items, _slotTemplate);

        IEnumerator<(ISlotDescriptor Slot, T Value)> IEnumerable<(ISlotDescriptor Slot, T Value)>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<(ISlotDescriptor Slot, T Value)>
        {
            private readonly List<T> _items;
            private readonly ListSlotDescriptorTemplate _template;
            private int _index;

            internal Enumerator(List<T> items, ListSlotDescriptorTemplate template)
            {
                _items = items;
                _template = template;
                _index = -1;
            }

            public (ISlotDescriptor Slot, T Value) Current => (_template.GetSlot(_index), _items[_index]);
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                int next = _index + 1;
                if (next < _items.Count)
                {
                    _index = next;
                    return true;
                }
                return false;
            }

            public void Reset() => _index = -1;

            public void Dispose() { }
        }
    }
}
