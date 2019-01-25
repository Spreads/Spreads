namespace Spreads.Collections.Experimental
{
    // Rewrite of initial (and battle-tested) F# version using
    // Memory<T> instead of arrays as the backing

    public class Matrix : BaseContainer<long>
    {
    }

    public class Matrix<T> : Matrix
    {
    }

    public class AppendMatrix : Matrix
    {
    }

    public class AppendMatrix<T> : Matrix<T>
    {
    }

    public class MutableMatrix : Matrix
    {
    }

    public class MutableMatrix<T> : Matrix<T>
    {
    }

}
