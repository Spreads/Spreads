using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace Spreads.Serialization
{
    internal sealed class SafeBufferAccessor : UnmanagedMemoryAccessor {
        [SecurityCritical]
        internal SafeBufferAccessor(SafeBuffer buffer, long offset, long length, bool readOnly) {
            Debug.Assert(buffer != null, "buffer is null");
            Initialize(buffer, offset, length, readOnly ? FileAccess.Read : FileAccess.ReadWrite);
        }

        protected override void Dispose(bool disposing) {
            try {
                // todo?
            } finally {
                base.Dispose(disposing);
            }
        }
    }
}