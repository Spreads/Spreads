/*
    Copyright(c) 2014-2015 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Diagnostics;

namespace Spreads {

    public class BaseArrayPool : IArrayPool {
        // NB this must be a class to be stored in CWT
        // effectively we are doing the same as NetMQ counter, but instead of 
        // a wrapper struct we use CWT and instead of incrementing/decrementing 
        // the counter directly we borrow/return buffers
        private class AtomicCounter {
            public int Count;
        }
        private ConditionalWeakTable<object, AtomicCounter> _cwt = new ConditionalWeakTable<object, AtomicCounter>();

        private InternalBufferManager<double>.PooledBufferManager _doublePool =
            new InternalBufferManager<double>.PooledBufferManager(512 * 1024 * 1024, 128 * 1024 * 1024);
        private InternalBufferManager<byte>.PooledBufferManager _bytePool =
            new InternalBufferManager<byte>.PooledBufferManager(512 * 1024 * 1024, 128 * 1024 * 1024);

        public virtual T[] TakeBuffer<T>(int bufferCount) {
            T[] buffer;
            bool doTrack = false;
            if (typeof(T) == typeof(double)) {
                buffer = _doublePool.TakeBuffer(bufferCount) as T[];
                doTrack = true;
            } else if (typeof(T) == typeof(byte)) {
                buffer = _bytePool.TakeBuffer(bufferCount) as T[];
                doTrack = true;
            } else {
                buffer = new T[bufferCount];
            }
            if (doTrack) {
                AtomicCounter cnt;
                if (_cwt.TryGetValue(buffer, out cnt)) {
                    Interlocked.Increment(ref cnt.Count);
                } else {
                    _cwt.Add(buffer, new AtomicCounter { Count = 1 });
                }
            }
            return buffer;
        }

        public virtual int ReturnBuffer<T>(T[] buffer) {
            AtomicCounter cnt;
            int ret = 0;
            if (_cwt.TryGetValue(buffer, out cnt)) {
                ret = Interlocked.Decrement(ref cnt.Count);
#if PRERELEASE
                // must not return buffer more times than it was taken/borrowed
                Trace.Assert(ret >= 0);
#endif
                if (ret == 0) {
                    var dblBuffer = buffer as double[];
                    if (dblBuffer != null) {
                        _doublePool.ReturnBuffer(dblBuffer);
                    }
                    var byteBuffer = buffer as byte[];
                    if (byteBuffer != null) {
                        _bytePool.ReturnBuffer(byteBuffer);
                    }
                }
            }
            return ret;
        }

        public virtual void Clear() {
            _doublePool.Clear();
            _bytePool.Clear();
        }


        public virtual int BorrowBuffer<T>(T[] buffer) {
            AtomicCounter cnt;
            if (_cwt.TryGetValue(buffer, out cnt)) {
                var count = Interlocked.Increment(ref cnt.Count);
#if PRERELEASE
                // NB we could only borrow a buffer while original one is alive,
                // so we must assert that the current counter is above 0 and above 1 after borrowing
                Trace.Assert(count > 1);
#endif
                return count;
            }
            // buffer is not tracked by the pool and is tracked by GC
            return 0;
        }

        public virtual int ReferenceCount<T>(T[] buffer) {
            AtomicCounter cnt;
            if (_cwt.TryGetValue(buffer, out cnt)) {
                return Interlocked.Add(ref cnt.Count, 0);
            }
            // buffer is not tracked by the pool and is tracked by GC
            return 0;
        }
    }
}
