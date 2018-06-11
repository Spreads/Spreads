// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Spreads.Utils
{
    // That is probably subtly wrong, especially for the case from https://github.com/dotnet/corefx/issues/27445
    [Obsolete]
    public static class ValueTaskExtensions
    {
        [Obsolete]
        public static async ValueTask WhenAll<T>(this ValueTask<T>[] pending)
        {
            foreach (var task in pending)
            {
                if (!task.IsCompleted)
                {
                    try
                    {
                        await task;
                    }
                    catch { }
                }
            }
        }
    }

    internal class WhenAnyAwiter<T> : INotifyCompletion
    {
        private ValueTask<T> _first;
        private ValueTask<T> _second;

        public WhenAnyAwiter<T> GetAwaiter() => this;

        public bool IsCompleted => _first.IsCompleted || _second.IsCompleted;

        public void GetResult()
        {
        }

        public WhenAnyAwiter(ValueTask<T> first, ValueTask<T> second)
        {
            _first = first;
            _second = second;
        }

        private bool completed = false;

        public void OnCompleted(Action continuation)
        {
            try
            {
                
                _first.GetAwaiter().OnCompleted(() =>
                {
                    if (!completed)
                    {
                        completed = true;
                        try
                        {
                            continuation();
                        }
                        catch
                        {
                        }
                    }
                });
                _second.GetAwaiter().OnCompleted(() =>
                {
                    if (!completed)
                    {
                        try
                        {
                            continuation();
                        }
                        catch
                        {
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        //public void UnsafeOnCompleted(Action continuation)
        //{
        //    _first.GetAwaiter().UnsafeOnCompleted(continuation);
        //    _second.GetAwaiter().UnsafeOnCompleted(continuation);
        //}
    }

    internal class ReusableValueTaskWhenAny<T> : IValueTaskSource
    {
        private ValueTask<T> _first;
        private ValueTask<T> _second;
        private Action<object> _continuation;
        private object _state;
        private bool _completed;
        private short _version;

        // private Action _action;

        public ReusableValueTaskWhenAny()
        {
            // _action = RunContinuation;
        }

        public ValueTask WhenAny(ValueTask<T> first, ValueTask<T> second)
        {
            unchecked
            {
                _version++;
            }

            _first = first;
            _second = second;
            _state = null;
            _completed = false;
            Interlocked.Exchange(ref _continuation, null);
            return new ValueTask(this, _version);
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            try
            {
                if (_first.IsCompleted || _second.IsCompleted)
                {
                    if (_first.IsFaulted || _second.IsFaulted)
                    {
                        return ValueTaskSourceStatus.Faulted;
                    }

                    if (_first.IsCanceled || _second.IsCanceled)
                    {
                        return ValueTaskSourceStatus.Canceled;
                    }

                    return ValueTaskSourceStatus.Succeeded;
                }

                return ValueTaskSourceStatus.Pending;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return ValueTaskSourceStatus.Faulted;
        }

        private void RunContinuation()
        {
            var cont = _continuation;
            var st = _state;

            try
            {
                if (!_completed && cont != null)
                {
                    // _continuation = null;

                    _first = default;
                    _second = default;
                    _completed = true;
                    Interlocked.Exchange(ref _continuation, null);
                    _state = null;

                    // ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(cont), st);
                    cont(st);
                    // _continuation(_state);
                }
                else
                {
                    // Console.WriteLine("");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            try
            {
                _completed = false;
                //if (Volatile.Read(ref _continuation) != null)
                //{
                //    ThrowHelper.ThrowInvalidOperationException("Multiple continuations");
                //}

                // Interlocked.Exchange(ref _continuation, continuation);

                // _continuation = continuation;

                // _state = state;

                Action action = () =>
                {
                    if (!_completed)
                    {
                        continuation(state);
                    }
                };

                //if (GetStatus(token) != ValueTaskSourceStatus.Pending)
                //{
                //    RunContinuation();
                //}
                //else
                {
                    //if (!_first.IsCompleted)
                    {
                        _first.GetAwaiter().UnsafeOnCompleted(action);
                    }

                    // if (!_second.IsCompleted)
                    {
                        _second.GetAwaiter().UnsafeOnCompleted(action);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public void GetResult(short token)
        {
            //if (!_completed)
            //{
            //    ThrowHelper.ThrowInvalidOperationException();
            //}
        }
    }
}
