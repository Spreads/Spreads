/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/


using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
#if DEBUG
using System.Collections.Concurrent;
using System.Globalization;
using System.Security;

#endif //DEBUG
// ReSharper disable once CheckNamespace
namespace System.Runtime {
	internal abstract class InternalBufferManager<T> {
		protected InternalBufferManager() {
		}

		public abstract T[] TakeBuffer(int bufferLength);
		public abstract void ReturnBuffer(T[] buffer);
		public abstract void Clear();

		public static InternalBufferManager<T> Create(long maxBufferPoolSize, int maxBufferSize) {
			if (maxBufferPoolSize == 0) {
				return GCBufferManager<T>.Value;
			} else {
				Trace.Assert(maxBufferPoolSize > 0 && maxBufferSize >= 0, "bad params, caller should verify");
				return new PooledBufferManager(maxBufferPoolSize, maxBufferSize);
			}
		}

		internal class PooledBufferManager : InternalBufferManager<T> {
			private const int minBufferSizeInBytes = 128;
			private const int maxMissesBeforeTuning = 8;
			private const int initialBufferCount = 1;
			private readonly object _tuningLock;

			private int[] _bufferLengthes;
			private BufferPool[] _bufferPools;
			private long _memoryInBytesLimit;
			private long _remainingMemoryInBytes;
			private bool _areQuotasBeingTuned;
			private int _totalMisses;
#if DEBUG && !FEATURE_NETNATIVE
			private ConcurrentDictionary<int, string> _buffersPooled = new ConcurrentDictionary<int, string>();
#endif //DEBUG

			public PooledBufferManager(long maxMemoryInBytesToPool, int maxBufferSizeInBytes) {
				_tuningLock = new object();
				_memoryInBytesLimit = maxMemoryInBytesToPool;
				_remainingMemoryInBytes = maxMemoryInBytesToPool;
				List<BufferPool> bufferPoolList = new List<BufferPool>();

				for (int bufferSizeInBytes = minBufferSizeInBytes; ;) {
					long bufferCountLong = _remainingMemoryInBytes / bufferSizeInBytes;

					int bufferCount = bufferCountLong > int.MaxValue ? int.MaxValue : (int)bufferCountLong;

					if (bufferCount > initialBufferCount) {
						bufferCount = initialBufferCount;
					}

					bufferPoolList.Add(BufferPool.CreatePool(bufferSizeInBytes, bufferCount));

					_remainingMemoryInBytes -= (long)bufferCount * bufferSizeInBytes;

					if (bufferSizeInBytes >= maxBufferSizeInBytes) {
						break;
					}

					long newBufferSizeLong = (long)bufferSizeInBytes * 2;

					if (newBufferSizeLong > (long)maxBufferSizeInBytes) {
						bufferSizeInBytes = maxBufferSizeInBytes;
					} else {
						bufferSizeInBytes = (int)newBufferSizeLong;
					}
				}

				_bufferPools = bufferPoolList.ToArray();
				_bufferLengthes = new int[_bufferPools.Length];
				for (int i = 0; i < _bufferPools.Length; i++) {
					_bufferLengthes[i] = _bufferPools[i].BufferSize;
				}
			}

			public override void Clear() {
#if DEBUG && !FEATURE_NETNATIVE
				_buffersPooled.Clear();
#endif //DEBUG

				for (int i = 0; i < _bufferPools.Length; i++) {
					BufferPool bufferPool = _bufferPools[i];
					bufferPool.Clear();
				}
			}

			private void ChangeQuota(ref BufferPool bufferPool, int delta) {
				// TODO tracing
				//if (TraceCore.BufferPoolChangeQuotaIsEnabled(Fx.Trace)) {
				//	TraceCore.BufferPoolChangeQuota(Fx.Trace, bufferPool.BufferSize, delta);
				//}

				BufferPool oldBufferPool = bufferPool;
				int newLimit = oldBufferPool.Limit + delta;
				BufferPool newBufferPool = BufferPool.CreatePool(oldBufferPool.BufferSize, newLimit);
				for (int i = 0; i < newLimit; i++) {
					T[] buffer = oldBufferPool.Take();
					if (buffer == null) {
						break;
					}
					newBufferPool.Return(buffer);
					newBufferPool.IncrementCount();
				}
				_remainingMemoryInBytes -= oldBufferPool.BufferSize * delta;
				bufferPool = newBufferPool;
			}

