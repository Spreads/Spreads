// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;

namespace Bootstrap
{
    // TODO internal
    internal class NativeLibrary : IDisposable
    {
        public string path;
        public IntPtr handle;
        public readonly INativeLibraryLoader loader;

        public NativeLibrary(string path, INativeLibraryLoader loader)
        {
            this.path = path;
            this.loader = loader;
            this.handle = loader.LoadLibrary(path);
            if (this.handle == IntPtr.Zero)
            {
                Trace.TraceError("NativeLibrary handle == IntPtr.Zero");
                throw new DllNotFoundException(path);
            }
        }

        ~NativeLibrary()
        {
            Dispose(false);
        }

        public Delegate GetFunction(string name, Type type)
        {
            IntPtr function = loader.FindFunction(this.handle, name);
            if (function == IntPtr.Zero)
#if NET451
                throw new EntryPointNotFoundException(name);
#else
                throw new Exception(name);
#endif
            return System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(function, type);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (this.handle != IntPtr.Zero)
            {
                this.loader.UnloadLibrary(this.handle);
                this.handle = IntPtr.Zero;
            }
            if (this.path != null)
            {
                try
                {
                    System.IO.File.Delete(path);
                }
                catch
                {
                }
                this.path = null;
            }
        }
    }
}