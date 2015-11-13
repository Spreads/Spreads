using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime;

namespace Spreads {

	public class DoubleArrayPool : IArrayPool
	{
		private InternalBufferManager<double>.PooledBufferManager _doublePool =
			new InternalBufferManager<double>.PooledBufferManager(512*1024*1024, 1024 * 1024);

		public T[] TakeBuffer<T>(int bufferCount)
		{
			if (typeof (T) == typeof (double))
			{
				return _doublePool.TakeBuffer(bufferCount) as T[];
			}
			else
			{
				return new T[bufferCount];
			}
		}

		public void ReturnBuffer<T>(T[] buffer)
		{
			if (typeof(T) == typeof(double))
			{
				_doublePool.ReturnBuffer(buffer as double[]);
			}
		}

		public void Clear()
		{
			_doublePool.Clear();
		}
	}
}