			private void DecreaseQuota(ref BufferPool bufferPool) {
				ChangeQuota(ref bufferPool, -1);
			}

			private int FindMostExcessivePool() {
				long maxBytesInExcess = 0;
				int index = -1;

				for (int i = 0; i < _bufferPools.Length; i++) {
					BufferPool bufferPool = _bufferPools[i];

					if (bufferPool.Peak < bufferPool.Limit) {
						long bytesInExcess = (bufferPool.Limit - bufferPool.Peak) * (long)bufferPool.BufferSize;

						if (bytesInExcess > maxBytesInExcess) {
							index = i;
							maxBytesInExcess = bytesInExcess;
						}
					}
				}

				return index;
			}

			private int FindMostStarvedPool() {
				long maxBytesMissed = 0;
				int index = -1;

				for (int i = 0; i < _bufferPools.Length; i++) {
					BufferPool bufferPool = _bufferPools[i];

					if (bufferPool.Peak == bufferPool.Limit) {
						long bytesMissed = bufferPool.Misses * (long)bufferPool.BufferSize;

						if (bytesMissed > maxBytesMissed) {
							index = i;
							maxBytesMissed = bytesMissed;
						}
					}
				}

				return index;
			}

			private BufferPool FindPool(int desiredBufferLength) {
				for (int i = 0; i < _bufferLengthes.Length; i++) {
					if (desiredBufferLength <= _bufferLengthes[i]) {
						return _bufferPools[i];
					}
				}

				return null;
			}

			private void IncreaseQuota(ref BufferPool bufferPool) {
				ChangeQuota(ref bufferPool, 1);
			}

			public override void ReturnBuffer(T[] buffer) {
				Trace.Assert(buffer != null, "Return buffer is null");


				BufferPool bufferPool = FindPool(buffer.Length);
				if (bufferPool != null) {
					if (buffer.Length != bufferPool.BufferSize) {
						throw new ArgumentException("Buffer Is Not Right Size For Buffer Manager", nameof(buffer));
					}

					if (bufferPool.Return(buffer)) {
						bufferPool.IncrementCount();
					}
				}
			}

			public override T[] TakeBuffer(int bufferLength) {
				Trace.Assert(bufferLength >= 0, "caller must ensure a non-negative argument");

				var bufferPool = FindPool(bufferLength);
				T[] returnValue;
				if (bufferPool != null) {
					var buffer = bufferPool.Take();
					if (buffer != null) {
						bufferPool.DecrementCount();
						returnValue = buffer;
					} else {
						if (bufferPool.Peak == bufferPool.Limit) {
							bufferPool.Misses++;
							if (++_totalMisses >= maxMissesBeforeTuning) {
								TuneQuotas();
							}
						}

						// TODO tracing
						//if (TraceCore.BufferPoolAllocationIsEnabled(Fx.Trace)) {
						//	TraceCore.BufferPoolAllocation(Fx.Trace, bufferPool.BufferSize);
						//}

						returnValue = new T[bufferPool.BufferSize];
					}
				} else {
					// TODO tracing
					//if (TraceCore.BufferPoolAllocationIsEnabled(Fx.Trace)) {
					//	TraceCore.BufferPoolAllocation(Fx.Trace, bufferSizeInBytes);
					//}

					returnValue = new T[bufferLength];
				}

#if DEBUG && !FEATURE_NETNATIVE
				string dummy;
				_buffersPooled.TryRemove(returnValue.GetHashCode(), out dummy);
#endif //DEBUG

				return returnValue;
			}


