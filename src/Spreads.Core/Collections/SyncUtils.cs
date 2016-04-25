using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Collections {

    public unsafe static class SyncUtils {
        private static int _pid = Process.GetCurrentProcess().Id;

        public static void WriteLock(IntPtr locker, Action<bool> action) {
            try {
                var sw = new SpinWait();
                var cont = true;
                var cleanup = false;
                try {
                } finally {
                    while (cont) {
                        var pid = Interlocked.CompareExchange(ref *(int*)(locker), _pid, 0);
                        if (pid == 0) {
                            cont = false;
                        }
                        // TODO Add managed thread id to timeout logic
                        if (sw.Count > 100 && pid != _pid) {
                            try {
                                var p = Process.GetProcessById(pid);
                                throw new ApplicationException($"Cannot acquire lock, process {p.Id} has it for a long time");
                            } catch (ArgumentException ex) {
                                // pid is not running anymore, try to take it
                                if (pid == Interlocked.CompareExchange(ref *(int*)(locker), _pid, pid)) {
                                    cleanup = true;
                                    break;
                                }
                            }
                        }
                        sw.SpinOnce();
                    }
                }
                action.Invoke(cleanup);
            } finally {
                var pid = Interlocked.CompareExchange(ref *(int*)(locker), 0, _pid);
                if (pid != _pid) {
                    throw new ApplicationException("Cannot release lock, it was stolen");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnterWriteLock(IntPtr locker) {
            var sw = new SpinWait();
            var cont = true;
            try { } finally {
                while (cont) {
                    if (Interlocked.CompareExchange(ref *(int*)(locker), _pid, 0) == 0) {
                        cont = false;
                    }
                    sw.SpinOnce();
                }
            }
            return !cont;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExitWriteLock(IntPtr locker) {
            var pid = Interlocked.CompareExchange(ref *(int*)(locker), 0, _pid);
            if (pid != _pid) {
                throw new ApplicationException("Cannot release lock, it was stolen");
            }
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
