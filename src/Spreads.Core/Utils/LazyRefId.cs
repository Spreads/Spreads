// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Utils
{
    public static class LazyRefId
    {
        private static readonly ConditionalWeakTable<object, Box<int>> _refIds = new();
        private static int _refIdCounter;

        public static int GetLazyRefId(this object? obj)
        {
            if (obj == null)
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj);
            var box = _refIds.GetOrCreateValue(obj);
            if (box!.Value == 0) box.Value = Interlocked.Increment(ref _refIdCounter);
            return box.Value;
        }
    }
}
