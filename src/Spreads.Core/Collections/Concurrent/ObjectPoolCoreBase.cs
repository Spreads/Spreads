using System;
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
    }
}