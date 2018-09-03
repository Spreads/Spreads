// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Spreads.DataTypes
{

    [Serialization(PreferBlittable = true)] // when both types are blittable the struct is written in one operation
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // NB cannot use JsonFormatter attribute, this is hardcoded in DynamicGenericResolverGetFormatterHelper
    public readonly struct TaggedKeyValue<TKey, TValue> : IEquatable<TaggedKeyValue<TKey, TValue>>, 
        IBinaryConverter<TaggedKeyValue<TKey, TValue>>
    {
        private static readonly int KeySize = TypeHelper<TKey>.Size;
        private static readonly int ValueSize = TypeHelper<TValue>.Size;
        private static readonly bool IsFixedSizeStatic = TypeHelper<TKey>.Size > 0 && TypeHelper<TValue>.Size > 0;
        private static readonly int FixedSizeStatic = 4 + TypeHelper<TKey>.Size + TypeHelper<TValue>.Size;

        private static DataTypeHeader _defaultHeader = new DataTypeHeader
        {
            VersionAndFlags =
            {
                Version = 1,
                IsBinary = true,
                IsDelta = false,
                IsCompressed = false
            },
            TypeEnum = VariantHelper<TaggedKeyValue<TKey, TValue>>.TypeEnum,
            TypeSize = IsFixedSizeStatic ? (byte)FixedSizeStatic : (byte)0
        };

        // for blittable case all this is written in one operation,
        // for var size case will manually write with two headers
        [IgnoreDataMember]
        private readonly byte _keyTypeEnum;

        [IgnoreDataMember]
        private readonly byte _valueTypeEnum;

        [IgnoreDataMember]
        private readonly byte _reserved;

        [DataMember(Name = "t", Order = 0)]
        public readonly byte Tag;

        [DataMember(Name = "k", Order = 1)]
        public readonly TKey Key;

        [DataMember(Name = "v", Order = 2)]
        public readonly TValue Value;

        public TaggedKeyValue(TKey key, TValue value)
        {
            _keyTypeEnum = (byte)VariantHelper<TKey>.TypeEnum;
            _valueTypeEnum = (byte)VariantHelper<TValue>.TypeEnum;
            _reserved = 0;
            Tag = 0;
            Key = key;
            Value = value;
        }

        public TaggedKeyValue(TKey key, TValue value, byte tag)
        {
            _keyTypeEnum = (byte)VariantHelper<TKey>.TypeEnum;
            _valueTypeEnum = (byte)VariantHelper<TValue>.TypeEnum;
            _reserved = 0;
            Tag = tag;
            Key = key;
            Value = value;
        }

        // TODO for version 0 write using BS with double headers
        // TODO special treatment of TKey=DateTime, autolayout

        public int SizeOf(TaggedKeyValue<TKey, TValue> value, out MemoryStream temporaryStream, 
            SerializationFormat format = SerializationFormat.Binary, 
            Timestamp timestamp = default)
        {
            if (IsFixedSizeStatic)
            {
                temporaryStream = default;
                return FixedSizeStatic + (timestamp == default ? 0 : 8);
            }

            throw new NotImplementedException();
        }

        public int Write(TaggedKeyValue<TKey, TValue> value, IntPtr pinnedDestination, MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary, 
            Timestamp timestamp = default)
        {
            var fixedSize = TypeHelper<TaggedKeyValue<TKey, TValue>>.Size;
            if (fixedSize > 0)
            {
                return BinarySerializer.WriteUnsafe(value, pinnedDestination, temporaryStream, format, timestamp);
            }
            // TODO write key + value
            throw new NotImplementedException();
        }

        public int Read(IntPtr ptr, out TaggedKeyValue<TKey, TValue> value, out Timestamp timestamp)
        {
            var fixedSize = TypeHelper<TaggedKeyValue<TKey, TValue>>.Size;
            if (fixedSize > 0)
            {
                return BinarySerializer.Read(ptr, out value, out timestamp);
            }
            throw new NotImplementedException();
        }

        [IgnoreDataMember]
        public bool IsFixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsFixedSizeStatic;
        }

        [IgnoreDataMember]
        public int Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsFixedSizeStatic ? FixedSizeStatic : -1;
        }

        [IgnoreDataMember]
        public byte ConverterVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 1;
        }

        public static implicit operator KeyValuePair<TKey, TValue>(TaggedKeyValue<TKey, TValue> kv)
        {
            return new KeyValuePair<TKey, TValue>(kv.Key, kv.Value);
        }

        public static implicit operator TaggedKeyValue<TKey, TValue>(KeyValuePair<TKey, TValue> kvp)
        {
            return new TaggedKeyValue<TKey, TValue>(kvp.Key, kvp.Value);
        }

        public bool Equals(TaggedKeyValue<TKey, TValue> other)
        {
            return Tag == other.Tag && EqualityComparer<TKey>.Default.Equals(Key, other.Key) && EqualityComparer<TValue>.Default.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is TaggedKeyValue<TKey, TValue> && Equals((TaggedKeyValue<TKey, TValue>)obj);
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


    // NB cannot use JsonFormatter attribute, this is hardcoded in DynamicGenericResolverGetFormatterHelper
    public class TaggedKeyValueFormatter<TKey, TValue> : IJsonFormatter<TaggedKeyValue<TKey, TValue>>
    {
        public void Serialize(ref JsonWriter writer, TaggedKeyValue<TKey, TValue> value, IJsonFormatterResolver formatterResolver)
        {
            writer.WriteBeginArray();

            writer.WriteByte(value.Tag);

            writer.WriteValueSeparator();

            formatterResolver.GetFormatter<TKey>().Serialize(ref writer, value.Key, formatterResolver);

            writer.WriteValueSeparator();

            formatterResolver.GetFormatter<TValue>().Serialize(ref writer, value.Value, formatterResolver);

            writer.WriteEndArray();
        }

        public TaggedKeyValue<TKey, TValue> Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            reader.ReadIsBeginArrayWithVerify();

            var tag = reader.ReadByte();

            reader.ReadIsValueSeparatorWithVerify();

            var key = formatterResolver.GetFormatterWithVerify<TKey>().Deserialize(ref reader, formatterResolver);

            reader.ReadIsValueSeparatorWithVerify();

            var value = formatterResolver.GetFormatterWithVerify<TValue>().Deserialize(ref reader, formatterResolver);

            reader.ReadIsEndArrayWithVerify();

            return new TaggedKeyValue<TKey, TValue>(key, value, tag);
        }
    }
}
