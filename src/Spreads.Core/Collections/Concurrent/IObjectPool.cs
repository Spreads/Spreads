using System;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// An object pool interface. The <see cref="IDisposable.Dispose"/> implementation
    /// checks if every item stored in the pool implements <see cref="IDisposable"/>
    /// and disposes it. Note that when <see cref="Return"/> method returns false
    /// then an object is not disposed and a caller of that method must dispose the
    /// object if that is needed. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IObjectPool<T> : IDisposable where T : class
    {
        /// <summary>
        /// Returns an object from the pool. Could return null depending on implementation and configuration.
        /// </summary>
        /// <returns></returns>
        T? Rent();
        
        /// <summary>
        /// Returns true when an object was returned to the pool. Best-effort for non-locked <see cref="ObjectPool{T}"/>.
        /// </summary>
        bool Return(T obj);
    }
}