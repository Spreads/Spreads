using System;
using System.Runtime.CompilerServices;
using Spreads.Threading;

namespace Spreads.Collections.Concurrent
{
    public sealed class MPMCPool<T> : PerCoreObjectPool<T, MPMCPoolCore<T>, MPMCPoolCoreWrapper<T>> where T : class
    {
        public MPMCPool(Func<T> factory, int perCoreSize, bool allocateOnEmpty = true, bool unbounded = false)
            : base(() => new RightPaddedLockedObjectPoolCore(factory, perCoreSize, allocateOnEmpty: false), allocateOnEmpty ? factory : () => null, unbounded)
        {
        }

        internal sealed class RightPaddedLockedObjectPoolCore : MPMCPoolCore<T>
        {
#pragma warning disable 169
            private readonly Padding64 _padding64;
            private readonly Padding32 _padding32;
#pragma warning restore 169

            public RightPaddedLockedObjectPoolCore(Func<T> factory, int size, bool allocateOnEmpty = true) : base(
                factory, size, allocateOnEmpty)
            {
            }
        }
    }

    public struct MPMCPoolCoreWrapper<T> : IObjectPoolWrapper<T, MPMCPoolCore<T>> where T : class
    {
        public MPMCPoolCore<T> Pool
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            set;
        }

        public void Dispose()
        {
            ((IDisposable) Pool).Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? Rent()
        {
            return Pool.Rent();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(T obj)
        {
            return Pool.Return(obj);
        }
    }

    public class MPMCPoolCore<T> : MPMCQueue, IObjectPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly bool _allocateOnEmpty;

        public MPMCPoolCore(Func<T> factory, int bufferSize, bool allocateOnEmpty = false) : base(bufferSize)
        {
            _factory = factory;
            _allocateOnEmpty = allocateOnEmpty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? Rent()
        {
            return Unsafe.As<T?>(Dequeue()) ?? CreateNew();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T? CreateNew()
        {
            // Methods that contain delegate invocation are not inlined.
            // Need to move factory invoke into separate non-inlined slow path.
            return _allocateOnEmpty ? _factory?.Invoke() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(T obj)
        {
            return Enqueue(obj);
        }

        public virtual void Dispose()
        {
        }
    }
}