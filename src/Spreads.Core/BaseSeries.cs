﻿// This Source Code Form is subject to the terms of the Mozilla Public
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

        /// <inheritdoc />
        public abstract ICursor<TK, TV> GetCursor();

        /// <inheritdoc />
        public virtual IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
        {
            return GetCursor();
        }

        /// <inheritdoc />
        public abstract KeyComparer<TK> Comparer { get; }

        /// <inheritdoc />
        public abstract bool IsIndexed { get; }

        /// <inheritdoc />
        public abstract bool IsReadOnly { get; }

        /// <inheritdoc />
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
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
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

        public abstract Task<bool> Updated { get; }

        /// <inheritdoc />
        public abstract bool IsEmpty { get; }

        /// <inheritdoc />
        public abstract KeyValuePair<TK, TV> First { get; }

        /// <inheritdoc />
        public abstract KeyValuePair<TK, TV> Last { get; }

        /// <inheritdoc />
        public virtual TV this[TK key]
        {
            get
            {
                if (TryFind(key, Lookup.EQ, out var tmp))
                {
                    return tmp.Value;
                }
                throw new KeyNotFoundException();
            }
        }

        /// <inheritdoc />
        public abstract TV GetAt(int idx);

        /// <inheritdoc />
        public abstract IEnumerable<TK> Keys { get; }

        /// <inheritdoc />
        public abstract IEnumerable<TV> Values { get; }

        /// <inheritdoc />
        public abstract bool TryFind(TK key, Lookup direction, out KeyValuePair<TK, TV> value);

        /// <inheritdoc />
        public abstract bool TryGetFirst(out KeyValuePair<TK, TV> value);

        /// <inheritdoc />
        public abstract bool TryGetLast(out KeyValuePair<TK, TV> value);

    }

    public abstract class ContainerSeries<TK, TV> : BaseSeries<TK, TV>
    {
        internal long _version;

        internal long _nextVersion;
        private int _writeLocker;
        private TaskCompletionSource<bool> _tcs;
        private TaskCompletionSource<bool> _unusedTcs;

        /// <summary>
        /// Takes a write lock, increments _nextVersion field and returns the current value of the _version field.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected long BeforeWrite(bool takeLock = true)
        {
            var spinwait = new SpinWait();
            long version = -1L;
            // NB try{} finally{ .. code here .. } prevents method inlining, therefore should be used at the caller place, not here
            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
            while (takeLock)
            {
                if (Interlocked.CompareExchange(ref _writeLocker, 1, 0) == 0)
                {
                    // Interlocked.CompareExchange generated implicit memory barrier
                    var nextVersion = _nextVersion + 1L;
                    // Volatile.Write prevents the read above to move below
                    Volatile.Write(ref _nextVersion, nextVersion);
                    // Volatile.Read prevents any read/write after to to move above it
                    // see CoreClr 6121, esp. comment by CarolEidt
                    version = Volatile.Read(ref _version);
                    // do not return from a loop, see CoreClr #9692
                    break;
                }
                if (spinwait.Count == 10000) // 10 000 is c.700 msec
                {
                    TryUnlock();
                }
                // NB Spinwait significantly increases performance probably due to PAUSE instruction
                spinwait.SpinOnce();
            }
            return version;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected virtual void TryUnlock()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Release write lock and increment _version field or decrement _nextVersion field if no updates were made
        /// </summary>
        /// <param name="version"></param>
        /// <param name="doIncrement"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AfterWrite(long version, bool doIncrement = true)
        {
            if (version < 0L) return;

            // Volatile.Write will prevent any read/write to move below it
            if (doIncrement)
            {
                Volatile.Write(ref _version, version + 1L);

                // NB no Interlocked inside write lock, readers must follow pattern to retry MN after getting `Updated` Task, and MN has it's own read lock. For other cases the behavior in undefined.
                var tcs = _tcs;
                _tcs = null;
                tcs?.SetResult(true);
            }
            else
            {
                // set nextVersion back to original version, no changes were made
                Volatile.Write(ref _nextVersion, version);
            }
            // release write lock
            //Interlocked.Exchange(ref _writeLocker, 0);
            // TODO review if this is enough. Iterlocked is right for sure, but is more expensive (slower by more than 25% on field increment test)
            Volatile.Write(ref _writeLocker, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyUpdate(bool result = true)
        {
            // NB in some cases (inside write lock) interlocked is not needed, but we then use this same lgic manually without Interlocked
            var tcs = Interlocked.Exchange(ref _tcs, null);
            tcs?.SetResult(result);
        }

        /// <summary>
        /// A Task that is completed with True whenever underlying data is changed.
        /// Internally used for signaling to async cursors.
        /// After getting the Task one should check if any changes happened (version change or cursor move) before awating the task.
        /// If the task is completed with false then the series is read-only, immutable or complete.
        /// </summary>
        public override Task<bool> Updated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // saving one allocation vs Interlocked.Exchange call
                var unusedTcs = Interlocked.Exchange(ref _unusedTcs, null);
                var newTcs = unusedTcs ?? new TaskCompletionSource<bool>();
                var tcs = Interlocked.CompareExchange(ref _tcs, newTcs, null);
                if (tcs == null)
                {
                    // newTcs was put to the _tcs field, use it
                    tcs = newTcs;
                }
                else
                {
                    Volatile.Write(ref _unusedTcs, newTcs);
                }
                return tcs.Task;
            }
        }
    }
}