// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Bootstrap
{
    // TODO internal
    public interface INativeLibraryLoader
    {
        IntPtr LoadLibrary(string path);

        bool UnloadLibrary(IntPtr library);

        IntPtr FindFunction(IntPtr library, string function);
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

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);
    }

    internal abstract class UnixLibraryLoader : INativeLibraryLoader
    {
        IntPtr INativeLibraryLoader.LoadLibrary(string path)
        {
            int flags = GetDLOpenFlags();
            return UnixLibraryLoader.dlopen(path, flags);
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

        [DllImport("dl", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

        [DllImport("dl", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlsym(IntPtr library, [MarshalAs(UnmanagedType.LPStr)] string function);

        [DllImport("dl", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dlclose(IntPtr library);
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

    // TODO internal
    public class Loader
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

            string path = ExtractNativeResource<T>(resource);
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

        public static string ExtractNativeResource<T>(string resource)
        {
            var split = resource.Split('.');
            // each process will have its own temp folder
            string path = Path.Combine(Bootstrapper.Instance.TempFolder, split[2] + "." + split[3]);

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
            catch
            {
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

        //public static Assembly LoadManagedDll<T>(string name)
        //{
        //    // [os/][arch/]name.extension
        //    var split = name.Split('/');
        //    string path = null;
        //    if (split.Length == 1)
        //    {
        //        // name.extension
        //        path = Path.Combine(Bootstrapper.Instance.AppFolder, split[0]);
        //    }
        //    else
        //    {
        //        throw new ArgumentException("wrong resource name");
        //    }

        //    Assembly assembly = typeof(T).GetTypeInfo().Assembly;
        //    using (Stream resourceStream = assembly.GetManifestResourceStream(name))
        //    {
        //        using (DeflateStream deflateStream = new DeflateStream(resourceStream, CompressionMode.Decompress))
        //        {
        //            using (
        //                MemoryStream ms = new MemoryStream())
        //            {
        //                byte[] buffer = new byte[1048576];
        //                int bytesRead;
        //                do
        //                {
        //                    bytesRead = deflateStream.Read(buffer, 0, buffer.Length);
        //                    if (bytesRead != 0)
        //                        ms.Write(buffer, 0, bytesRead);
        //                }
        //                while (bytesRead != 0);
        //                var bytes = ms.ToArray();
        //                return Assembly.Load(bytes);
        //            }
        //        }
        //    }
        //}

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
                Console.WriteLine(e.ToString());
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
                Console.WriteLine(e.ToString());
            }
        }
    }
}