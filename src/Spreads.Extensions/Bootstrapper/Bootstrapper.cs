using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Bootstrap {


    public static class Program {

        // when running as console app, init Bootstrapper
        static Program() {
            Console.WriteLine(Bootstrapper.Instance.AppFolder);
        }

        public static void Main() {


            // NetMQ
            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\src\SpreadsDB\bin\Release\AsyncIO.dll");
            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\src\SpreadsDB\bin\Release\NetMQ.dll");

            // RX
            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\src\SpreadsDB\bin\Release\System.Reactive.Core.dll");
            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\src\SpreadsDB\bin\Release\System.Reactive.Interfaces.dll");
            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\src\SpreadsDB\bin\Release\System.Reactive.Linq.dll");
            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\src\SpreadsDB\bin\Release\System.Reactive.PlatformServices.dll");

            // Blosc
            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\libblosc\win\x64\libblosc.dll");
            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\libblosc\win\x32\libblosc.dll");


            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\SpreadsDB\src\libspreadsdb\out\w64\bin\libspreadsdb.dll");
            //Loader.CompressResource(@"C:\Users\Sun\MD\CS\Public Projects\SpreadsDB\src\libspreadsdb\out\w32\bin\libspreadsdb.dll");
            //Loader.CompressFolder(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\msvcrt\x32");
            //Loader.ExtractFolder(@"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\msvcrt\x64.zip", @"C:\Users\Sun\MD\CS\Public Projects\Spreads\lib\msvcrt");



            var json = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { 1, 2, 3, 4, 5 });
            Console.WriteLine("Running Bootstrapper: " + json);
            //YepppTest.Run();

            Console.ReadLine();
        }
    }

    public class Bootstrapper {
        internal static ABI ABI { get; set; }

        private static readonly Bootstrapper instance = new Bootstrapper();
        public static Bootstrapper Instance {
            get {
                return instance;
            }
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

        static Bootstrapper() {
            ABI = Process.DetectABI();

            instance.Bootstrap<Loader>(
                new[] { "libblosc", "yeppp" },
                null, //new[] { "Newtonsoft.Json.dll" },
                null,
                null,
                () => {
#if DEBUG
                    Console.WriteLine("Pre-copy action");
#endif
                },
                () => {
                    Yeppp.Library.Init();
#if DEBUG
                Console.WriteLine("Post-copy action");
#endif
                },
                () => {
                    Yeppp.Library.Release();
                });

            AppDomain.CurrentDomain.AssemblyResolve +=
                new ResolveEventHandler((object sender, ResolveEventArgs args) => {
                    var an = new AssemblyName(args.Name);
                    return instance.managedLibraries[an.Name + ".dll"];
                });
            //new ResolveEventHandler(Loader.ResolveManagedAssembly);
        }


        private string _baseFolder;
        private string _dataFolder;

        // Botstrap self
        public Bootstrapper()
        {
            _assemblyDirectory = GetAssemblyDirectory();
            _baseFolder = Environment.UserInteractive
                ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                : _assemblyDirectory;

            _dataFolder = Path.Combine(_baseFolder, rootFolder, dataSubFolder);

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
        }

        /// <summary>
        /// in AppData and AppDataLocal
        /// </summary>
        private const string rootFolder = "Spreads";
        private const string configSubFolder = "config";
        private const string appSubFolder = "bin";
        private const string dataSubFolder = "data";
        // TODO next two only in user interactive mode
        private const string docFolder = "Docs";
        private const string gplFolder = "Libraries";


        public string AssemblyDirectory
        {
            get { return _assemblyDirectory; }
        }

        public static string GetAssemblyDirectory()
        {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        public string BaseFolder {
            get {
                return _baseFolder;
            }
            set {
                _baseFolder = value;
            }
        }

        internal string ConfigFolder {
            get {
                return Path.Combine(_baseFolder, rootFolder, configSubFolder);
            }
        }

        public string AppFolder {
            get {
                return Path.Combine(_baseFolder, rootFolder, appSubFolder);
            }
        }

        internal string DataFolder {
            get {
                return _dataFolder;
            }
            set {
                _dataFolder = value;
            }
        }

        private string _tmpFolder = null;
        internal string TempFolder {
            get {
                if (_tmpFolder == null) {
                    _tmpFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                }
                return _tmpFolder;
            }
        }

        // keep references to avoid GC
        internal Dictionary<string, NativeLibrary> nativeLibraries = new Dictionary<string, NativeLibrary>();
        // thi will block other dlls with the same name from loading
        // TODO do not store managed, return to resolve method
        internal Dictionary<string, Assembly> managedLibraries = new Dictionary<string, Assembly>();
        private List<Action> DisposeActions = new List<Action>();
        private readonly string _assemblyDirectory;

        /// <summary>
        /// From assembly with type T load libraries
        /// </summary>
        public void Bootstrap<T>(string[] nativeLibNames = null,
            string[] managedLibNames = null,
            string[] resourceNames = null,
            string[] serviceNames = null, // ensure these .exes are running
            Action preCopyAction = null,
            Action postCopyAction = null,
            Action disposeAction = null) {

            if (preCopyAction != null) preCopyAction.Invoke();

            if (nativeLibNames != null) {
                foreach (var nativeName in nativeLibNames) {
                    if (nativeLibraries.ContainsKey(nativeName)) continue;
                    nativeLibraries.Add(nativeName, Loader.LoadNativeLibrary<T>(nativeName));
                }
            }

            if (managedLibNames != null) {
                foreach (var managedName in managedLibNames) {
                    if (managedLibraries.ContainsKey(managedName)) continue;
                    Trace.Assert(managedName.EndsWith(".dll"));
                    //if (!Environment.UserInteractive){
                    //    Debugger.Launch();
                    //}
                    managedLibraries.Add(managedName, Loader.LoadManagedDll<T>(managedName));
                }
            }


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

            DisposeActions.Add(disposeAction);
        }

        ~Bootstrapper() {
            if (DisposeActions.Count > 0) {
                foreach (var action in DisposeActions) {
                    action.Invoke();
                }
            }
            foreach (var loadedLibrary in nativeLibraries) {
                if (loadedLibrary.Value != null) loadedLibrary.Value.Dispose();
            }
            try {
                Directory.Delete(Instance.TempFolder, true);
            } catch {
            }
        }
    }
}
