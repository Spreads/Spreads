using System;

namespace Spreads
{
    public struct MovingWindowOptions<T>
    {
        private readonly Func<T, T, bool> _rangeFunc;
        private readonly int? _itemCount;

        public MovingWindowOptions(int itemCount)
        {
            _itemCount = itemCount;
            _rangeFunc = default;
        }

        public MovingWindowOptions(Func<T,T,bool> rangeFunc)
        {
            _itemCount = default;
            _rangeFunc = rangeFunc;
        }

        internal bool IsDefault => _itemCount == default && _rangeFunc == default;
    }
}