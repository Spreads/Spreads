// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Experimental.Utils.Runtime.CompilerServices
{
    public sealed class ManualResetAwaiter : IAwaiter<bool>
    {
        private readonly bool _runContinuationsAsynchronously;

        // TODO if it ever works, use a field for a single continuation as a special case,
        // do not instantiate the queue every time - single is the hot case
        private readonly ConcurrentQueue<Action> _continuations = new ConcurrentQueue<Action>();

        private long _result;

        public ManualResetAwaiter(bool runContinuationsAsynchronously = true)
        {
            _runContinuationsAsynchronously = runContinuationsAsynchronously;
        }

        public bool IsCompleted => Volatile.Read(ref _result) > 0;

        /// <summary>
        /// This could return false if result was set to false after we checked IsCompleted
        /// Consumers must check the returned value and retry if they received false
        /// </summary>
        /// <returns></returns>
        public bool GetResult()
        {
            var result = Interlocked.Decrement(ref _result);
            if (result < 0)
            {
                Interlocked.Increment(ref _result);
                result = 0L;
            }
            return result > 0L;
        }

        public long SignalResult()
        {
            var result = Interlocked.Increment(ref _result);
            NotifyAwaiter(result);
            return result;
        }

        private void NotifyAwaiter(long result)
        {
            if (result == 0L) return;
            Action c;
            while (_continuations.TryDequeue(out c))
            {
                if (_runContinuationsAsynchronously)
                {
                    Task.Run(c);
                }
                else
                {
                    c();
                }
            }
        }

        public void OnCompleted(Action continuation)
        {
            if (IsCompleted)
            {
                Task.Run(continuation);
                return;
            }
            _continuations.Enqueue(continuation);
        }

        public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
    }

    internal class ManualResetAwaitable
    {
        public readonly ManualResetAwaiter Awaiter = new ManualResetAwaiter();

        public ManualResetAwaiter GetAwaiter()
        {
            return Awaiter;
        }
    }
}