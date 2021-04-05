using System.Collections.Generic;

namespace Spreads.Collections.Generic
{
    public sealed partial class DictionarySlim<TKey, TValue>
    {
        public int TryGetNextPair(out KeyValuePair<TKey, TValue> pair)
        {
            if (Count > 0)
            {
                for (int i = 0; i < _count; i++)
                {
                    ref var e = ref _entries[i];
                    if (e.Next >= -1)
                    {
                        pair = new KeyValuePair<TKey, TValue>(e.Key, e.Value);
                        return i;
                    }
                }
            }

            pair = default;
            return -1;
        }

        public int TryGetNextPair(in TKey key, out KeyValuePair<TKey, TValue> pair)
        {
            TryGetValueIdx(in key, out int index);

            if (index < 0)
            {
                pair = default;
                return -2;
            }

            for (int i = index + 1; i < _count; i++)
            {
                ref Entry e = ref _entries[i];
                if (e.Next >= -1)
                {
                    pair = new KeyValuePair<TKey, TValue>(e.Key, e.Value);
                    return i;
                }
            }

            pair = default;
            return -1;
        }
    }
}
