// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Threading.Tasks.Channels;

namespace Spreads.Threading.Tasks.Channels {
    public static partial class Channel {
        /// <summary>Provides an async enumerator for the data in a channel.</summary>
        private sealed class AsyncEnumerator<T> : IAsyncEnumerator<T> {
            /// <summary>The channel being enumerated.</summary>
            private readonly IReadableChannel<T> _channel;

            /// <summary>The current element of the enumeration.</summary>
            private T _current;

            internal AsyncEnumerator(IReadableChannel<T> channel) {
                _channel = channel;
            }

            public Task<bool> MoveNext(CancellationToken cancellationToken) {
                var result = _channel.ReadAsync(cancellationToken);

                if (result.IsCompletedSuccessfully) {
                    _current = result.Result;
                    return s_trueTask;
                }

                return result.AsTask().ContinueWith((t, s) => {
                    AsyncEnumerator<T> thisRef = (AsyncEnumerator<T>)s;
                    try {
                        thisRef._current = t.GetAwaiter().GetResult();
                        return true;
                    } catch (ClosedChannelException) {
                        return false;
                    }
                }, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            }

            public bool MoveNext() {
                T tmp;
                if (!_channel.TryRead(out tmp)) return false;
                _current = tmp;
                return true;
            }

            public void Reset() {
                // noop
            }

            object IEnumerator.Current => Current;

            public T Current => _current;

            public void Dispose() {
                // noop
            }
        }
    }
}
