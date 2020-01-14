using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Threading
{
    [StructLayout(LayoutKind.Explicit)]
    public class ReaderWriterSpinLock
    {
        [FieldOffset(0)]
        private int _value;
        [FieldOffset(0)]
        private int _version;
        [FieldOffset(4)]
        private int _nextVersion;

        public int Version => Volatile.Read(ref _version);

        public int NextVersion => Volatile.Read(ref _nextVersion);

        public void TryAcquireLock()
        {
            // if version == next version try cas for some time
            // 
        }

    }
}