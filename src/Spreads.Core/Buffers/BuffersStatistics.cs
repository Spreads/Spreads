using Spreads.Threading;

namespace Spreads.Buffers
{
    internal static class BuffersStatistics
    {
        internal static PerCoreCounter AllocatedNativeMemory = new();
        internal static PerCoreCounter ReleasedNativeMemory = new();
        internal static PerCoreCounter RentReturnedBeforeYield = new();
        internal static PerCoreCounter ReturnReturnedBeforeYield = new();
        internal static PerCoreCounter SameCoreRentContention = new();
        internal static PerCoreCounter SameCoreReturnContention = new();

        internal static PerCoreCounter RentLoop = new();
        internal static PerCoreCounter ReturnLoop = new();
    }
}
