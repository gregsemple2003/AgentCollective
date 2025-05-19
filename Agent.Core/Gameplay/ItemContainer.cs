namespace Agent.Core.Gameplay
{
    /// <summary>
    /// Simple implementation of <see cref="ListContainer{T}"/> representing a list of items.
    /// </summary>
    public class ItemContainer<T> : ListContainer<T>
    {
        public ItemContainer(string slotTemplate = "item") : base(slotTemplate)
        {
        }

        public void Add(T item) => Items.Add(item);
    }
}
