namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Pairs an item with a selection weight.
    /// 
    /// Weight is relative, not a percentage. An item with weight 3
    /// is three times as likely to be picked as one with weight 1.
    /// 
    /// Using a struct keeps weighted tables allocation-free when
    /// stored in arrays or lists.
    /// </summary>
    /// <typeparam name="T">Type of item in the table.</typeparam>
    public readonly struct WeightedEntry<T>
    {
        /// <summary>
        /// The item that can be selected.
        /// </summary>
        public readonly T Item;

        /// <summary>
        /// Relative selection weight. Must be > 0.
        /// Higher weight = more likely to be picked.
        /// </summary>
        public readonly float Weight;

        public WeightedEntry(T item, float weight)
        {
            Item = item;
            Weight = weight;
        }

        public override string ToString() => $"{Item} (w:{Weight})";
    }
}
