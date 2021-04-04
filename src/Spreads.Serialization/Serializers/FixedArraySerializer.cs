// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Reflection;
using Spreads.Buffers;
using Spreads.DataTypes;

namespace Spreads.Serialization.Serializers
{
    internal interface IFixedArraySerializer
    {
        int FixedArrayCount<T>(T value);
    }

    internal static class FixedArraySerializerFactory
    {
        public static BinarySerializer<FixedArray<TElement>> GenericCreate<TElement>()
        {
            return new FixedArraySerializer<TElement>();
        }

        public static object Create(Type type)
        {
            var method = typeof(FixedArraySerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type);
            return generic?.Invoke(null, null);
        }

        internal class FixedArraySerializer<T> :  BinarySerializer<FixedArray<T>>, IFixedArraySerializer
        {
            public override byte KnownTypeId => throw new System.NotImplementedException();

            public override short FixedSize => throw new System.NotImplementedException();

            public override int SizeOf(in FixedArray<T> value, BufferWriter payload)
            {
                throw new NotImplementedException();
            }

            public override int Write(in FixedArray<T> value, DirectBuffer destination)
            {
                throw new System.NotImplementedException();
            }

            public override int Read(DirectBuffer source, out FixedArray<T> value)
            {
                throw new System.NotImplementedException();
            }

            public int FixedArrayCount<T1>(T1 value)
            {
                var fa = (FixedArray<T>)(object)(value);
                return fa.Count;
            }
        }
    }
}
