// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads
{
    /// <summary>
    /// Base class for all series implementations.
    /// </summary>
    public class BaseSeries
    {
        private static readonly ConditionalWeakTable<BaseSeries, Dictionary<string, object>> Attributes = new ConditionalWeakTable<BaseSeries, Dictionary<string, object>>();

        /// <summary>
        /// Get an attribute that was set using SetAttribute() method.
        /// </summary>
        /// <param name="attributeName">Name of an attribute.</param>
        /// <returns>Return an attribute value or null is the attribute is not found.</returns>
        public object GetAttribute(string attributeName)
        {
            if (Attributes.TryGetValue(this, out Dictionary<string, object> dic) && dic.TryGetValue(attributeName, out object res))
            {
                return res;
            }
            return null;
        }

        /// <summary>
        /// Set any custom attribute to a series. An attribute is available during lifetime of a series and is available via GetAttribute() method.
        /// </summary>
        public void SetAttribute(string attributeName, object attributeValue)
        {
            var dic = Attributes.GetOrCreateValue(this);
            dic[attributeName] = attributeValue;
        }
    }

    /// <summary>
    /// Base generic class for all series implementations.
    /// </summary>
    /// <typeparam name="TK">Type of series keys.</typeparam>
    /// <typeparam name="TV">Type of series values.</typeparam>
    public abstract class BaseSeries<TK, TV> : BaseSeries, IReadOnlySeries<TK, TV>
    {
        private object _syncRoot;

        public abstract ICursor<TK, TV> GetCursor();

        public abstract IComparer<TK> Comparer { get; }
        public abstract bool IsIndexed { get; }
        public abstract bool IsReadOnly { get; }

        public virtual IDisposable Subscribe(IObserver<KeyValuePair<TK, TV>> observer)
        {
            // TODO not virtual and implement all logic here, including backpressure case
            throw new NotImplementedException();
        }

        IAsyncEnumerator<KeyValuePair<TK, TV>> IAsyncEnumerable<KeyValuePair<TK, TV>>.GetEnumerator()
        {
            return GetCursor();
        }

        IEnumerator<KeyValuePair<TK, TV>> IEnumerable<KeyValuePair<TK, TV>>.GetEnumerator()
        {
            return GetCursor();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetCursor();
        }

        public object SyncRoot
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_syncRoot == null)
                {
                    Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                }
                return _syncRoot;
            }
        }

        // IReadOnlySeries members
        public abstract bool IsEmpty { get; }

        public abstract KeyValuePair<TK, TV> First { get; }
        public abstract KeyValuePair<TK, TV> Last { get; }

        public virtual TV this[TK key]
        {
            get
            {
                TV tmp;
                if (TryGetValue(key, out tmp))
                {
                    return tmp;
                }
                throw new KeyNotFoundException();
            }
        }

        public abstract TV GetAt(int idx);

        public abstract IEnumerable<TK> Keys { get; }
        public abstract IEnumerable<TV> Values { get; }
        public abstract Task<bool> Updated { get; }

        public abstract bool TryFind(TK key, Lookup direction, out KeyValuePair<TK, TV> value);

        public abstract bool TryGetFirst(out KeyValuePair<TK, TV> value);

        public abstract bool TryGetLast(out KeyValuePair<TK, TV> value);

        public abstract bool TryGetValue(TK key, out TV value);
    }
}