// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Threading
{
    /// <summary>
    /// Helper methods for <see cref="SharedSpinLock"/> and <see cref="Wpid"/>.
    /// Thinks of this interface as a collection of delegates and not as an object.
    /// </summary>
    public interface IWpidHelper
    {
        /// <summary>
        /// <see cref="Wpid"/> of this helper.
        /// </summary>
        Wpid MyWpid { get; }

        /// <summary>
        /// Do not fail if we try to take lock while it is already taken by <see cref="Wpid"/> of this helper.
        /// If this property returns false we call <see cref="Environment.FailFast(string)"/> because it is wrong
        /// lock usage and not a recoverable exception. The true case simplifies some implementation on a cold path.
        /// </summary>
        [Obsolete("If this is still in use we should refactor to nevel allow reentrancy")]
        bool IgnoreReentrant { get; }

        /// <summary>
        /// Both <see cref="Wpid.Pid"/> and <see cref="Wpid.InstanceId"/> must be alive.
        /// <see cref="Wpid.Pid"/> is checked with <see cref="Process.GetProcessById(int)"/>
        /// and <see cref="Wpid.InstanceId"/> is checked according to some application logic.
        /// </summary>
        /// <remarks>
        /// It should not be possible that InstanceId is alive while Pid is not, while the same
        /// system process could have multiple logical instances. If Pid is dead then we could
        /// return false immediately. If Pid is alive but InstanceId is not then we should wait longer.
        /// Check should be something like:
        /// 1. Return true if InstanceId heartbeat is less then a small limit. This should be a very fast check.
        /// 2. Consult <see cref="Process.GetProcessById(int)"/> to check if Pid part of <paramref name="wpid"/> is alive.
        ///    Return false if not (additionally mark logical instance as dead to help other processes and speed up clean up).
        /// 3. Repeat 1-2 up to some counter limit so that total time account for any realistic delay in heartbeat update,
        ///    e.g. assume that heartbeat is updated by a lower-priority thread than other threads that are occupying 100% of CPU.
        /// </remarks>
        bool IsWpidAlive(Wpid wpid);

        /// <summary>
        /// Declare logical instance of this helper as dead.
        /// <see cref="IsWpidAlive"/> should always return <see langword="false" /> for <see cref="Wpid"/> of this helper.
        /// </summary>
        void Suicide();

        /// <summary>
        /// Notify that <see cref="SharedSpinLock"/> has detected dead lock holder and unlocked it.
        /// </summary>
        /// <param name="wpid"></param>
        void OnForceUnlock(Wpid wpid);
    }

    /// <summary>
    /// Wide process id: system process id + logical instance id.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    public readonly struct Wpid : IEquatable<Wpid>
    {
        public static readonly Wpid Empty;

        private static int InstanceCounter;

        private static readonly long _pid = Process.GetCurrentProcess().Id;

        /// <summary>
        /// More than uint.MaxValue and keeps Pid human readable from raw value. Mul/Mod are never in the hot path so keep it simple.
        /// </summary>
        private const long PidMultiple = 10_000_000_000L;

        /// <summary>
        /// We need long and Int32 InstanceId because it is persistent and unique among running processes.
        /// It could wrap around after int32.MaxValue process starts, or approximately a month if processes
        /// start every 1 millisecond (or 7 years if processes start every 100 milliseconds).
        /// Additionally one could have some logic to avoid creating Wpid for a live instance (e.g. <see cref="IWpidHelper.IsWpidAlive"/>).
        ///
        /// <para />
        ///
        /// This is very important for Docker without pid==host, where Pid==1 for all containers and the only way to separate processes in InstanceId.
        /// </summary>
        private readonly long _value;

        public static Wpid Create()
        {
            var instanceId = unchecked((uint)Interlocked.Increment(ref InstanceCounter));
            return new Wpid(instanceId);
        }

        public static Wpid Create(uint instanceId)
        {
            return new Wpid(instanceId);
        }

        [DebuggerStepThrough]
        private Wpid(uint instanceId)
        {
            _value = _pid * PidMultiple + instanceId;
        }

        public int Pid => ((int)(_value / PidMultiple));
        public uint InstanceId => ((uint)(_value % PidMultiple));

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe implicit operator long(Wpid id)
        {
            return *(long*)&id;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe explicit operator Wpid(long value)
        {
            return *(Wpid*)&value;
        }

        [DebuggerStepThrough]
        public static bool operator ==(Wpid id1, Wpid id2)
        {
            return id1.Equals(id2);
        }

        [DebuggerStepThrough]
        public static bool operator !=(Wpid id1, Wpid id2)
        {
            return !id1.Equals(id2);
        }

        [DebuggerStepThrough]
        public bool Equals(Wpid other)
        {
            return _value == other._value;
        }

        [DebuggerStepThrough]
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Wpid wpid && Equals(wpid);
        }

        [DebuggerStepThrough]
        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        [DebuggerStepThrough]
        public override string ToString()
        {
            return $"Pid={Pid}, InstanceId={InstanceId}";
        }
    }

    /// <summary>
    /// TAS lock over (potentially) shared memory.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "}")]
    public readonly struct SharedSpinLock
    {
        // 1. At first spin normally, locks should not be contented in DataSpreads
        // 2. After "quite small" number of retries assume that the lock holder
        //    is blocked or preempted. Try to flip the sign of wpid and continue.
        // 2.1. If we changed the sign then keep spinning, reset count
        // 2.2. If the sign was already negative, back-off. This means that
        //      the original lock was taken for a while and someone else wanted
        //      it for a while and the first waiter changed the sign.
        //      Probabilistically the first waiter changes the sign first unless preempted.
        // 2.2.1. Call Thread.Yield ?? Thread.Sleep(1) if the wpid is negative after CAS attempt, do not spin
        // 2.2.2. When wpid is positive reset counters and go to 1.
        // 3. After long enough attempts check if lock holder is alive and unlock if not.
        // 4. Die if spinning very very long. This should not happen with correct usage.
        // 5. If LocalMulticast is supported (not nul) then wait on a lock-holder-specific
        //    semaphore instead of just yielding.
        // This scheme should be on average fair to the first waiter.
        // New waiters should see negative wpid and go to 2.2.
        // If Yield/Sleep are equal on average then there should be some probabilistic fairness:
        // the first backed-off process will start actively spinning first and will reach the "quite small"
        // number of attempts first and it will change the sign of the new lock holder first. Then the logic repeats.

        // The idea with yield notification is that if we are spinning more than quite smallish number
        // such as 5 in this case, then we already have some contention that should not happen
        // with DataSpreads design that prefers a single writer per stream. On hyper threaded CPUs
        // with small number of cores excessive spinning is very unproductive and cores are very scarce
        // resource, so it's better to yield since we will yield/switch to another thread anyway sooner or
        // later by OS thread scheduler. Stress test with i3 instance with 2 vCPUs (HT) showed
        // that this is beneficial in contended case and by construction does not affect uncontended one.
        // Without it CPU load is only 50% with sum close to 100%, with it it goes to 90%+/180%+.

        // From SpinWait
        // internal const int YieldThreshold = 10; // When to switch over to a true yield.
        // private const int Sleep0EveryHowManyYields = 5; // After how many yields should we Sleep(0)?
        // internal const int DefaultSleep1Threshold = 20; // After how many yields should we Sleep(1) frequently?

        // Internal for tests only

        internal static int PriorityThreshold = 5;
        internal static int UnlockCheckThreshold = 500; // 500 is around 1 second
        internal static int DeadLockThreshold = 200_000; // 100_000 is 187 seconds on i7-8700

        private const long ThreadIdTagMask = (long)0b_0111_1111 << 56;

        // normally Wpid number is very limited, 2-20 should be normal range
        internal static (long, SemaphoreSlim)[] _semaphores = new (long, SemaphoreSlim)[32];

        internal static long SemaphoreReleaseCount;
        internal static long SemaphoreTimeoutCount;

        internal static LocalMulticast<long> _multicast =
            Settings.SharedSpinLockNotificationPort > 0
            ?
            new LocalMulticast<long>(Settings.SharedSpinLockNotificationPort, (wpid) =>
            {
                // Console.WriteLine($"Received nitification for {wpid}");
                var sem = GetSemaphore(wpid);
                sem.Release();
            })
            : null;

        internal readonly IntPtr Pointer;

        public unsafe SharedSpinLock(long* pointer)
        {
            Pointer = (IntPtr)pointer;
        }

        public SharedSpinLock(IntPtr pointer)
        {
            Pointer = pointer;
        }

        internal unsafe Wpid LockHolder
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Wpid)Volatile.Read(ref *(long*)(Pointer));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long WpidToLockValue(Wpid wpid)
        {
            // effectively we assume Pid is always smaller that 2 ^ 24
            // TODO review: could use InstanceId high bits instead, but we could find Pid by instance id and instance id is mor important in general
            Debug.Assert((wpid & ThreadIdTagMask) == 0);
            return (((long) Thread.CurrentThread.ManagedThreadId & 127) << 56) | (~ThreadIdTagMask & wpid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Wpid LockValueToWpid(long lockValue)
        {
            return (Wpid)(Math.Abs(lockValue) & ~ThreadIdTagMask);
        }

        /// <summary>
        /// Returns zero if acquired lock or Wpid of existing lock holder.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Wpid TryAcquireLock(Wpid wpid, int spinLimit = 0, IWpidHelper wpidHelper = null)
        {
            var lockValue = WpidToLockValue(wpid);

            // TTAS significantly slower for uncontended case, which is often the case, do not check: 0 == *(long*)Pointer &&
            if (0 == Interlocked.CompareExchange(ref *(long*)Pointer, lockValue, 0))
            {
                return default;
            }
            return TryAcquireLockContended(lockValue, spinLimit, wpidHelper);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization // first call could be problematic with the loop if tiered compilation is on
#endif
        )]
        private unsafe Wpid TryAcquireLockContended(long wpid, int spinLimit, IWpidHelper wpidHelper)
        {
            DateTime backOffStarted = default;
            SemaphoreSlim sem = null;

            // true if this loop flipped the sign of existing
            var priority = false;

            // Let SpinWait do it's well tuned job, do not reset or try to outsmart.
            // Just jump ahead of it once to get priority and start over.
            var sw = new SpinWait();

            var counter = 0;

            while (true)
            {
                sw.SpinOnce();

                long existing;
                // if (0 == (existing = *(long*)(Pointer))) // TTAS doesn't help here either
                {
                    existing = Interlocked.CompareExchange(ref *(long*)(Pointer), wpid, 0);
                }

                if (existing == 0)
                {
                    return default;
                }

#pragma warning disable 618
                if (existing == wpid && wpidHelper?.IgnoreReentrant != true)
#pragma warning restore 618
                {
                    // Unexpected reentrancy is wrong usage and not an exception. User code is wrong.
                    ThrowHelper.FailFast("Lock reentry is not supported");
                }

                // needs to be above counter = UnlockCheckThreshold; so that counter % UnlockCheckThreshold == 0
                counter++;

                // 2.2.1.
                if (existing < 0 && !priority)
                {
                    // Console.WriteLine("BO");
                    if (backOffStarted == default)
                    {
                        backOffStarted = DateTime.Now;
                    }

                    // back off, others are waiting, don't waste CPU and cache
                    if (!Thread.Yield())
                    {
                        Thread.Sleep(0); // sw will Sleep(1) if needed
                    }

                    if (DateTime.Now - backOffStarted < TimeSpan.FromSeconds(1))
                    {
                        continue;
                    }
                    // We waited for entire second, maybe priority waiter had some problems.
                    // Become a normal waiter and try to do checks.
                    backOffStarted = default;
                    counter = UnlockCheckThreshold;
                }

                // 2.2.2. We were waiting but existing has become positive, priority waiter took the lock and we are waiting for it now.
                if (backOffStarted != default)
                {
                    Debug.Assert(existing > 0); // checks are above
                    backOffStarted = default;
                    // Start anew, go to 1.
                    counter = PriorityThreshold; // will try to get priority
                }

                if (counter >= PriorityThreshold)
                {
                    if (!priority && existing > 0)
                    {
                        // first waiter that reached here is the first that started, no yields before PriorityThreshold,
                        // only preemption could have kicked it out before trying to acquire priority.
                        var replaced = Interlocked.CompareExchange(ref *(long*)(Pointer), -existing, existing);
                        if (replaced == existing)
                        {
                            priority = true;
                        }
                        //else
                        //{
                        //    // Someone took the lock, but we should have been the first
                        //    // try to regain priority quickly without SpinOnce.
                        //    // Also if we are from 2.2.1. after backing off we deserve priority.
                        //    continue; // TODO review
                        //}
                    }

                    // TODO add stopwatch if was waiting via semaphore
                    if (counter % UnlockCheckThreshold == 0) // Try force unlock
                    {
                        if (wpidHelper != null && !wpidHelper.IsWpidAlive((Wpid)Math.Abs(existing)))
                        {
                            var replaced = Interlocked.CompareExchange(ref *(long*)(Pointer), wpid, existing);
                            if (replaced == existing)
                            {
                                wpidHelper.OnForceUnlock((Wpid)Math.Abs(existing));
                                return default;
                            }
                        }

                        if (counter > DeadLockThreshold)
                        {
                            if (wpidHelper != null)
                            {
                                wpidHelper.Suicide();
                            }
                            else
                            {
                                ThrowHelper.FailFast("Deadlock");
                            }
                        }
                    }
                }

                if (priority && sw.NextSpinWillYield && _multicast != null)
                {
                    sem = sem ?? GetSemaphore((Wpid)Math.Abs(existing));

                    existing = Interlocked.CompareExchange(ref *(long*)(Pointer), wpid, 0);
                    if (existing == 0)
                    {
                        return default;
                    }

                    if (existing < 0)
                    {
                        if (sem.Wait(40))
                        {
                            // Console.WriteLine("Entered semaphore");
                            Interlocked.Increment(ref SemaphoreReleaseCount);
                        }
                        else
                        {
                            //Console.WriteLine("Semaphore timeout");
                            Interlocked.Increment(ref SemaphoreTimeoutCount);
                        }
                    }
                    else
                    {
                        // Console.WriteLine("Positive existing with priority: " + existing);
                    }
                }

                // Note: not counter, we sometimes set it to a special value.
                if (spinLimit > 0 && sw.Count > spinLimit)
                {
                    return (Wpid)Math.Abs(existing);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ValueTask<Wpid> TryAcquireLockAsync(Wpid wpid, int spinLimit = 0, IWpidHelper wpidHelper = null)
        {
            var lockValue = WpidToLockValue(wpid);

            if (0 == Interlocked.CompareExchange(ref *(long*)Pointer, lockValue, 0))
            {
                return new ValueTask<Wpid>(default(Wpid));
            }

            return TryAcquireLockContendedAsync(lockValue, spinLimit, wpidHelper);
        }

        private unsafe ref long PointerAsRef()
        {
            return ref *(long*)Pointer;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization // first call could be problematic with the loop if tiered compilation is on
#endif
        )]
        private async ValueTask<Wpid> TryAcquireLockContendedAsync(long wpid, int spinLimit, IWpidHelper wpidHelper)
        {
            DateTime backOffStarted = default;
            SemaphoreSlim sem = null;

            // true if this loop flipped the sign of existing
            var priority = false;

            // Let SpinWait do it's well tuned job, do not reset or try to outsmart.
            // Just jump ahead of it once to get priority and start over.
            var sw = new SpinWait();

            var counter = 0;

            while (true)
            {
                sw.SpinOnce();
                
                long existing;
                // if (0 == (existing = *(long*)(Pointer))) // TTAS doesn't help here either
                {
                    existing = Interlocked.CompareExchange(ref PointerAsRef(), wpid, 0);
                }

                if (existing == 0)
                {
                    return default;
                }

#pragma warning disable 618
                if (existing == wpid && wpidHelper?.IgnoreReentrant != true)
#pragma warning restore 618
                {
                    // Unexpected reentrancy is wrong usage and not an exception. User code is wrong.
                    ThrowHelper.FailFast("Lock reentry is not supported");
                }

                // needs to be above counter = UnlockCheckThreshold; so that counter % UnlockCheckThreshold == 0
                counter++;

                // 2.2.1.
                if (existing < 0 && !priority)
                {
                    // Console.WriteLine("BO");
                    if (backOffStarted == default)
                    {
                        backOffStarted = DateTime.Now;
                    }

                    // back off, others are waiting, don't waste CPU and cache
                    await Task.Delay(1);

                    if (DateTime.Now - backOffStarted < TimeSpan.FromSeconds(1))
                    {
                        continue;
                    }
                    // We waited for entire second, maybe priority waiter had some problems.
                    // Become a normal waiter and try to do checks.
                    backOffStarted = default;
                    counter = UnlockCheckThreshold;
                }

                // 2.2.2. We were waiting but existing has become positive, priority waiter took the lock and we are waiting for it now.
                if (backOffStarted != default)
                {
                    Debug.Assert(existing > 0); // checks are above
                    backOffStarted = default;
                    // Start anew, go to 1.
                    counter = PriorityThreshold; // will try to get priority
                }

                if (counter >= PriorityThreshold)
                {
                    if (!priority && existing > 0)
                    {
                        // first waiter that reached here is the first that started, no yields before PriorityThreshold,
                        // only preemption could have kicked it out before trying to acquire priority.
                        var replaced = Interlocked.CompareExchange(ref PointerAsRef(), -existing, existing);
                        if (replaced == existing)
                        {
                            priority = true;
                        }
                        //else
                        //{
                        //    // Someone took the lock, but we should have been the first
                        //    // try to regain priority quickly without SpinOnce.
                        //    // Also if we are from 2.2.1. after backing off we deserve priority.
                        //    continue; // TODO review
                        //}
                    }

                    // TODO add stopwatch if was waiting via semaphore
                    if (counter % UnlockCheckThreshold == 0) // Try force unlock
                    {
                        if (wpidHelper != null && !wpidHelper.IsWpidAlive((Wpid)Math.Abs(existing)))
                        {
                            var replaced = Interlocked.CompareExchange(ref PointerAsRef(), wpid, existing);
                            if (replaced == existing)
                            {
                                wpidHelper.OnForceUnlock((Wpid)Math.Abs(existing));
                                return default;
                            }
                        }

                        //if (counter > DeadLockThreshold)
                        //{
                        //    if (wpidHelper != null)
                        //    {
                        //        wpidHelper.Suicide();
                        //    }
                        //    else
                        //    {
                        //        ThrowHelper.FailFast("Deadlock");
                        //    }
                        //}
                    }
                }

                if (priority && sw.NextSpinWillYield)
                {
                    if (_multicast != null)
                    {
                        sem = sem ?? GetSemaphore((Wpid) Math.Abs(existing));
                        if (backOffStarted == default)
                        {
                            backOffStarted = DateTime.Now;
                        }

                        if (await sem.WaitAsync(40))
                        {
                            // Console.WriteLine("Entered semaphore");
                        }
                        else
                        {
                            //Console.WriteLine("Semaphore timeout");
                            Interlocked.Increment(ref SemaphoreTimeoutCount);
                        }
                    }
                    else
                    {
                        // what if we just sleep?
                        // Thread.Sleep(1);
                    }
                }

                // Note: not counter, we sometimes set it to a special value.
                if (spinLimit > 0 && sw.Count > spinLimit)
                {
                    return (Wpid)Math.Abs(existing);
                }
            }
        }

        /// <summary>
        /// Returns zero if released lock.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe Wpid TryReleaseLock(Wpid wpid)
        {
            var lockValue = WpidToLockValue(wpid);

            if (lockValue == Interlocked.CompareExchange(ref *(long*)(Pointer), 0, lockValue))
            {
                return default;
            }

            return TryReleaseLockContended(wpid);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private unsafe Wpid TryReleaseLockContended(Wpid wpid)
        {
            var lockValue = WpidToLockValue(wpid);
            long existing = *(long*)(Pointer);
            if (LockValueToWpid(existing) == wpid &&
                Interlocked.CompareExchange(ref *(long*)(Pointer), 0, existing) == existing)
            {
                _multicast?.Send(lockValue);
                return default;
            }

            return LockValueToWpid(lockValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SemaphoreSlim GetSemaphore(long wpid)
        {
            foreach (var pair in _semaphores)
            {
                if (wpid == pair.Item1)
                {
                    return pair.Item2;
                }
            }

            return GetSemaphoreCold(wpid);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static SemaphoreSlim GetSemaphoreCold(long wpid)
        {
            while (true)
            {
                var arr = _semaphores;
                lock (arr)
                {
                    if (arr != _semaphores)
                    {
                        continue;
                    }

                    // try again
                    var i = 0;
                    for (; i < _semaphores.Length; i++)
                    {
                        var pair = _semaphores[i];
                        if (wpid == pair.Item1)
                        {
                            return pair.Item2;
                        }

                        if (pair.Item1 == default)
                        {
                            break;
                        }
                    }

                    if (i == _semaphores.Length)
                    {
                        var newArr = new (long, SemaphoreSlim)[_semaphores.Length * 2];
                        Array.Copy(_semaphores, newArr, _semaphores.Length);
                        // reference assignment is atomic, readers will scan
                        // the old one, go to the cold path, wait for us to finish
                        // and will retry scan
                        _semaphores = newArr;
                    }

                    var sem = new SemaphoreSlim(0, int.MaxValue);
                    _semaphores[i] = (wpid, sem);

                    return sem;
                }
            }
        }
    }
}
