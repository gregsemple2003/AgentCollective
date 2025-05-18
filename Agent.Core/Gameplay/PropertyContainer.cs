using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Agent.Core.Gameplay
{
    /// <summary>
    /// Generic base container that exposes all properties decorated with
    /// <see cref="ContainerPropertyAttribute"/> as enumerable slot/value pairs.
    /// </summary>
    public interface IContainer<T> : IEnumerable<(ISlotDescriptor Slot, T Value)>
    {
    }

    /// <summary>
    /// Generic base container that exposes all properties decorated with
    /// <see cref="ContainerPropertyAttribute"/> as enumerable slot/value pairs.
    /// </summary>
    public abstract class PropertyContainer<T> : IContainer<T>
    {
        private readonly (ISlotDescriptor Slot, PropertyInfo Property)[] _layout;

        protected PropertyContainer()
        {
            _layout = GetLayout(GetType());
        }

        private static readonly ConcurrentDictionary<Type, (ISlotDescriptor Slot, PropertyInfo Property)[]> _layouts = new();

        private static (ISlotDescriptor Slot, PropertyInfo Property)[] GetLayout(Type type)
        {
            return _layouts.GetOrAdd(type, t =>
            {
                var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                var list = new List<(ISlotDescriptor Slot, PropertyInfo Property)>();
                foreach (var prop in props)
                {
                    var attr = prop.GetCustomAttribute<ContainerPropertyAttribute>();
                    if (attr != null && prop.PropertyType == typeof(T))
                    {
                        list.Add((attr.Slot, prop));
                    }
                }

                return list.ToArray();
            });
        }

        public Enumerator GetEnumerator() => new Enumerator(this, _layout);

        IEnumerator<(ISlotDescriptor Slot, T Value)> IEnumerable<(ISlotDescriptor Slot, T Value)>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<(ISlotDescriptor Slot, T Value)>
        {
            private readonly PropertyContainer<T> _owner;
            private readonly (ISlotDescriptor Slot, PropertyInfo Property)[] _layout;
            private int _index;

            internal Enumerator(PropertyContainer<T> owner, (ISlotDescriptor Slot, PropertyInfo Property)[] layout)
            {
                _owner = owner;
                _layout = layout;
                _index = -1;
            }

            public (ISlotDescriptor Slot, T Value) Current
            {
                get
                {
                    var pair = _layout[_index];
                    var value = (T)(pair.Property.GetValue(_owner) ?? default(T));
                    return (pair.Slot, value);
                }
            }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                int next = _index + 1;
                if (next < _layout.Length)
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
