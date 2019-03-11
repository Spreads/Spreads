// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization.Utf8Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Spreads.DataTypes
{
    // It is Tuple3 from serialization point of view and has custom binary converter.

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct TaggedKeyValue<TKey, TValue> : IEquatable<TaggedKeyValue<TKey, TValue>>
    {
        [DataMember(Name = "t", Order = 0)]
        public readonly byte Tag;

        [DataMember(Name = "k", Order = 1)]
        public readonly TKey Key;

        [DataMember(Name = "v", Order = 2)]
        public readonly TValue Value;

        public TaggedKeyValue(TKey key, TValue value)
        {
            Tag = 0;
            Key = key;
            Value = value;
        }

        public TaggedKeyValue(byte tag, TKey key, TValue value)
        {
            Tag = tag;
            Key = key;
            Value = value;
        }

        public static implicit operator KeyValuePair<TKey, TValue>(TaggedKeyValue<TKey, TValue> kv)
        {
            return new KeyValuePair<TKey, TValue>(kv.Key, kv.Value);
        }

        public static implicit operator TaggedKeyValue<TKey, TValue>(KeyValuePair<TKey, TValue> kvp)
        {
            return new TaggedKeyValue<TKey, TValue>(kvp.Key, kvp.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator (byte Tag, TKey Key, TValue Value) (TaggedKeyValue<TKey, TValue> tkv)
        {
            return (tkv.Tag, tkv.Key, tkv.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TaggedKeyValue<TKey, TValue>((byte Tag, TKey Key, TValue Value) tuple)
        {
            return new TaggedKeyValue<TKey, TValue>(tuple.Tag, tuple.Key, tuple.Value);
        }


        public bool Equals(TaggedKeyValue<TKey, TValue> other)
        {
            return Tag == other.Tag && EqualityComparer<TKey>.Default.Equals(Key, other.Key) && EqualityComparer<TValue>.Default.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is TaggedKeyValue<TKey, TValue> value && Equals(value);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Tag.GetHashCode();
                hashCode = (hashCode * 397) ^ EqualityComparer<TKey>.Default.GetHashCode(Key);
                hashCode = (hashCode * 397) ^ EqualityComparer<TValue>.Default.GetHashCode(Value);
                return hashCode;
            }
        }
    }

    // NB cannot use generic JsonFormatter attribute, this is hardcoded in DynamicGenericResolverGetFormatterHelper
    public class TaggedKeyValueFormatter<TKey, TValue> : IJsonFormatter<TaggedKeyValue<TKey, TValue>>
    {
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void Serialize(ref JsonWriter writer, TaggedKeyValue<TKey, TValue> value, IJsonFormatterResolver formatterResolver)
        {
            writer.WriteBeginArray();

            writer.WriteByte(value.Tag);

            writer.WriteValueSeparator();

            formatterResolver.GetFormatterWithVerify<TKey>().Serialize(ref writer, value.Key, formatterResolver);

            writer.WriteValueSeparator();

            formatterResolver.GetFormatterWithVerify<TValue>().Serialize(ref writer, value.Value, formatterResolver);

            writer.WriteEndArray();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public TaggedKeyValue<TKey, TValue> Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            reader.ReadIsBeginArrayWithVerify();

            var tag = reader.ReadByte();

            reader.ReadIsValueSeparatorWithVerify();

            var key = formatterResolver.GetFormatterWithVerify<TKey>().Deserialize(ref reader, formatterResolver);

            reader.ReadIsValueSeparatorWithVerify();

            var value = formatterResolver.GetFormatterWithVerify<TValue>().Deserialize(ref reader, formatterResolver);

            reader.ReadIsEndArrayWithVerify();

            return new TaggedKeyValue<TKey, TValue>(tag, key, value);
        }
    }
}
