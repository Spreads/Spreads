namespace Spreads.Collections.Internal.Experimental
{

    public interface ISeries<TKey> // where TKey : IIntDiffable
    {
    }

    public interface ISeries<TKey, TValue> : ISeries<TKey>
    {
    }

    public interface IMatrix : ISeries<long, ISeries<long>>
    {
        IMatrix Transpose();
    }

    public interface IMatrix<T> : IMatrix, ISeries<int, ISeries<int, T>>
    {
        new IMatrix<T> Transpose();
    }

    // Panel is a series of members
    public interface IFrame<TRow, TCol> : ISeries<TRow, ISeries<TCol>>
    {
    }

    public interface IFrame<TRow, TCol, T> : IFrame<TRow, TCol>, ISeries<TRow, ISeries<TCol, T>>
    {
    }

    public interface IPanel<TRow, TCol> : IFrame<TRow, TCol>, 
        ISeries<TRow, ISeries<TCol, ISeries<TRow>>>
    {
    }

    public interface IPanel<TRow, TCol, T> : IFrame<TRow, TCol, T>,
        ISeries<TRow, ISeries<TCol, ISeries<TRow, T>>>
    {
    }

    // Matrix is a series of rows. Matrix could be transposed to get a series of columns.
    public class Matrix : IMatrix
    {
        public IMatrix Transpose()
        {
            throw new System.NotImplementedException();
        }
    }

    public class Matrix<T> : Matrix, IMatrix<T>
    {
        public new IMatrix<T> Transpose()
        {
            throw new System.NotImplementedException();
        }
    }

    public class NDArray<T>
    {
    }

    public class Series
    {
    }

    public class Series<TKey, TValue>
    {
    }

    // DataFrame has row and columns indices
    public class Frame<TRow, TCol> : ISeries<TRow, ISeries<TCol, object>>
    {
    }

    public class Frame<TRow, TCol, T> : Frame<TRow, TCol>, ISeries<TRow, ISeries<TCol, T>>
    {
    }

    //// Series is Nx1 matrix
    //public class Series : DataFrame
    //{
    //}

    //public class Series<TRow, TValue>
    //{
    //}

    //public class DataFrame<TRow, TColumn>
    //{
    //}

    //public class DataFrame<TRow, TColumn, TValue> : DataFrame<TRow, TColumn>
    //{
    //}
}
