namespace Spreads
{
    /// <summary>
    /// The minimal implementation of Option type/
    /// </summary>
    public struct Opt<T>
    {
        /// <summary>
        /// Missing value.
        /// </summary>
        public static readonly Opt<T> Missing = new Opt<T>();

        /// <summary>
        /// Create new optional value with a given present value.
        /// </summary>
        /// <param name="value"></param>
        public Opt(T value)
        {
            HasValue = true;
            Value = value;
        }

        /// <summary>
        /// True if a value is present.
        /// </summary>
        public bool HasValue { get; }

        /// <summary>
        /// Present value.
        /// </summary>
        public T Value { get; }
    }
}