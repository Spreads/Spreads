namespace Spreads.Core.Tests
{
    public static class TestUtils
    {
        public static long GetBenchCount(long count = 1_000_000, long debugCount = -1)
        {
#if DEBUG
                if (debugCount <= 0)
                {
                    return 100;
                }

                return debugCount;
#else
            return count;
#endif
        }
    }
}