			private void TuneQuotas() {
				if (_areQuotasBeingTuned) {
					return;
				}

				bool lockHeld = false;
				try {
					Monitor.TryEnter(_tuningLock, ref lockHeld);

					// Don't bother if another thread already has the lock
					if (!lockHeld || _areQuotasBeingTuned) {
						return;
					}

					_areQuotasBeingTuned = true;
				} finally {
					if (lockHeld) {
						Monitor.Exit(_tuningLock);
					}
				}

				// find the "poorest" pool
				int starvedIndex = FindMostStarvedPool();
				if (starvedIndex >= 0) {
					BufferPool starvedBufferPool = _bufferPools[starvedIndex];

					if (_remainingMemoryInBytes < starvedBufferPool.BufferSize) {
						// find the "richest" pool
						int excessiveIndex = FindMostExcessivePool();
						if (excessiveIndex >= 0) {
							// steal from the richest
							DecreaseQuota(ref _bufferPools[excessiveIndex]);
						}
					}

					if (_remainingMemoryInBytes >= starvedBufferPool.BufferSize) {
						// give to the poorest
						IncreaseQuota(ref _bufferPools[starvedIndex]);
					}
				}

				// reset statistics
				for (int i = 0; i < _bufferPools.Length; i++) {
					BufferPool bufferPool = _bufferPools[i];
					bufferPool.Misses = 0;
				}

				_totalMisses = 0;
				_areQuotasBeingTuned = false;
			}

			internal abstract class BufferPool {
				private int _bufferSize;
				private int _count;
				private int _limit;
				private int _misses;
				private int _peak;

				public BufferPool(int bufferSize, int limit) {
					_bufferSize = bufferSize;
					_limit = limit;
				}

				public int BufferSize {
					get { return _bufferSize; }
				}

				public int Limit {
					get { return _limit; }
				}

				public int Misses {
					get { return _misses; }
					set { _misses = value; }
				}

				public int Peak {
					get { return _peak; }
				}

				public void Clear() {
					this.OnClear();
					_count = 0;
				}

				public void DecrementCount() {
					int newValue = _count - 1;
					if (newValue >= 0) {
						_count = newValue;
					}
				}

				public void IncrementCount() {
					int newValue = _count + 1;
					if (newValue <= _limit) {
						_count = newValue;
						if (newValue > _peak) {
							_peak = newValue;
						}
					}
				}

				internal abstract T[] Take();
				internal abstract bool Return(T[] buffer);
				internal abstract void OnClear();

				internal static BufferPool CreatePool(int bufferSizeInBytes, int limit) {
                    // To avoid many buffer drops during training of large objects which
                    // get allocated on the LOH, we use the LargeBufferPool and for 
                    // bufferSize < 85000, the SynchronizedPool. However if bufferSize < 85000
                    // and (bufferSize + array-overhead) > 85000, this would still use 
                    // the SynchronizedPool even though it is allocated on the LOH.
                    if (bufferSizeInBytes < 85000) {
                        return new SynchronizedBufferPool(bufferSizeInBytes, limit);
                    } else {
                        return new LargeBufferPool(bufferSizeInBytes, limit);
                    }
                }

				internal class SynchronizedBufferPool : BufferPool {
					private SynchronizedPool<T[]> _innerPool;

					internal SynchronizedBufferPool(int bufferSize, int limit)
						: base(bufferSize, limit) {
						_innerPool = new SynchronizedPool<T[]>(limit);
					}

					internal override void OnClear() {
						_innerPool.Clear();
					}

					internal override T[] Take() {
						return _innerPool.Take();
					}

					internal override bool Return(T[] buffer) {
						return _innerPool.Return(buffer);
					}
				}

				internal class LargeBufferPool : BufferPool {
					private Stack<T[]> _items;

					internal LargeBufferPool(int bufferSize, int limit)
						: base(bufferSize, limit) {
						_items = new Stack<T[]>(limit);
					}

					private object ThisLock {
						get {
							return _items;
						}
					}

					internal override void OnClear() {
						lock (ThisLock) {
							_items.Clear();
						}
					}

					internal override T[] Take() {
						lock (ThisLock) {
							if (_items.Count > 0) {
								return _items.Pop();
							}
						}

						return null;
					}

					internal override bool Return(T[] buffer) {
						lock (ThisLock) {
							if (_items.Count < this.Limit) {
								_items.Push(buffer);
								return true;
							}
						}

						return false;
					}
				}
			}
		}

		internal class GCBufferManager<T> : InternalBufferManager<T> {
			private static GCBufferManager<T> s_value = new GCBufferManager<T>();

			private GCBufferManager() {
			}

			public static GCBufferManager<T> Value {
				get { return s_value; }
			}

			public override void Clear() {
			}

			public override T[] TakeBuffer(int bufferLength) {
				return new T[bufferLength];
			}

			public override void ReturnBuffer(T[] buffer) {
				// do nothing, GC will reclaim this buffer
			}
		}
	}
}