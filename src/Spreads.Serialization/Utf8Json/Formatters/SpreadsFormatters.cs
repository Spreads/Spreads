// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using Spreads.DataTypes;

namespace Spreads.Serialization.Utf8Json.Formatters
{
    internal class SmallDecimalFormatter : IJsonFormatter<SmallDecimal>
    {
        public static SmallDecimalFormatter Default = new();

        public void Serialize(ref JsonWriter writer, SmallDecimal value, IJsonFormatterResolver formatterResolver)
        {
            var df = formatterResolver.GetFormatter<decimal>();
            df.Serialize(ref writer, value, formatterResolver);
        }

        public SmallDecimal Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            var df = formatterResolver.GetFormatter<decimal>();

            var d = df.Deserialize(ref reader, formatterResolver);

            // if we are reading SD then it was probably written as SD
            // if we are reading decimal we cannot silently truncate from serialized
            // value - that's the point of storing as decimal: not to lose precision
            return new SmallDecimal(d, truncate: false);
        }
    }

    internal class TimestampFormatter : IJsonFormatter<Timestamp>
    {
        public static TimestampFormatter Default = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Serialize(ref JsonWriter writer, Timestamp value, IJsonFormatterResolver formatterResolver)
        {
            var asDecimal = (decimal)value.Nanos / 1_000_000_000L;

            // need a string so that JS could strip last 3 digits to get int53 as micros and not loose precision
            writer.WriteQuotation();
            formatterResolver.GetFormatterWithVerify<decimal>().Serialize(ref writer, asDecimal, formatterResolver);
            writer.WriteQuotation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Timestamp Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            var token = reader.GetCurrentJsonToken();
            if (token == JsonToken.String)
            {
                reader.AdvanceOffset(1);
                var asDecimal = formatterResolver.GetFormatterWithVerify<decimal>().Deserialize(ref reader,
                    formatterResolver);
                // ReSharper disable once RedundantOverflowCheckingContext
                var timestamp = new Timestamp(checked((long)(asDecimal * 1_000_000_000L)));
                reader.AdvanceOffset(1);
                return timestamp;
            }

            if (token == JsonToken.Number)
            {
                var asDecimal = formatterResolver.GetFormatterWithVerify<decimal>().Deserialize(ref reader,
                    formatterResolver);
                // ReSharper disable once RedundantOverflowCheckingContext
                var timestamp = new Timestamp(checked((long)(asDecimal * 1_000_000_000L)));
                return timestamp;
            }

            ThrowHelper.ThrowInvalidOperationException($"Wrong timestamp token in JSON: {token}");

            return default;
        }
    }

    // NB cannot use JsonFormatter attribute, this is hardcoded in DynamicGenericResolverGetFormatterHelper
    public class TimestampedFormatter<T> : IJsonFormatter<Timestamped<T>>
    {
        public void Serialize(ref JsonWriter writer, Timestamped<T> value, IJsonFormatterResolver formatterResolver)
        {
            writer.WriteBeginArray();

            formatterResolver.GetFormatter<Timestamp>().Serialize(ref writer, value.Timestamp, formatterResolver);

            writer.WriteValueSeparator();

            formatterResolver.GetFormatter<T>().Serialize(ref writer, value.Value, formatterResolver);

            writer.WriteEndArray();
        }

        public Timestamped<T> Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            reader.ReadIsBeginArrayWithVerify();

            var timestamp = formatterResolver.GetFormatter<Timestamp>().Deserialize(ref reader, formatterResolver);

            reader.ReadIsValueSeparatorWithVerify();

            var value = formatterResolver.GetFormatterWithVerify<T>().Deserialize(ref reader, formatterResolver);

            reader.ReadIsEndArrayWithVerify();

            return new Timestamped<T>(timestamp, value);
        }
    }
}
