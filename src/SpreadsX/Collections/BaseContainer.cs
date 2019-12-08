// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Collections
{
    /// <summary>
    /// Base class for data container implementations.
    /// </summary>
    [Utils.CannotApplyEqualityOperator]
    public class BaseContainer : IDataSource, IAsyncCompleter, IDisposable
    {
        /// <summary>
        /// Container-specific data. Never null - instead <see cref="DataBlock.Empty"/> is used as a sentinel.
        /// </summary>
        [NotNull]
        internal object Data = DataBlock.Empty;

        /// <summary>
        /// Flags.
        /// </summary>
        internal Flags Flags;

        private int _locker;
        internal int OrderVersion;

        // See http://joeduffyblog.com/2009/06/04/a-scalable-readerwriter-scheme-with-optimistic-retry/

        internal volatile int Version;
        internal volatile int NextVersion;

        internal BaseContainer()
        { }

        public ContainerLayout ContainerLayout => Flags.ContainerLayout;

        public Mutability Mutability => Flags.Mutability;

        public KeySorting KeySorting => Flags.KeySorting;

        public bool IsCompleted => Mutability == Mutability.ReadOnly;

        public ulong? RowCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Data is DataBlock block)
                {
                    return (ulong)block.RowCount;
                }

                if (Data is IRowCount rc)
                {
                    return rc.RowCount;
                }

                return RowCountImpl();
            }
        }

        protected virtual ulong? RowCountImpl()
        {
            return null;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Data is DataBlock block)
                {
                    return block.RowCount == 0;
                }

                if (Data is IRowCount rc)
                {
                    return rc.IsEmpty;
                }

                return IsEmptyImpl();
            }
        }

        protected virtual bool IsEmptyImpl()
        {
            throw new NotSupportedException("Derived containers must implement this method if Data does not provide it.");
        }

        #region Synchronization

        /// <summary>
        /// Acquire a write lock and increment <seealso cref="NextVersion"/>.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use this ONLY for IMutableSeries operations")]
        internal void BeforeWrite()
        {
#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
#endif
            var spinwait = new SpinWait();
            // NB try{} finally{ .. code here .. } prevents method inlining, therefore should be used at the caller place, not here
            var doSpin = !Flags.IsImmutable;
            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
            while (doSpin)
            {
                if (Interlocked.CompareExchange(ref _locker, 1, 0) == 0)
                {
                    unchecked
                    {
                        NextVersion++;
                    }
                    break;
                }
#if DEBUG
                sw.Stop();
                var elapsed = sw.ElapsedMilliseconds;
                if (elapsed > 1000)
                {
                    TryUnlock();
                }
#endif
                spinwait.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG")]
        internal virtual void TryUnlock()
        {
            ThrowHelper.FailFast("This should never happen. Locks are only in memory and should take less than a microsecond.");
        }

        /// <summary>
        /// Release write lock and increment <see cref="Version"/> or decrement <seealso cref="NextVersion"/> if no updates were made.
        /// Call NotifyUpdate if doVersionIncrement is true
        /// </summary>
        /// <param name="doVersionIncrement"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use this ONLY for IMutableSeries operations")]
        internal void AfterWrite(bool doVersionIncrement)
        {
            if (Flags.IsImmutable)
            {
                if (doVersionIncrement)
                {
                    // TODO(!) review when/why this is possible
                    // ThrowHelper.FailFast("WTF, how doVersionIncrement == true when immutable!?");
                    unchecked
                    {
                        Version++;
                    }
                    NextVersion = Version;

                    // TODO WTF? see git blame for the next line, what was here?
                    NotifyUpdate(); // TODO remove after flags fixed
                }
            }
            else if (doVersionIncrement)
            {
                unchecked
                {
                    Version++;
                }

                // TODO
                NotifyUpdate();
            }
            else
            {
                // set nextVersion back to original version, no changes were made
                NextVersion = Version;
            }

            ReleaseLock();
        }

        /// <summary>
        /// Acquire lock without incrementing next version as in <see cref="BeforeWrite"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AcquireLock()
        {
#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
#endif
            var spinwait = new SpinWait();
            // NB try{} finally{ .. code here .. } prevents method inlining, therefore should be used at the caller place, not here
            while (true)
            {
                if (Interlocked.CompareExchange(ref _locker, 1, 0) == 0L)
                {
                    // do not return from a loop, see CoreClr #9692
                    break;
                }
#if DEBUG
                sw.Stop();
                var elapsed = sw.ElapsedMilliseconds;
                if (elapsed > 1000)
                {
                    TryUnlock();
                }
#endif
                spinwait.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReleaseLock()
        {
            Volatile.Write(ref _locker, 0);
        }

        #endregion Synchronization

        #region Subscription & notification

        // Union of ContainerSubscription | ThreadSafeList<ContainerSubscription> (ThreadSafeList could be implemented differently)
        private object? _subscriptions;

        private class ContainerSubscription : IDisposable
        {
            private readonly BaseContainer _container;
            public readonly IAsyncCompletable Subscriber;

            public ContainerSubscription(BaseContainer container, IAsyncCompletable subscriber)
            {
                _container = container;
                Subscriber = subscriber;
            }

            // ReSharper disable once UnusedParameter.Local
            private void Dispose(bool disposing)
            {
                // 1. lock(this) is bad
                // 2. sync object just for subscribe/dispose will add 8 bytes to obj size, rarely used
                // 3. reusing _container._locker could interfere with data writes,
                //    but in steady state there should be no subscriptions/disposals.

                _container.AcquireLock();
                try
                {
                    DoDispose();
                }
                finally
                {
                    _container.ReleaseLock();
                }

                void DoDispose()
                {
                    if (ReferenceEquals(_container._subscriptions, this))
                    {
                        _container._subscriptions = null;
                        return;
                    }

                    // single -> [] is one-way, we have read existing via
                    if (_container._subscriptions is ContainerSubscription[] subsArray)
                    {
                        for (int i = 0; i < subsArray.Length; i++)
                        {
                            if (ReferenceEquals(subsArray[i], this))
                            {
                                Volatile.Write(ref subsArray[i], null);
                                break;
                            }
                        }
                    }
                }
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                Dispose(true);
            }

            ~ContainerSubscription()
            {
                Trace.TraceWarning("Container subscription is finalized");
                Dispose(false);
            }
        }

        public IDisposable Subscribe(IAsyncCompletable subscriber)
        {
            AcquireLock();
            try
            {
                return DoSubscribe();
            }
            catch (Exception ex)
            {
                var message = "Error in ContainerSeries.Subscribe: " + ex;
                Trace.TraceError(message);
                ThrowHelper.FailFast(message);
                throw;
            }
            finally
            {
                ReleaseLock();
            }

            IDisposable DoSubscribe()
            {
                var subscription = new ContainerSubscription(this, subscriber);

                switch (_subscriptions)
                {
                    case null:
                        {
                            Interlocked.Exchange(ref _subscriptions, subscription);
                            break;
                        }

                    case ContainerSubscription sub when !ReferenceEquals(_subscriptions, subscription):
                        {
                            var newArr = new[] { sub, subscription };
                            Interlocked.Exchange(ref _subscriptions, newArr);
                            break;
                        }

                    case ContainerSubscription[] subsArray when Array.IndexOf(subsArray, subscription) < 0:
                        {
                            int i;
                            if ((i = Array.IndexOf(subsArray, null)) >= 0)
                            {
                                Volatile.Write(ref subsArray[i], subscription);
                            }
                            else
                            {
                                var newArr = new ContainerSubscription[subsArray.Length * 2];
                                subsArray.CopyTo(newArr, 0);
                                Interlocked.Exchange(ref _subscriptions, newArr);
                            }

                            break;
                        }

                    default:
                        {
                            // ignoring repeated subscription but it will be caught here
                            ThrowHelper.FailFast("_subscriptions could be either null, a single element or an array. (or repeated subscription)");
                            break;
                        }
                }

                return subscription;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyUpdate()
        {
            // inline only null check, DoNotifyUpdate is explicitly not inlined
            var subscriptions = _subscriptions;
            if (subscriptions == null)
            {
                return;
            }

            DoNotifyUpdate();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DoNotifyUpdate()
        {
            // Read the reference atomically and work with the local variable after.
            // No need to use lock in this method.

            var subscriptions = _subscriptions;

            switch (subscriptions)
            {
                case ContainerSubscription sub:
                    {
                        sub.Subscriber?.TryComplete(false);
                        break;
                    }

                case ContainerSubscription[] subsArray:
                    {
                        // We want to avoid a lock here for reading subsArray,
                        // which is modified only inside a lock in Subscribe/Dispose.
                        // Reference assignment is atomic, no synchronization is needed for subsArray.
                        // If we miss one that is being added concurrently then it was added after NotifyUpdate.
                        // We need to iterate over the entire array and not just until first null
                        // because removed subscribers could leave an empty slot.
                        // Async subscription should be rare and the number of subscribers is typically small.
                        for (int i = 0; i < subsArray.Length; i++)
                        {
                            var subI = Volatile.Read(ref subsArray[i]);
                            subI.Subscriber?.TryComplete(false);
                        }

                        break;
                    }

                default:
                    {
                        if (!(subscriptions is null))
                        {
                            ThrowHelper.FailFast("Wrong cursors subscriptions type");
                        }
                        else
                        {
                            ThrowHelper.FailFast("Cursors field is null, but that was checked in NotifyUpdate that calls this method");
                        }

                        break;
                    }
            }
        }

        #endregion Subscription & notification

        #region Attributes

        private static readonly ConditionalWeakTable<BaseContainer, Dictionary<string, object>> Attributes =
            new ConditionalWeakTable<BaseContainer, Dictionary<string, object>>();

        /// <summary>
        /// Get an attribute that was set using SetAttribute() method.
        /// </summary>
        /// <param name="attributeName">Name of an attribute.</param>
        /// <returns>Return an attribute value or null is the attribute is not found.</returns>
        public object? GetAttribute(string attributeName)
        {
            if (Attributes.TryGetValue(this, out Dictionary<string, object> dic))
            {
                lock (dic)
                {
                    if (dic.TryGetValue(attributeName, out object res))
                    {
                        return res;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Set any custom attribute to a series. An attribute is available during lifetime of a series and is available via GetAttribute() method.
        /// </summary>
        public void SetAttribute(string attributeName, object attributeValue)
        {
            // GetOrCreateValue is thread-safe
            var dic = Attributes.GetOrCreateValue(this);
            lock (dic)
            {
                dic[attributeName] = attributeValue;
            }
        }

        #endregion Attributes

        protected virtual void Dispose(bool disposing)
        {
        }

        internal bool IsDisposed => Data == null;

        public void Dispose()
        {
            Dispose(true);
            Data = null!;
            GC.SuppressFinalize(this);
        }

        ~BaseContainer()
        {
            // Containers are root objects that own data and usually are relatively long-lived.
            // Most containers use memory from some kind of a memory pool, including native
            // memory, and properly releasing that memory is important to avoid GC and high
            // peaks of memory usage.

            Trace.TraceWarning("Finalizing BaseContainer. This should not normally happen and containers should be explicitly disposed.");
            try
            {
                Dispose(false);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Exception during BaseContainer finalization: " + ex);
                throw;
            }
        }
    }
}
