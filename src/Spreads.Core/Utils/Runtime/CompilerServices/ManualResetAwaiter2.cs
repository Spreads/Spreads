// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Experimental.Utils.Runtime.CompilerServices
{
    public sealed class ManualResetAwaiter2 : IAwaiter<bool>
    {
        private readonly bool _runContinuationsAsynchronously;

        // TODO if it ever works, use a field for a single continuation as a special case,
        // do not instantiate the queue every time - single is the hot case
        private readonly ConcurrentQueue<Action> _continuations = new ConcurrentQueue<Action>();

        private long _result;

        public ManualResetAwaiter2(bool runContinuationsAsynchronously = true)
        {
            _runContinuationsAsynchronously = runContinuationsAsynchronously;
        }

        public bool IsCompleted => Interlocked.Add(ref _result, 0L) == 1L;

        /// <summary>
        /// This could return false if result was set to false after we checked IsCompleted
        /// Consumers must check the returned value and retry if they received false
        /// </summary>
        /// <returns></returns>
        public bool GetResult()
        {
            long result = Interlocked.Add(ref _result, 0L);
            return result == 1L;
        }

        public bool SetResult(bool result)
        {
            var previous = Interlocked.Exchange(ref _result, result ? 1L : 0L);
            NotifyAwaiter(result);
            return previous == 1L;
        }

        private void NotifyAwaiter(bool result)
        {
            if (!result) return;
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

    internal class ManualResetAwaitable2
    {
        public readonly ManualResetAwaiter2 Awaiter = new ManualResetAwaiter2();

        public ManualResetAwaiter2 GetAwaiter()
        {
            return Awaiter;
        }
    }
}