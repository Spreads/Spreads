// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A value with a timestamp.
    /// </summary>
    [BinarySerialization(preferBlittable: true)]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // NB cannot use JsonFormatter attribute, this is hardcoded in DynamicGenericResolverGetFormatterHelper
    public readonly struct Timestamped<T>
    {
        public readonly Timestamp Timestamp;
        public readonly T Value;

        public Timestamped(T value, Timestamp timestamp)
        {
            Timestamp = timestamp;
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(Timestamped<T> timestamped)
        {
            return timestamped.Value;
        }
    }

    // NB cannot use JsonFormatter attribute, this is hardcoded in DynamicGenericResolverGetFormatterHelper
    public class TimestampedFormatter<T> : IJsonFormatter<Timestamped<T>>
    {
        public void Serialize(ref JsonWriter writer, Timestamped<T> value, IJsonFormatterResolver formatterResolver)
        {
            writer.WriteBeginArray();

            writer.WriteInt64((long)value.Timestamp);

            writer.WriteValueSeparator();

            formatterResolver.GetFormatter<T>().Serialize(ref writer, value.Value, formatterResolver);

            writer.WriteEndArray();
        }

        public Timestamped<T> Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            reader.ReadIsBeginArrayWithVerify();

            var timestamp = (Timestamp)reader.ReadInt64();

            reader.ReadIsValueSeparatorWithVerify();

            var value = formatterResolver.GetFormatterWithVerify<T>().Deserialize(ref reader, formatterResolver);

            reader.ReadIsEndArrayWithVerify();

            return new Timestamped<T>(value, timestamp);
        }
    }
}
