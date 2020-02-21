using System;

namespace Spreads.Collections.Concurrent
{
    public interface IObjectPool<T> : IDisposable where T : class
    {
        T? Rent();
        bool Return(T obj);
    }
}