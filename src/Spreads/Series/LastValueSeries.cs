// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections;
using System;
using System.Collections.Generic;
using Spreads.Cursors;

namespace Spreads
{
    // TODO 
    
    /// <summary>
    /// A series that keeps only a last element.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class LastValueSeries<TKey, TValue> : ContainerSeries<TKey, TValue, Cursor<TKey, TValue>>
    {
        private KeyValuePair<TKey, TValue> _lastValue;
        private bool _isSet = false;

        public override Opt<KeyValuePair<TKey, TValue>> First => _isSet ? Opt.Present(_lastValue) : Opt<KeyValuePair<TKey, TValue>>.Missing;
        public override Opt<KeyValuePair<TKey, TValue>> Last => _isSet  ? Opt.Present(_lastValue) : Opt<KeyValuePair<TKey, TValue>>.Missing;
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
                ThrowHelper.ThrowKeyNotFoundException("Key not found");
                return default;
            }
            set
            {
                _lastValue = new KeyValuePair<TKey, TValue>(key, value);
                _isSet = true;
                NotifyUpdate();
            }
        }

        public override bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> value)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetValue(TKey key, out TValue value)
        {
            if (Comparer.Compare(key, _lastValue.Key) == 0)
            {
                value = _lastValue.Value;
                return true;
            }

            value = default;
            return false;
        }

        public override bool TryGetAt(long index, out KeyValuePair<TKey, TValue> value)
        {
            if (index != 0)
            {
                value = default;
                return false;
            }
            value = _lastValue;
            return true;
        }

        internal override Cursor<TKey, TValue> GetContainerCursor()
        {
            return GetCursor();
        }

        public override KeyComparer<TKey> Comparer { get; } = KeyComparer<TKey>.Default;
        public override bool IsIndexed => false;

        protected override ICursor<TKey, TValue> GetCursorImpl()
        {
            throw new NotImplementedException();
        }
    }
}