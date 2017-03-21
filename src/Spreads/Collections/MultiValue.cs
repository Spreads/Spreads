using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.Buffers;
using Spreads.Serialization;

namespace Spreads.Collections
{
    // Story
    // I have a stream of values that could have a duplicate key, but I want to index them by that key
    // the index key is only increasing, otherwise I throw
    // I want to be able to transform input somewhat, e.g. to remove the index (Ticks -> Quotes),
    // however a duplicate field will be compressed greatly (99%) by Blosc
    // I want to be able to require overlap, to protect myself from creating holes in historical data

    public struct DataStream<TInput, TKey, TValue>
    {
        private readonly Func<TInput, KeyValuePair<TKey, TValue>> _transformer;
        private readonly int _maxValuesPerChunk;

        public DataStream(Func<TInput, KeyValuePair<TKey, TValue>> transformer, int maxValuesPerChunk = 1000)
        {
            _transformer = transformer;
            _maxValuesPerChunk = maxValuesPerChunk;
            var typeSize = BinarySerializer.Size<TValue>();
            var memorySize = typeSize * _maxValuesPerChunk + _maxValuesPerChunk * 4 / 2;

            var memory = BufferPool.PreserveMemory(memorySize);
            throw new NotImplementedException();
        }

        private SortedMap<TKey, TValue> InnerFactory(int capacity, IComparer<TKey> comparer)
        {
            return new SortedMap<TKey, TValue>(capacity, comparer);
        }
    }
}