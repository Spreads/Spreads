// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// Fast reads and locked writes.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AppendOnlyStorage<T>
    {
        internal T[] _storage = new T[16];
        private int _count;
        public int Count => _count;

        public int Add(T value)
        {
            lock (this)
            {
                var idx = _count;
                int cnt = idx + 1;
                _count = cnt;

                if (cnt > _storage.Length)
                {
                    var newStorage = new T[_storage.Length * 2];
                    _storage.CopyTo(newStorage, 0);
                    _storage = newStorage; // ref assignment is atomic
                }

                _storage[idx] = value;
                return idx;
            }
        }

        public ref T this[int index]
        {
            // no locks here because _storage could be changed atomically
            // and to use an index from code it must be added first and
            // Add must return first (otherwise usage is broken).
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _storage[index];
        }
    }
}
