namespace Spreads.Collections.Internal
{
    /// <summary>
    /// Storage for Series, Matrix and DataFrame
    /// </summary>
    internal class DataStorage
    {
        // TODO this object should be pooled

        // for structural sharing no references to this should be exposed outside, only new object (or from pool)

        private VectorStorage _rowIndex;
        private VectorStorage _columnIndex;
        private VectorStorage _values;
        private VectorStorage[] _columns;


        // TODO must be disposable and finalizable, decrement ref counts in fields
    }
}