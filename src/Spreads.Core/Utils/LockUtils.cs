// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Utils {
    internal static class LockUtils {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnterWriteLockIf(ref int locker, bool condition = true) {
            if (!condition) return false;
            var sw = new SpinWait();
            while (true) {
                if (Interlocked.CompareExchange(ref locker, 1, 0) == 0) {
                    return true;
                }
                sw.SpinOnce();
                // TODO DEBUG-conditional iter count limit
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExitWriteLockIf(ref int locker, bool condition) {
            if (!condition) return;
            Interlocked.Exchange(ref locker, 0);
        }

    }
}
