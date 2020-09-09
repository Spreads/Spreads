// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Collections
{
    internal sealed partial class DataContainer
    {
        // Union of ContainerSubscription | ThreadSafeList<ContainerSubscription> (ThreadSafeList could be implemented differently)
        private object? _subscriptions;

        private class ContainerSubscription : IAsyncSubscription
        {
            private readonly DataContainer _container;
            public readonly IAsyncCompletable? Subscriber;

            public ContainerSubscription(DataContainer container, IAsyncCompletable subscriber)
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

            public IAsyncCompletable AwaitingCompletable
            {
                set => throw new NotImplementedException();
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

            IAsyncSubscription DoSubscribe()
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

            if (subscriptions is ContainerSubscription sub)
            {
                sub.Subscriber?.TryComplete(false);
            }
            else if (subscriptions is ContainerSubscription[] subsArray)
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
            }
            else
            {
                var errMsg = subscriptions is null
                    ? "Cursors field is null, but that was checked in NotifyUpdate that calls this method"
                    : "Wrong cursors subscriptions type";
                Environment.FailFast(errMsg);
            }
        }
    }
}