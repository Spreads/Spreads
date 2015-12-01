namespace Spreads.Experimental
{
    /// <summary>
    /// Direction to lookup data or move from a starting point
    /// </summary>
    public enum Lookup
    {
        /// <summary>
        /// Less than, excludes a strating point even if it is present
        /// </summary>
        LT,
        /// <summary>
        /// Less or equal
        /// </summary>
        LE,
        /// <summary>
        /// Equal
        /// </summary>
        EQ,
        /// <summary>
        /// Greater or equal
        /// </summary>
        GE,
        /// <summary>
        /// Greater than, excludes a strating point even if it is present
        /// </summary>
        GT
    }
}