using Spreads.Collections;
using System;
using System.Collections.Generic;

namespace Spreads.Series
{
    /// <summary>
    /// Series that keeps only last set element.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class LastValueSeries<TKey, TValue> : ContainerSeries<TKey, TValue>
    {
        private KeyValuePair<TKey, TValue> _lastValue;
        private bool _isSet = false;

        public override bool IsReadOnly => false;
        public override bool IsEmpty => !_isSet;
        public override KeyValuePair<TKey, TValue> First => _lastValue;
        public override KeyValuePair<TKey, TValue> Last => _lastValue;
        public override IEnumerable<TKey> Keys => new SingleSequence<TKey>(_lastValue.Key);
        public override IEnumerable<TValue> Values => new SingleSequence<TValue>(_lastValue.Value);

        public new TValue this[TKey key]
        {
            get
            {
                if (Comparer.Compare(key, _lastValue.Key) == 0)
                {
                    return _lastValue.Value;
                }
                throw new KeyNotFoundException();
            }
            set
            {
                _lastValue = new KeyValuePair<TKey, TValue>(key, value);
                _isSet = true;
                this.NotifyUpdate(true);
            }
        }

        public override bool TryFind(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> value)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetFirst(out KeyValuePair<TKey, TValue> value)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetLast(out KeyValuePair<TKey, TValue> value)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetValue(TKey key, out TValue value)
        {
            throw new NotImplementedException();
        }

        public override IComparer<TKey> Comparer { get; } = KeyComparer.GetDefault<TKey>();
        public override bool IsIndexed => false;

        public override ICursor<TKey, TValue> GetCursor()
        {
            throw new NotImplementedException();
        }
    }
}