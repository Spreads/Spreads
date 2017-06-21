namespace Spreads.DataTypes
{
    /// <summary>
    /// Count and sum.
    /// </summary>
    public struct Stat1<TKey>
    {
        internal double _sum;
        internal int _count;

        public int Count => _count;
        public double Sum => _sum;
    }
}