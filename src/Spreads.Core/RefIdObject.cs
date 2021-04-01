// This Source Code Form is subject to the terms of the Mozilla Public
//  License, v. 2.0. If a copy of the MPL was not distributed with this
//  file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads
{
    /// <summary>
    /// Holds a unique ReferenceID property for debugging purposes.
    /// </summary>
    public class RefIdObject
    {
        private static readonly ConditionalWeakTable<RefIdObject, Box<int>> _refIds = new();
        private static int _refIdCounter;

        /// <summary>
        /// Gets the reference identifier.
        /// </summary>
        public int ReferenceId
        {
            get
            {
                var box = _refIds.GetOrCreateValue(this);
                if (box.Value == 0) box.Value = Interlocked.Increment(ref _refIdCounter);
                return box.Value;
            }
        }
    }
}
