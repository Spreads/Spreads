using System;
using System.Collections.Generic;
using System.Diagnostics;
using Spreads.Threading;

namespace Spreads.Collections.Concurrent
{
    public class ObjectPoolCoreBase<T> : LeftPad112 where T : class
    {
        [DebuggerDisplay("{Value,nq}")]
        protected struct Element
        {
            internal T? Value;
        }

        internal Func<T> Factory;
        protected Element[] _items;
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
                    {
                        idisp.Dispose();
                    }
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