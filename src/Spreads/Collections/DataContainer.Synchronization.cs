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
        /// <summary>
        /// Acquire a write lock and increment <seealso cref="NextOrderVersion"/>.
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
                        NextOrderVersion++;
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
        internal void TryUnlock()
        {
            ThrowHelper.FailFast("This should never happen. Locks are only in memory and should take less than a microsecond.");
        }

        /// <summary>
        /// Release write lock and increment <see cref="Version"/> or decrement <seealso cref="NextOrderVersion"/> if no updates were made.
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

                    NextOrderVersion = Version;

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
                NextOrderVersion = Version;
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
    }
}