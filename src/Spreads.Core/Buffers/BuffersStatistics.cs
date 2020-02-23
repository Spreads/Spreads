using Spreads.Threading;

namespace Spreads.Buffers
{
    internal static class BuffersStatistics
    {
        internal static PerCoreCounter AllocatedNativeMemory = new PerCoreCounter();
        internal static PerCoreCounter ReleasedNativeMemory = new PerCoreCounter();
    }
}