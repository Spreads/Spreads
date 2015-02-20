using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Bootstrapper {

    public static class Bootstrapper {

        public static void Main() {

            Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\libspreadsdb\w32\libspreadsdb.dll");
            Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\libspreadsdb\w64\libspreadsdb.dll");

            //Loader.CompressFolder(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\msvcrt\x32");
            //Loader.ExtractFolder(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\msvcrt\x64.zip", @"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\msvcrt");


            //var json = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { 1, 2, 3, 4, 5 });
            //Console.WriteLine("Running Bootstrapper: " + json);
            //YepppTest.Run();
            Console.ReadLine();
        }

        // Bootstrapper extracts dlls embedded as resource to the app folder.
        // Those dlls could contain other embedded dlls, which are extracted recursively as Russian dolls.
        // Bootstrapper overwrites existing files 
        // TODO major version

        // AppData/fi.im/bin/ - all dlls and exes
        // AppData/fi.im/config/config.sdb - all app and users configuration
        // LocalAppData/fi.im/data - data folder
        //                   /data/local.sdb - data for not logged-in user
        //                   /data/userid.sdb - data for not logged-in user
        // Documents/Modules - distributes LGPL libraries + user libraries. 
        //                      All are loaded to app domain


        // Botstrap self
        static Bootstrapper() {

            Bootstrapper.Bootstrap<Loader>(
                null, //new[] { "yeppp" },
                new[] { "Newtonsoft.Json.dll" },
                null,
                null,
                () => {
                    if (!Directory.Exists(AppFolder)) {
                        Directory.CreateDirectory(AppFolder);
                    }

                    if (!Directory.Exists(Path.Combine(AppFolder, "x32"))) {
                        Directory.CreateDirectory(Path.Combine(AppFolder, "x32"));
                    }

                    if (!Directory.Exists(Path.Combine(AppFolder, "x64"))) {
                        Directory.CreateDirectory(Path.Combine(AppFolder, "x64"));
                    }

                    if (!Directory.Exists(AppFolder)) {
                        Directory.CreateDirectory(AppFolder);
                    }

                    if (!Directory.Exists(ConfigFolder)) {
                        Directory.CreateDirectory(ConfigFolder);
                    }

                    if (!Directory.Exists(DataFolder)) {
                        Directory.CreateDirectory(DataFolder);
                    }

                    if (!Directory.Exists(TempFolder)) {
                        Directory.CreateDirectory(TempFolder);
                    }
                }, 
                () => {
                    //Yeppp.Library.Init();
                },
                () => {
                    //Yeppp.Library.Release();
                });
        }

        /// <summary>
        /// in AppData and AppDataLocal
        /// </summary>
        private const string rootFolder = "fi.im";
        private const string configSubFolder = "config";
        private const string appSubFolder = "bin";
        private const string dataSubFolder = "data";
        private const string docFolder = "Docs";
        private const string gplFolder = "Libraries";

        internal static string ConfigFolder {
            get {
                return Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
              rootFolder, configSubFolder);
            }
        }

        internal static string AppFolder {
            get {
                return Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
              rootFolder, appSubFolder);
            }
        }

        internal static string DataFolder {
            get {
                return Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
              rootFolder, dataSubFolder);
            }
        }

        private static string _tmpFolder = null;
        internal static string TempFolder {
            get {
                if (_tmpFolder == null) {
                    _tmpFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                }
                return _tmpFolder;
            }
        }

        // keep references to avoid GC
        internal static List<NativeLibrary> loadedLibraries = new List<NativeLibrary>();
        private static Disposer disposer = new Disposer();
        private static Action DisposeAction;
        /// <summary>
        /// From assembly with type T load libraries
        /// </summary>
        public static void Bootstrap<T>(string[] nativeLibNames = null,
            string[] managedLibNames = null,
            string[] resourceNames = null,
            string[] serviceNames = null, // ensure these .exes are running
            Action preCopyAction = null,
            Action postCopyAction = null,
            Action disposeAction = null) {

            if (preCopyAction != null) preCopyAction.Invoke();

            if (nativeLibNames != null) {
                foreach (var nativeName in nativeLibNames) {
                    loadedLibraries.Add(Loader.LoadNativeLibrary<T>(nativeName));
                }
            }

            if (managedLibNames != null) {
                foreach (var managedName in managedLibNames) {
                    Trace.Assert(managedName.EndsWith(".dll"));
                    Loader.ExtractResource<T>(managedName);
                }
            }
            AppDomain.CurrentDomain.AssemblyResolve +=
                new ResolveEventHandler(Loader.ResolveManagedAssembly);

            if (resourceNames != null) {
                foreach (var resourceName in resourceNames) {
                    Loader.ExtractResource<T>(resourceName);
                }
            }

            if (serviceNames != null) {
                foreach (var serviceName in serviceNames) {
                    Trace.Assert(serviceName.EndsWith(".exe"));
                    // TODO run exe as singletone process by path
                    throw new NotImplementedException("TODO start exe process");
                }
            }

            if (postCopyAction != null) postCopyAction.Invoke();

            DisposeAction = disposeAction;
        }


        private class Disposer : IDisposable {
            public void Dispose() {
                if(Bootstrapper.DisposeAction != null) Bootstrapper.DisposeAction.Invoke();
                foreach (var loadedLibrary in Bootstrapper.loadedLibraries) {
                    if (loadedLibrary != null) loadedLibrary.Dispose();
                }
                try {
                    Directory.Delete(Bootstrapper.TempFolder, true);
                } catch {
                }
            }

            ~Disposer() {
                Dispose();
            }
        }
    }
}
