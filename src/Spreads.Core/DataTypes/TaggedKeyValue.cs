// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    [Serialization(PreferBlittable = true)] // when both types are blittable the struct is written in one operation
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct TaggedKeyValue<TKey, TValue> : IEquatable<TaggedKeyValue<TKey, TValue>>, IBinaryConverter<TaggedKeyValue<TKey, TValue>>
    {
        // for blittable case all this is written in one operation,
        // for var size case will manually write with two headers
        private readonly byte _keyTypeEnum;
        private readonly byte _valueTypeEnum;
        private readonly byte _reserved;
        public readonly byte Tag;
        
        public readonly TKey Key;
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

        public int SizeOf(in TaggedKeyValue<TKey, TValue> value, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            var fixedSize = TypeHelper<TaggedKeyValue<TKey, TValue>>.Size;
            if (fixedSize > 0)
            {
                temporaryStream = default;
                return fixedSize;
            }
            throw new NotImplementedException();
        }

        public int Write(in TaggedKeyValue<TKey, TValue> value, IntPtr pinnedDestination, MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary)
        {
            var fixedSize = TypeHelper<TaggedKeyValue<TKey, TValue>>.Size;
            if (fixedSize > 0)
            {
                return BinarySerializer.WriteUnsafe(value, pinnedDestination, temporaryStream, format);
            }
            throw new NotImplementedException();
        }

        public int Read(IntPtr ptr, out TaggedKeyValue<TKey, TValue> value)
        {
            var fixedSize = TypeHelper<TaggedKeyValue<TKey, TValue>>.Size;
            if (fixedSize > 0)
            {
                return BinarySerializer.Read(ptr, out value);
            }
            throw new NotImplementedException();
        }

        public bool IsFixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return TypeHelper<TaggedKeyValue<TKey, TValue>>.Size > 0; }
        }

        public int Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return TypeHelper<TaggedKeyValue<TKey, TValue>>.Size;
            }
        }

        public byte Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return 0; }
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
            return obj is TaggedKeyValue<TKey, TValue> && Equals((TaggedKeyValue<TKey, TValue>) obj);
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
}
