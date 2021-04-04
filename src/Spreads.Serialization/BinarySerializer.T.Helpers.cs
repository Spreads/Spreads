// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Serialization.Serializers;

namespace Spreads.Serialization
{
    public abstract partial class BinarySerializer<T>
    {
        internal static readonly BinarySerializer<T>? TypeSerializer = InitSerializer();

        public static readonly bool HasTypeSerializer = TypeSerializer != null;

        internal static readonly bool IsTypeSerializerInternal = TypeSerializer is InternalSerializer<T>;

        internal static readonly DataTypeHeader CustomHeader = InitCustomHeader();

        private static BinarySerializer<T> InitSerializer()
        {
            var bsAttr = BinarySerializationAttribute.GetSerializationAttribute(typeof(T));

            BinarySerializer<T> serializer = null;

            if (bsAttr != null && bsAttr.SerializerType != null)
            {
                if (!typeof(BinarySerializer<T>).IsAssignableFrom(bsAttr.SerializerType))
                    ThrowHelper.ThrowInvalidOperationException($"SerializerType `{bsAttr.SerializerType.FullName}` in BinarySerialization " +
                                                               $"attribute does not implement IBinaryConverter<T> for the type `{typeof(T).FullName}`");

                try
                {
                    serializer = (BinarySerializer<T>)Activator.CreateInstance(bsAttr.SerializerType);
                }
                catch
                {
                    ThrowHelper.ThrowInvalidOperationException($"SerializerType `{bsAttr.SerializerType.FullName}` must have a parameterless constructor.");
                }
            }

            // NB we try to check interface as a last step, because some generic types
            // could implement IBinaryConverter<T> but still be blittable for certain types,
            // e.g. DateTime vs long in PersistentMap<K,V>.Entry
            //if (tmp is IBinaryConverter<T>) {
            if (typeof(BinarySerializer<T>).IsAssignableFrom(typeof(T)))
            {
                if (serializer != null)
                    ThrowHelper.ThrowInvalidOperationException($"IBinarySerializer `{serializer.GetType().FullName}` was already set via " +
                                                               $"BinarySerialization attribute. The type `{typeof(T).FullName}` should not implement " +
                                                               "IBinaryConverter<T> interface or the attribute should not include SerializerType property.");

                try
                {
                    serializer = (BinarySerializer<T>)(object)Activator.CreateInstance<T>();
                }
                catch
                {
                    ThrowHelper.ThrowInvalidOperationException($"Type T ({typeof(T).FullName}) implements IBinaryConverter<T> and must have a parameterless constructor.");
                }
            }

#if SPREADS
            // TODO synchronize with TypeEnumHelper's GetTypeEnum and CreateTypeInfo

            if (typeof(T) == typeof(DateTime))
            {
                serializer = (InternalSerializer<T>)(object)DateTimeSerializer.Instance;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(FixedArray<>))
            {
                var elementType = typeof(T).GetGenericArguments()[0];
                var elementSize = TypeHelper.GetFixedSize(elementType);
                if (elementSize > 0)
                {
                    // only for blittable types
                    serializer = (InternalSerializer<T>)FixedArraySerializerFactory.Create(elementType);
                }
            }

            #region Tuple2

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)KvpSerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (InternalSerializer<T>)ValueTuple2SerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)Tuple2SerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetTypeInfo().IsValueType &&
                typeof(T).GetTypeInfo().GetInterfaces()
                    .Any(i => i.IsGenericType
                              && i.GetGenericTypeDefinition() == typeof(ITuple<,,>)
                              && i.GetGenericArguments().Last() == typeof(T)
                    )
            )
            {
                var iTy = typeof(T).GetTypeInfo().GetInterfaces()
                    .First(i => i.IsGenericType
                                && i.GetGenericTypeDefinition() == typeof(ITuple<,,>)
                                && i.GetGenericArguments().Last() == typeof(T)
                    );
                var gArgs = iTy.GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)InterfaceTuple2SerializerFactory.Create(gArgs[0], gArgs[1], typeof(T));
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            #endregion Tuple2

            #region Tuple3

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(TaggedKeyValue<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)TaggedKeyValueByteSerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (InternalSerializer<T>)ValueTuple3SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)Tuple3SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            #endregion Tuple3

            #region Tuple4

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (InternalSerializer<T>)ValueTuple4SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2], gArgs[3]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)Tuple4SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2], gArgs[3]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            #endregion Tuple4

            #region Tuple5

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (InternalSerializer<T>)ValueTuple5SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2], gArgs[3], gArgs[4]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,,,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)Tuple5SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2], gArgs[3], gArgs[4]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            #endregion Tuple5

            if (typeof(T).IsArray)
            {
                var elementType = typeof(T).GetElementType();
                var elementSize = TypeHelper.GetFixedSize(elementType);
                if (elementSize > 0)
                {
                    // only for blittable types
                    serializer = (InternalSerializer<T>)ArraySerializerFactory.Create(elementType);
                }
            }

            if (typeof(T).IsGenericType
                && typeof(T).GetGenericTypeDefinition() == typeof(RetainedVec<>))
            {
                // TODO TEH by type
                var elementType = typeof(T).GenericTypeArguments[0];
                var elementSize = TypeHelper.GetFixedSize(elementType);
                if (elementSize > 0)
                {
                    // only for blittable types
                    serializer = (InternalSerializer<T>)Collections.Internal.VectorStorageSerializerFactory.Create(elementType);
                }
            }

            // Do not add Json converter as fallback, it is not "binary", it implements the interface for
            // simpler implementation in BinarySerializer and fallback happens there
#endif

            return serializer;
        }

        private static DataTypeHeader InitCustomHeader()
        {
            // re-read, happens once per type
            var sa = BinarySerializationAttribute.GetSerializationAttribute(typeof(T));

            if (sa != null && sa.CustomHeader.TEOFS.TypeEnum != TypeEnum.None)
            {
                var value = (byte)sa.CustomHeader.TEOFS.TypeEnum;
                if (value < 100 || value >= 120)
                {
                    // Internal
                    Environment.FailFast("CustomHeader.TEOFS.TypeEnum must be in the range [100,119]");
                }

                return sa.CustomHeader;
            }

            return default;
        }
    }
}
