// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;

namespace Spreads.Utils.Bootstrap
{
    public class NativeLibrary : IDisposable
    {
        internal string _path;
        internal IntPtr _handle;
        internal readonly INativeLibraryLoader _loader;

        internal NativeLibrary(string path, INativeLibraryLoader loader)
        {
            _path = path;
            _loader = loader;
            _handle = loader.LoadLibrary(path);
            if (_handle == IntPtr.Zero)
            {
                Trace.TraceError("NativeLibrary handle == IntPtr.Zero");
                throw new DllNotFoundException(path);
            }
        }

        public string Path => _path;
        public IntPtr Handle => _handle;

        [Obsolete("Use generic overload instead")]
        public Delegate GetFunction(string name, Type type)
        {
            IntPtr function = _loader.FindFunction(_handle, name);
            if (function == IntPtr.Zero)
#if NET451
                throw new EntryPointNotFoundException(name);
#else
                throw new Exception(name);
#endif
            return System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(function, type);
        }

        public TDelegate GetFunction<TDelegate>(string name)
        {
            IntPtr function = _loader.FindFunction(_handle, name);
            if (function == IntPtr.Zero)
#if NET451
                throw new EntryPointNotFoundException(name);
#else
                throw new Exception(name);
#endif
            return System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<TDelegate>(function);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (this._handle != IntPtr.Zero)
            {
                this._loader.UnloadLibrary(this._handle);
                this._handle = IntPtr.Zero;
            }
            if (this._path != null)
            {
                try
                {
                    System.IO.File.Delete(_path);
                }
                catch
                {
                }
                this._path = null;
            }
        }

        ~NativeLibrary()
        {
            Dispose(false);
        }
    }
}