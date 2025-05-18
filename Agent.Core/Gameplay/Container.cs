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
    public abstract class Container<T> : IEnumerable<(ISlotDescriptor Slot, T Value)>
    {
        private readonly (ISlotDescriptor Slot, PropertyInfo Property)[] _layout;

        protected Container()
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

        public IEnumerator<(ISlotDescriptor Slot, T Value)> GetEnumerator()
        {
            foreach (var (slot, property) in _layout)
            {
                var value = (T)(property.GetValue(this) ?? default(T));
                yield return (slot, value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
