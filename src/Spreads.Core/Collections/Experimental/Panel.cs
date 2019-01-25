namespace Spreads.Collections.Experimental
{
    // Rewrite of initial (and battle-tested) F# version using
    // Memory<T> instead of arrays as the backing

    public static class Panel
    {
    }

    public class Panel<TRow, TColumn>
    {
    }

    public sealed class Panel<TRow, TColumn, TValue> : Panel<TRow, TColumn>
    {
    }
}
