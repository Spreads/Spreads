using System;
using System.Runtime.InteropServices;

using static Microsoft.Data.Sqlite.Interop.Constants;

namespace Microsoft.Data.Sqlite.Interop {

    internal class Sqlite3BlobHandle : SafeHandle 
    {
        private Sqlite3BlobHandle() : base(IntPtr.Zero, ownsHandle: true) 
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle() 
        {
            var rc = NativeMethods.sqlite3_blob_close(handle);
            handle = IntPtr.Zero;
            return rc == SQLITE_OK;
        }
    }
}

