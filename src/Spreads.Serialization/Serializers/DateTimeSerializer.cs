// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using System;

namespace Spreads.Serialization.Serializers
{
    /// <summary>
    /// Fallback serializer that serializes data as JSON but pretends to be a binary one.
    /// </summary>
    internal sealed class DateTimeSerializer : InternalSerializer<DateTime>
    {
        internal static DateTimeSerializer Instance = new DateTimeSerializer();

        private DateTimeSerializer()
        {
        }

        public const short DateTimeSize = 8;

        public override byte KnownTypeId => 0;

        public override short FixedSize => DateTimeSize;

        public override int SizeOf(in DateTime value, BufferWriter payload)
        {
            payload?.Write(in value);
            return DateTimeSize;
        }

        public override int Write(in DateTime value, DirectBuffer destination)
        {
            destination.Write(0, value);
            return DateTimeSize;
        }

        public override int Read(DirectBuffer source, out DateTime value)
        {
            value = source.Read<DateTime>(0);
            return DateTimeSize;
        }
    }
}