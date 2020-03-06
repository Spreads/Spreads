using Spreads.Threading;

namespace Spreads.Buffers
{
    internal static class BuffersStatistics
    {
        internal static PerCoreCounter AllocatedNativeMemory = new PerCoreCounter();
        internal static PerCoreCounter ReleasedNativeMemory = new PerCoreCounter();
        internal static PerCoreCounter RentReturnedBeforeYield = new PerCoreCounter();
        internal static PerCoreCounter ReturnReturnedBeforeYield = new PerCoreCounter();
        internal static PerCoreCounter SameCoreRentContention = new PerCoreCounter();
        internal static PerCoreCounter SameCoreReturnContention = new PerCoreCounter();
        
        internal static PerCoreCounter RentLoop = new PerCoreCounter();
        internal static PerCoreCounter ReturnLoop = new PerCoreCounter();
    }
}