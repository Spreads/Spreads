using Bootstrap;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Spreads.Native {
    internal static class NativeMethods {

        internal static ABI ABI { get; set; }

        private static INativeLibraryFacade _libraryFacade;
        public static INativeLibraryFacade Library { get { return _libraryFacade; } }


        internal static void Init() {
            ABI = Bootstrapper.ABI;
            _libraryFacade = new NativeLibraryFacade();
        }

    }
}
