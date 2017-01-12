// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace Spreads.Buffers {

    internal sealed class SafeBufferStream : UnmanagedMemoryStream {

        [SecurityCritical]
        internal SafeBufferStream(SafeBuffer buffer, long offset, long length, bool readOnly) {
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