// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Collections.Concurrent
{
    public interface IPoolable<T> where T : class, new() {

        void Init();

        void Release();
    }

    public interface IObjectPool<T> where T : class, IPoolable<T>, new() {
        bool IsBounded { get; }
        int Capacity { get; }
        int Count { get; }

        /// <summary>
        /// Produces an instance.
        /// </summary>
        /// <remarks>
        /// Search strategy is a simple linear probing which is chosen for it cache-friendliness.
        /// Note that Free will try to store recycled objects close to the start thus statistically
        /// reducing how far we will typically search.
        /// </remarks>
        T Allocate();

        /// <summary>
        /// Returns objects to the pool.
        /// </summary>
        /// <remarks>
        /// Search strategy is a simple linear probing which is chosen for it cache-friendliness.
        /// Note that Free will try to store recycled objects close to the start thus statistically
        /// reducing how far we will typically search in Allocate.
        /// </remarks>
        void Free(T obj);
    }
}