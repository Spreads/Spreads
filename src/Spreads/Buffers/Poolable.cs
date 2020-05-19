using System;
using Spreads.Collections.Concurrent;

namespace Spreads.Buffers
{
    internal abstract class Poolable<T> : IDisposable where T : Poolable<T>, new()
    {
        public abstract void Clear();
        public abstract T Clone();

        public void Dispose()
        {
            Clear();
            if (this is T obj)
                Pool<T>.Return(obj);
        }
    }

    internal static class Pool<T> where T : Poolable<T>, new()
    {
        private static readonly ObjectPool<T> ObjectPool = new ObjectPool<T>(() => new T(), 16);

        public static T Rent()
        {
            return ObjectPool.Rent()!;
        }

        public static bool Return(T obj)
        {
            return ObjectPool.Return(obj);
        }
    }
}