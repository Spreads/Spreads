using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Collections {

    public unsafe static class SyncUtils {
        public static void WriteLock(IntPtr locker, Action action) {
            try {
                var sw = new SpinWait();
                var cont = true;
                try {
                } finally {
                    while (cont) {
                        if (Interlocked.CompareExchange(ref *(int*)(locker), 1, 0) == 0) {
                            cont = false;
                        }
                        sw.SpinOnce();
                    }
                }
                action.Invoke();
            } finally {
                Interlocked.Exchange(ref *(int*)(locker), 0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnterWriteLock(IntPtr locker) {
            var sw = new SpinWait();
            var cont = true;
            try { } finally {
                while (cont) {
                    if (Interlocked.CompareExchange(ref *(int*)(locker), 1, 0) == 0) {
                        cont = false;
                    }
                    sw.SpinOnce();
                }
            }
            return !cont;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExitWriteLock(IntPtr locker) {
            Interlocked.Exchange(ref *(int*)(locker), 0);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadLockIf<T>(IntPtr nextVersion, IntPtr currentVersion, bool condition, Func<T> f) {
            var value = default(T);
            var doSpin = true;
            var sw = new SpinWait();
            while (doSpin) {
                var ver = condition
                    ? Volatile.Read(ref *(long*)(currentVersion))
                    : *(long*)(currentVersion);
                value = f.Invoke();
                if (condition) {
                    var nextVer = Volatile.Read(ref *(long*)nextVersion);
                    if (ver == nextVer) {
                        doSpin = false;
                    } else {
                        sw.SpinOnce();
                    }
                } else {
                    doSpin = false;
                }
            }
            return value;
        }
    }
}
