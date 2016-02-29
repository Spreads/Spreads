
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Data.Sqlite.Interop {

    internal static partial class NativeMethods {

        static NativeMethods()
        {
            // Bootstrapper's static ctor loads native dlls
            Trace.Assert(Bootstrap.Bootstrapper.Instance.BaseFolder.Length > 0);
        }

        public static int sqlite3_blob_bytes(Sqlite3BlobHandle pBlob)
            => Sqlite3.blob_bytes(pBlob);

        public static int sqlite3_blob_close(IntPtr pBlob)
            => Sqlite3.blob_close(pBlob);

        public static int sqlite3_blob_open(Sqlite3Handle ppDb, string db, string table, string column, long iRow,
            int flags, out Sqlite3BlobHandle ppBlob) {
            var zDb = MarshalEx.StringToHGlobalUTF8(db);
            var zTable = MarshalEx.StringToHGlobalUTF8(table);
            var zColumn = MarshalEx.StringToHGlobalUTF8(column);
            try {
                return Sqlite3.blob_open(ppDb, zDb, zTable, zColumn, iRow, flags, out ppBlob);
            } finally {
                if (zDb != IntPtr.Zero) {
                    Marshal.FreeHGlobal(zDb);
                }
                if (zTable != IntPtr.Zero) {
                    Marshal.FreeHGlobal(zTable);
                }
                if (zColumn != IntPtr.Zero) {
                    Marshal.FreeHGlobal(zColumn);
                }
                
            }
        }

        public static int sqlite3_blob_read(Sqlite3BlobHandle pBlob, IntPtr destination, int length, int offset)
            => Sqlite3.blob_read(pBlob, destination, length, offset);

        public static int sqlite3_blob_write(Sqlite3BlobHandle pBlob, IntPtr source, int length, int offset)
            => Sqlite3.blob_write(pBlob, source, length, offset);


        private partial interface ISqlite3 {
            int blob_bytes(Sqlite3BlobHandle pBlob);

            int blob_close(IntPtr pStmt);

            int blob_open(Sqlite3Handle ppDb, IntPtr zDb, IntPtr zTable, IntPtr zColumn, long iRow, int flags,
                out Sqlite3BlobHandle ppBlob);

            int blob_read(Sqlite3BlobHandle pBlob, IntPtr destination, int length, int offset);

            int blob_write(Sqlite3BlobHandle pBlob, IntPtr source, int length, int offset);
        }

        private partial class Sqlite3_sqlite3 : ISqlite3 {

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_bytes(Sqlite3BlobHandle pBlob);

            public int blob_bytes(Sqlite3BlobHandle pBlob)
                => sqlite3_blob_bytes(pBlob);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_close(IntPtr pBlob);

            public int blob_close(IntPtr pBlob)
                => sqlite3_blob_close(pBlob);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_open(Sqlite3Handle pDb, IntPtr zDb, IntPtr zTable, IntPtr zColumn, long iRow, int flags, out Sqlite3BlobHandle ppBlob);

            public int blob_open(Sqlite3Handle ppDb, IntPtr zDb, IntPtr zTable, IntPtr zColumn, long iRow, int flags, out Sqlite3BlobHandle ppBlob)
                => sqlite3_blob_open(ppDb, zDb, zTable, zColumn, iRow, flags, out ppBlob);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_read(Sqlite3BlobHandle pBlob, IntPtr destination, int length, int offset);

            public int blob_read(Sqlite3BlobHandle pBlob, IntPtr destination, int length, int offset)
                => sqlite3_blob_read(pBlob, destination, length, offset);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_write(Sqlite3BlobHandle pBlob, IntPtr source, int length, int offset);

            public int blob_write(Sqlite3BlobHandle pBlob, IntPtr source, int length, int offset)
                => sqlite3_blob_write(pBlob, source, length, offset);

        }


        private partial class Sqlite3_winsqlite3 : ISqlite3 {

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_bytes(Sqlite3BlobHandle pBlob);

            public int blob_bytes(Sqlite3BlobHandle pBlob)
                => sqlite3_blob_bytes(pBlob);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_close(IntPtr pBlob);

            public int blob_close(IntPtr pBlob)
                => sqlite3_blob_close(pBlob);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_open(Sqlite3Handle pDb, IntPtr zDb, IntPtr zTable, IntPtr zColumn, long iRow, int flags, out Sqlite3BlobHandle ppBlob);

            public int blob_open(Sqlite3Handle ppDb, IntPtr zDb, IntPtr zTable, IntPtr zColumn, long iRow, int flags, out Sqlite3BlobHandle ppBlob)
                => sqlite3_blob_open(ppDb, zDb, zTable, zColumn, iRow, flags, out ppBlob);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_read(Sqlite3BlobHandle pBlob, IntPtr destination, int length, int offset);

            public int blob_read(Sqlite3BlobHandle pBlob, IntPtr destination, int length, int offset)
                => sqlite3_blob_read(pBlob, destination, length, offset);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern int sqlite3_blob_write(Sqlite3BlobHandle pBlob, IntPtr source, int length, int offset);

            public int blob_write(Sqlite3BlobHandle pBlob, IntPtr source, int length, int offset)
                => sqlite3_blob_read(pBlob, source, length, offset);

        }
    }

}

