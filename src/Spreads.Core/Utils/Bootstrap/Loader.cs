// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Spreads.Utils.Bootstrap
{
    // TODO internal
    internal interface INativeLibraryLoader
    {
        IntPtr LoadLibrary(string path);

        bool UnloadLibrary(IntPtr library);

        IntPtr FindFunction(IntPtr library, string function);

        IntPtr LastError();
    }

    internal sealed class WindowsLibraryLoader : INativeLibraryLoader
    {
        IntPtr INativeLibraryLoader.LoadLibrary(string path)
        {
            return WindowsLibraryLoader.LoadLibraryW(path);
        }

        bool INativeLibraryLoader.UnloadLibrary(IntPtr library)
        {
            return WindowsLibraryLoader.FreeLibrary(library);
        }

        IntPtr INativeLibraryLoader.FindFunction(IntPtr library, string function)
        {
            return WindowsLibraryLoader.GetProcAddress(library, function);
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr LoadLibraryW(string path);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr library);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr library, string function);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);

        public IntPtr LastError()
        {
            throw new NotImplementedException();
        }
    }

    internal abstract class UnixLibraryLoader : INativeLibraryLoader
    {
        IntPtr INativeLibraryLoader.LoadLibrary(string path)
        {
            Trace.WriteLine("Opening a library: " + path);
            try
            {
                int flags = GetDLOpenFlags();
                var result = UnixLibraryLoader.dlopen(path, flags);
                Trace.WriteLine("Open result: " + result);
                if (result == IntPtr.Zero)
                {
                    var lastError = dlerror();
                    Trace.WriteLine($"Failed to load native library \"{path}\".\r\nLast Error:{lastError}\r\nCheck inner exception and\\or windows event log.");
                }
                return result;
            }
            catch (Exception ex)
            {
                var lastError = dlerror();
                Trace.WriteLine($"Failed to load native library \"{path}\".\r\nLast Error:{lastError}\r\nCheck inner exception and\\or windows event log.\r\nInner Exception: {ex.ToString()}");

                Trace.WriteLine(ex.ToString());
                return IntPtr.Zero;
            }
        }

        bool INativeLibraryLoader.UnloadLibrary(IntPtr library)
        {
            return UnixLibraryLoader.dlclose(library) == 0;
        }

        IntPtr INativeLibraryLoader.FindFunction(IntPtr library, string function)
        {
            return UnixLibraryLoader.dlsym(library, function);
        }

        protected abstract int GetDLOpenFlags();

        private static IntPtr dlopen(string path, int flags)
        {
            try
            {
                return UnixLibraryLoaderNative.dlopen(path, flags);
            }
            catch
            {
                return UnixLibraryLoaderNative2.dlopen(path, flags);
            }
        }

        private static IntPtr dlsym(IntPtr library, string function)
        {
            try
            {
                return UnixLibraryLoaderNative.dlsym(library, function);
            }
            catch
            {
                return UnixLibraryLoaderNative2.dlsym(library, function);
            }
        }

        private static int dlclose(IntPtr library)
        {
            try
            {
                return UnixLibraryLoaderNative.dlclose(library);
            }
            catch
            {
                return UnixLibraryLoaderNative2.dlclose(library);
            }
        }

        private static IntPtr dlerror()
        {
            try
            {
                return UnixLibraryLoaderNative.dlerror();
            }
            catch
            {
                return UnixLibraryLoaderNative2.dlerror();
            }
        }

        public IntPtr LastError()
        {
            return UnixLibraryLoader.dlerror();
        }

        private static class UnixLibraryLoaderNative
        {
            [DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

            [DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr dlsym(IntPtr library, [MarshalAs(UnmanagedType.LPStr)] string function);

            [DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
            public static extern int dlclose(IntPtr library);

            [DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr dlerror();
        }

        // see https://github.com/dotnet/corefx/issues/17135
        // for FreeBSD may need another option with 'libc'
        private static class UnixLibraryLoaderNative2
        {
            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr dlsym(IntPtr library, [MarshalAs(UnmanagedType.LPStr)] string function);

            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            public static extern int dlclose(IntPtr library);

            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr dlerror();
        }
    }

    internal sealed class AndroidLibraryLoader : UnixLibraryLoader
    {
        protected override int GetDLOpenFlags()
        {
            return RTLD_NOW | RTLD_LOCAL;
        }

        private const int RTLD_NOW = 0x00000000;
        private const int RTLD_LOCAL = 0x00000000;
    }

    internal sealed class LinuxLibraryLoader : UnixLibraryLoader
    {
        protected override int GetDLOpenFlags()
        {
            return RTLD_NOW | RTLD_LOCAL;
        }

        private const int RTLD_NOW = 0x00000002;
        private const int RTLD_LOCAL = 0x00000000;
    }

    internal sealed class OSXLibraryLoader : UnixLibraryLoader
    {
        protected override int GetDLOpenFlags()
        {
            return RTLD_NOW | RTLD_LOCAL;
        }

        private const int RTLD_NOW = 0x00000002;
        private const int RTLD_LOCAL = 0x00000004;
    }

    internal class Loader
    {
        public static NativeLibrary LoadNativeLibrary<T>(string libname)
        {
            ABI abi = Process.DetectABI();
            if (abi.Equals(ABI.Unknown))
                return null;

            INativeLibraryLoader loader = GetNativeLibraryLoader(abi);
            if (loader == null)
                return null;

            string resource = GetNativeLibraryResource(abi, libname);
            if (resource == null)
                return null;

            string path = ExtractNativeResource<T>(resource, abi);
            if (path == null)
                return null;

            return new NativeLibrary(path, loader);
        }

        //internal static Assembly ResolveManagedAssembly(object sender,
        //                                              ResolveEventArgs args) {
        //    var assemblyname = new AssemblyName(args.Name).Name;
        //    var assemblyFileName = Path.Combine(Bootstrapper.Instance.AppFolder, assemblyname + ".dll");
        //    var assembly = Assembly.LoadFrom(assemblyFileName);
        //    return assembly;
        //}

        public static INativeLibraryLoader GetNativeLibraryLoader(ABI abi)
        {
            if (abi.IsWindows())
                return new WindowsLibraryLoader();
            else if (abi.IsLinux())
                return new LinuxLibraryLoader();
            else if (abi.IsOSX())
                return new OSXLibraryLoader();
            else
                return null;
        }

        private static string GetNativeLibraryResource(ABI abi, string libname)
        {
            if (abi.Equals(ABI.Windows_X86))
                return "win.x32." + libname + ".dll";
            else if (abi.Equals(ABI.Windows_X86_64))
                return "win.x64." + libname + ".dll";
            else if (abi.Equals(ABI.OSX_X86))
                return "osx.x32." + libname + ".dylib";
            else if (abi.Equals(ABI.OSX_X86_64))
                return "osx.x64." + libname + ".dylib";
            else if (abi.Equals(ABI.Linux_X86))
                return "lin.x32." + libname + ".so";
            else if (abi.Equals(ABI.Linux_X86_64))
                return "lin.x64." + libname + ".so";
            else if (abi.Equals(ABI.Linux_ARMEL))
                return "lin.armel." + libname + ".so";
            else if (abi.Equals(ABI.Linux_ARMHF))
                return "lin.armhf." + libname + ".so";
            else
                return null;
        }

        private static Stream GetResourceStream<T>(string resource)
        {
            var assembly = typeof(T).GetTypeInfo().Assembly;
            Stream resourceStream;
            try
            {
                resourceStream = assembly.GetManifestResourceStream(resource);
                if (resourceStream == null) throw new Exception();
            }
            catch
            {
                try
                {
                    resourceStream = assembly.GetManifestResourceStream(resource + ".compressed");
                    if (resourceStream == null) throw new Exception();
                }
                catch
                {
                    throw new ArgumentException($"Cannot get resource stream for '{resource}'");
                }
            }
            return resourceStream;
        }

        public static string ExtractNativeResource<T>(string resource, ABI abi)
        {
            var split = resource.Split('.');
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            string path;
            if (!abi.IsWindows())
            {
                // On Windows, LoadLibrary makes it available for DllImport
                // regardless of the location. On Linux, we need to
                // set LD_LIBRARY_PATH which is not possible to do from
                // already running app that needs it. Extracting to the current
                // directory is the simplest thing:
                // - we do not support WoW64-like thing on Linux even if it exists, only 64 bit on Linux so far
                // - location should be writable, while /usr/lib and /usr/local/lib are not writeable
                // - on Windows there was IIS issue - it was reloading every time content changed,
                // that is why we used temp folder.

                // TODO currently need <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
                // otherwise managed dll is in NuGet folder, while native is extracted into local bin
                // folder. This flag is consistent with donet publish behavior that will place all 
                // dlls in one place, so it's OK for now.

                path = Path.Combine(basePath, split[2] + "." + split[3]);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            else
            {
                // On Windows same manages dll could be run from both x32/x64
                // but LoadLibrary makes P/Invoke "just work" (TM)
                basePath = Path.Combine(basePath, split[1]);
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }
                path = Path.Combine(basePath, split[2] + "." + split[3]);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            try
            {
                using (Stream resourceStream = GetResourceStream<T>(resource))
                {
                    using (DeflateStream deflateStream = new DeflateStream(resourceStream, CompressionMode.Decompress))
                    {
                        using (
                            FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write,
                                FileShare.ReadWrite))
                        {
                            byte[] buffer = new byte[1048576];
                            int bytesRead;
                            do
                            {
                                bytesRead = deflateStream.Read(buffer, 0, buffer.Length);
                                if (bytesRead != 0)
                                    fileStream.Write(buffer, 0, bytesRead);
                            }
                            while (bytesRead != 0);
                        }
                    }
                }
                return path;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                File.Delete(path);
                return null;
            }
        }

        public static string ExtractResource<T>(string resource)
        {
            // [os/][arch/]name.extension
            var split = resource.Split('.');
            string path = null;
            if (split.Length == 2)
            {
                // name.extension
                path = Path.Combine(Bootstrapper.Instance.AppFolder, split[0]);
            }
            else if (split.Length == 3 && (split[0].StartsWith("x") || split[0].StartsWith("ar")))
            {
                // arch.name.extension
                path = Path.Combine(Bootstrapper.Instance.AppFolder, split[0], split[1]);
            }
            else if (split.Length == 3)
            {
                // os.name.extension
                path = Path.Combine(Bootstrapper.Instance.AppFolder, split[1]);
            }
            else if (split.Length == 4)
            {
                // os.arch.name.extension, ignore os
                path = Path.Combine(Bootstrapper.Instance.AppFolder, split[1], split[2]);
            }
            else
            {
                throw new ArgumentException("wrong resource name");
            }

            try
            {
                Assembly assembly = typeof(T).GetTypeInfo().Assembly;
                using (Stream resourceStream = assembly.GetManifestResourceStream(resource))
                {
                    using (DeflateStream deflateStream = new DeflateStream(resourceStream, CompressionMode.Decompress))
                    {
                        using (
                            FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write,
                                FileShare.ReadWrite))
                        {
                            byte[] buffer = new byte[1048576];
                            int bytesRead;
                            do
                            {
                                bytesRead = deflateStream.Read(buffer, 0, buffer.Length);
                                if (bytesRead != 0)
                                    fileStream.Write(buffer, 0, bytesRead);
                            }
                            while (bytesRead != 0);
                        }
                    }
                }
            }
            catch
            {
            }
            return path;
        }

        public static void CompressResource(string path)
        {
            using (FileStream inFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using (
                    FileStream outFileStream = new FileStream(path + ".compressed", FileMode.Create, FileAccess.Write,
                        FileShare.ReadWrite))
                {
                    using (DeflateStream deflateStream = new DeflateStream(outFileStream, CompressionMode.Compress))
                    {
                        byte[] buffer = new byte[1048576];
                        int bytesRead;
                        do
                        {
                            bytesRead = inFileStream.Read(buffer, 0, buffer.Length);
                            if (bytesRead != 0)
                                deflateStream.Write(buffer, 0, bytesRead);
                        }
                        while (bytesRead != 0);
                    }
                }
            }
        }

        public static void CompressFolder(string path)
        {
            //if (File.Exists(path + ".zip")) {
            //    //throw new ApplicationException("File already exists: " + path + ".zip");
            //}
            //else {
            //}
            try
            {
                ZipFile.CreateFromDirectory(path, path + ".zip", CompressionLevel.Optimal, false);
            }
            catch (IOException e)
            {
                Trace.WriteLine(e.ToString());
            }
        }

        public static void ExtractFolder(string path, string targetPath)
        {
            //var arch = ZipFile.OpenRead(path);
            //foreach (var entry in arch.Entries) {
            //    entry.ExtractToFile(Path.Combine(targetPath, entry.FullName), true);
            //}
            try
            {
                ZipFile.ExtractToDirectory(path, targetPath);
            }
            catch (IOException e)
            {
                Trace.WriteLine(e.ToString());
            }
        }
    }
}