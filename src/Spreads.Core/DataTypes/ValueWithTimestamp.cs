// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization.Utf8Json;
using System.Runtime.InteropServices;
using Spreads.Serialization;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Spreads.DataTypes
{

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serialization(PreferBlittable = true)]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // NB cannot use JsonFormatter attribute, this is hardcoded in DynamicGenericResolverGetFormatterHelper
    public readonly struct ValueWithTimestamp<T>
    {
        public readonly Timestamp Timestamp;
        public readonly T Value;

        public ValueWithTimestamp(T value, Timestamp timestamp)
        {
            Timestamp = timestamp;
            Value = value;
        }
    }

    // NB cannot use JsonFormatter attribute, this is hardcoded in DynamicGenericResolverGetFormatterHelper
    public class ValueWithTimestampFormatter<T> : IJsonFormatter<ValueWithTimestamp<T>>
    {
        public void Serialize(ref JsonWriter writer, ValueWithTimestamp<T> value, IJsonFormatterResolver formatterResolver)
        {
            writer.WriteBeginArray();

            writer.WriteInt64((long)value.Timestamp);

            writer.WriteValueSeparator();

            formatterResolver.GetFormatter<T>().Serialize(ref writer, value.Value, formatterResolver);

            writer.WriteEndArray();
        }

        public ValueWithTimestamp<T> Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            reader.ReadIsBeginArrayWithVerify();

            var timestamp = (Timestamp)reader.ReadInt64();

            reader.ReadIsValueSeparatorWithVerify();

            var value = formatterResolver.GetFormatterWithVerify<T>().Deserialize(ref reader, formatterResolver);

            reader.ReadIsEndArrayWithVerify();

            return new ValueWithTimestamp<T>(value, timestamp);
        }
    }
}
