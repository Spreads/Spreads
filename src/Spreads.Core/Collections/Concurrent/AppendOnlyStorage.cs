// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// Append-only linear storage with fast reads and locked writes.
    /// </summary>
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

                if (cnt > _storage.Length)
                {
                    var newStorage = new T[_storage.Length * 2];
                    _storage.CopyTo(newStorage, 0);
                    _storage = newStorage; // ref assignment is atomic
                }

                _storage[idx] = value;
                _count = cnt;
                return idx;
            }
        }

        /// <summary>
        /// Get an item at <paramref name="index"/>.
        /// </summary>
        public ref T this[int index]
        {
            // no locks here because _storage could be changed atomically
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _storage[index];
        }

        /// <summary>
        /// Get an item at <paramref name="index"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T UnsafeGetAt(int index) => ref _storage.UnsafeGetAt(index);

        /// <summary>
        /// Get a <see cref="ReadOnlySpan{T}"/> for the entire <see cref="AppendOnlyStorage{T}"/>.
        /// </summary>
        public ReadOnlySpan<T> Span => _storage.AsSpan().Slice(0, _count);
    }
}
