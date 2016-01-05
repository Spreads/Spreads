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

namespace Spreads {

    public class DoubleArrayPool : IArrayPool {
        private InternalBufferManager<double>.PooledBufferManager _doublePool =
            new InternalBufferManager<double>.PooledBufferManager(512 * 1024 * 1024, 128 * 1024 * 1024);

        public T[] TakeBuffer<T>(int bufferCount) {
            if (typeof(T) == typeof(double)) {
                return _doublePool.TakeBuffer(bufferCount) as T[];
            } else {
                return new T[bufferCount];
            }
        }

        public void ReturnBuffer<T>(T[] buffer) {
            if (typeof(T) == typeof(double)) {
                _doublePool.ReturnBuffer(buffer as double[]);
            }
        }

        public void Clear() {
            _doublePool.Clear();
        }
    }


    public class AtomicCounter {
        public int Count;
    }

    public class DoubleArrayPoolCWT : IArrayPool {
        private ConditionalWeakTable<object, AtomicCounter> _cwt = new ConditionalWeakTable<object, AtomicCounter>();

        private InternalBufferManager<double>.PooledBufferManager _doublePool =
            new InternalBufferManager<double>.PooledBufferManager(512 * 1024 * 1024, 128 * 1024 * 1024);

        public T[] TakeBuffer<T>(int bufferCount) {
            if (typeof(T) == typeof(double)) {
                var buffer = _doublePool.TakeBuffer(bufferCount) as T[];
                AtomicCounter cnt;
                if (_cwt.TryGetValue(buffer, out cnt))
                {
                    cnt.Count++;
                }
                else
                {
                    _cwt.Add(buffer, new AtomicCounter {Count = 1});
                }
                return buffer;
            } else {
                return new T[bufferCount];
            }
        }

        public void ReturnBuffer<T>(T[] buffer) {
            if (typeof(T) == typeof(double)) {
                AtomicCounter cnt;
                if (_cwt.TryGetValue(buffer, out cnt)) {
                    cnt.Count--;
                    _doublePool.ReturnBuffer(buffer as double[]);
                }
            }
        }

        public void Clear() {
            _doublePool.Clear();
        }
    }
}
