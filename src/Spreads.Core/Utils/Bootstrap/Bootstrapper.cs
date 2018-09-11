// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Spreads.Utils.Bootstrap
{
    internal static class Program
    {
        // when running as console app, init Bootstrapper
        static Program()
        {
            if (Bootstrapper.Instance.AppFolder == null)
            {
                throw new Exception("Bootstrapper.Instance.AppFolder == null");
            };
        }

        /// <summary>
        ///
        /// </summary>
        public static void Main(string[] args)
        {
            Run(args);
        }

        private static void Run(string[] args, bool interactive = false)
        {
            if (args == null || args.Length == 0)
            {
                string line = null;
                do
                {
                    Console.WriteLine("Compress individual files: filePath1 ... filePathN");
                    //Console.WriteLine("Compress directories: -d dirPath1 ... dirPathN");
                    Console.WriteLine("Compress files by pattern: -p pattern dirPath1 ... dirPathN");
                    line = Console.ReadLine();
                }
                while (string.IsNullOrWhiteSpace(line));
                try
                {
                    Run(line.Split(' '), true);
                    Console.WriteLine("Completed successfully");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.ToString());
                }
                Run(null, true);
            }
            else
            {
                // directory
                //if (args[0].ToLower().Trim() == "-d") {
                //    if (args.Length < 2) {
                //        var msg = "You must provide at least one directory";
                //        if (interactive) {
                //            Console.ForegroundColor = ConsoleColor.Red;
                //            Console.WriteLine(msg);
                //            Console.ResetColor();
                //            Run(null, true);
                //        } else {
                //            throw new ArgumentException(msg);
                //        }
                //    }
                //    for (int i = 1; i < args.Length; i++) {
                //        Loader.CompressFolder(args[i]);
                //        Console.ForegroundColor = ConsoleColor.Yellow;
                //        Console.WriteLine($"Compressed directory: {args[i]}");
                //        Console.ResetColor();
                //    }
                //    if (interactive) {
                //        Run(null, true);
                //    } else {
                //        return;
                //    }

                //}

                // pattern
                if (args[0].ToLower().Trim() == "-p")
                {
                    if (args.Length < 2)
                    {
                        var msg = "You must provide a search pattern";
                        if (interactive)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(msg);
                            Console.ResetColor();
                            Console.WriteLine();
                            Run(null, true);
                        }
                        else
                        {
                            throw new ArgumentException(msg);
                        }
                    }
                    var pattern = args[1];
                    if (args.Length < 3)
                    {
                        var list = args.ToList();
                        var assemblyFolder = ".";
                        list.Add(assemblyFolder);
                        args = list.ToArray();
                    }
                    var count = 0;

                    for (int i = 2; i < args.Length; i++)
                    {
                        var path = args[i];
                        if (!Directory.Exists(path))
                        {
                            var msg = $"Directory '{path}' does not exists";
                            if (interactive)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(msg);
                                Console.ResetColor();
                                Console.WriteLine();
                                Run(null, true);
                            }
                            else
                            {
                                throw new ArgumentException(msg);
                            }
                        }

                        var files = Directory.GetFiles(path, pattern, SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            Loader.CompressResource(file);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"Compressed file: '{file}'");
                            Console.ResetColor();
                            count++;
                        }
                    }

                    if (count == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No matching files");
                        Console.ResetColor();
                        Console.WriteLine();
                    }

                    if (interactive)
                    {
                        Run(null, true);
                    }
                    else
                    {
                        return;
                    }
                }

                // single files
                if (args.Length < 1)
                {
                    var msg = "You must provide at least one file";
                    if (interactive)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(msg);
                        Console.ResetColor();
                        Run(null, true);
                    }
                    else
                    {
                        throw new ArgumentException(msg);
                    }
                }

                foreach (var file in args)
                {
                    if (!File.Exists(file))
                    {
                        var msg = $"File '{file}' does not exists";
                        if (interactive)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(msg);
                            Console.ResetColor();
                            Console.WriteLine();
                            Run(null, true);
                        }
                        else
                        {
                            throw new ArgumentException(msg);
                        }
                    }

                    Loader.CompressResource(file);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Compressed file: '{file}'");
                    Console.ResetColor();
                }

                if (interactive)
                {
                    Run(null, true);
                }
            }
        }
    }

    // TODO internal all members and type if possible
    public class Bootstrapper
    {
        internal static ABI ABI { get; set; }

        private static readonly Bootstrapper instance = new Bootstrapper();

        public static Bootstrapper Instance
        {
            get
            {
                return instance;
            }
        }

        static Bootstrapper()
        {
            ABI = Process.DetectABI();

            //            instance.Bootstrap<Loader>(
            //                new string[] { }, //
            //                null, //new[] { "Newtonsoft.Json.dll" },
            //                null,
            //                null,
            //                () =>
            //                {
            //#if DEBUG
            //                    Console.WriteLine("Pre-copy action");
            //#endif
            //                },
            //                () =>
            //                {
            //#if DEBUG
            //                    Console.WriteLine("Post-copy action");
            //#endif
            //                },
            //                () =>
            //                {
            //                });

            //new ResolveEventHandler(Loader.ResolveManagedAssembly);
        }

        private string _baseFolder;
        private string _dataFolder;

        // Botstrap self
        public Bootstrapper()
        {
            //_assemblyDirectory = GetAssemblyDirectory();
            _baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dataFolder = Path.Combine(_baseFolder, rootFolder, dataSubFolder);

            if (!Directory.Exists(AppFolder))
            {
                Directory.CreateDirectory(AppFolder);
            }

            //if (!Directory.Exists(Path.Combine(AppFolder, "x32"))) {
            //    Directory.CreateDirectory(Path.Combine(AppFolder, "x32"));
            //}

            //if (!Directory.Exists(Path.Combine(AppFolder, "x64"))) {
            //    Directory.CreateDirectory(Path.Combine(AppFolder, "x64"));
            //}

            if (!Directory.Exists(AppFolder))
            {
                Directory.CreateDirectory(AppFolder);
            }

            if (!Directory.Exists(ConfigFolder))
            {
                Directory.CreateDirectory(ConfigFolder);
            }

            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }

            if (!Directory.Exists(TempFolder))
            {
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

        //public string AssemblyDirectory {
        //    get { return _assemblyDirectory; }
        //}

        public string BaseFolder
        {
            get
            {
                return _baseFolder;
            }
            set
            {
                _baseFolder = value;
            }
        }

        internal string ConfigFolder
        {
            get
            {
                return Path.Combine(_baseFolder, rootFolder, configSubFolder);
            }
        }

        public string AppFolder
        {
            get
            {
                return Path.Combine(_baseFolder, rootFolder, appSubFolder);
            }
        }

        public string DataFolder
        {
            get
            {
                return _dataFolder;
            }
            set
            {
                _dataFolder = value;
            }
        }

        private string _tmpFolder = null;

        public string TempFolder
        {
            get
            {
                if (_tmpFolder == null)
                {
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
        //private readonly string _assemblyDirectory;

        /// <summary>
        /// From assembly with type T load libraries
        /// </summary>
        public void Bootstrap<T>(string nativeLibraryName,
            string[] resourceNames = null,
            Action preCopyAction = null,
            Action<NativeLibrary> postCopyAction = null,
            Action disposeAction = null)
        {
            preCopyAction?.Invoke();

            NativeLibrary nativeLibrary = null;
            if (nativeLibraryName != null)
            {
                if (!nativeLibraries.ContainsKey(nativeLibraryName))
                {
                    nativeLibrary = Loader.LoadNativeLibrary<T>(nativeLibraryName);
                    nativeLibraries.Add(nativeLibraryName, nativeLibrary);
                    Trace.TraceInformation("Loaded native library: " + nativeLibraryName);
                }
            }

            if (resourceNames != null)
            {
                foreach (var resourceName in resourceNames)
                {
                    Loader.ExtractResource<T>(resourceName);
                }
            }

            postCopyAction?.Invoke(nativeLibrary);

            DisposeActions.Add(disposeAction);
        }

        // TODO now it is not working
        ~Bootstrapper()
        {
            if (DisposeActions.Count > 0)
            {
                foreach (var action in DisposeActions)
                {
                    try
                    {
                        action.Invoke();
                    }
                    catch
                    {
                    }
                }
            }

            foreach (var loadedLibrary in nativeLibraries)
            {
                if (loadedLibrary.Value != null)
                {
                    try
                    {
                        loadedLibrary.Value.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            try
            {
                Directory.Delete(Instance.TempFolder, true);
            }
            catch
            {
            }
        }
    }
}