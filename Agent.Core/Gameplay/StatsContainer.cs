namespace Agent.Core.Gameplay
{
    /// <summary>
    /// Simple container of floating point stats.
    /// </summary>
    public class StatsContainer : PropertyContainer<float>
    {
        [ContainerProperty("strength")]
        public float Strength { get; set; }

        [ContainerProperty("intelligence")]
        public float Intelligence { get; set; }

        [ContainerProperty("dexterity")]
        public float Dexterity { get; set; }
    }
}
