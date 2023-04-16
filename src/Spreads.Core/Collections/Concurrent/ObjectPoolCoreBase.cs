// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Spreads.Threading;

namespace Spreads.Collections.Concurrent
{
    public abstract class ObjectPoolCoreBase<T> : LeftPad112 where T : class
    {
        protected ObjectPoolCoreBase(int size, Func<T?>? factory = null)
        {
            _items = new Element[size];
            Factory = factory;
        }

        [DebuggerDisplay("{Value,nq}")]
        protected struct Element
        {
            internal T? Value;
        }

        protected readonly Func<T?>? Factory;
        protected readonly Element[] _items;
        protected volatile bool _disposed;

        public int Capacity => _items.Length;

        public virtual void Dispose()
        {
            lock (_items)
            {
                if (_disposed)
                    return;
                _disposed = true;

                foreach (var o in _items)
                {
                    if (o.Value != null && o.Value is IDisposable idisp)
                        idisp.Dispose();
                }
            }
        }

        /// <summary>
        /// For diagnostics only.
        /// </summary>
        internal IEnumerable<T> EnumerateItems()
        {
            foreach (var element in _items)
            {
                var value = element.Value;
                if (value != null)
                    yield return value;
            }
        }
    }
}
