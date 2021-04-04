// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class BinarySerializationAttribute : Attribute
    {
        internal static BinarySerializationAttribute? GetSerializationAttribute(Type type)
        {
            var attr = type.GetTypeInfo().GetCustomAttributes<BinarySerializationAttribute>(true).FirstOrDefault();
            return attr;
        }

        internal static StructLayoutAttribute GetStructLayoutAttribute(Type type)
        {
            return type.GetTypeInfo().StructLayoutAttribute;
        }

        public BinarySerializationAttribute(short blittableSize = 0, bool preferBlittable = false)
        {
            if (!preferBlittable)
            {
                if (blittableSize <= 0)
                {
                    throw new InvalidOperationException("blittableSize must be positive.");
                }
            }
            else
            {
                if (blittableSize != default)
                {
                    throw new InvalidOperationException("blittableSize must be zero when preferBlittable = true");
                }
            }
            BlittableSize = blittableSize;
            PreferBlittable = preferBlittable;
        }

        public BinarySerializationAttribute(Type serializerType)
        {
            SerializerType = serializerType;
        }

        internal BinarySerializationAttribute(byte[] customHeaderOverrides, int blittableSize = 0, bool preferBlittable = false, Type serializerType = null)
        {
#if SPREADS
            if (customHeaderOverrides == null || customHeaderOverrides.Length == 0 || customHeaderOverrides.Length > 3)
            {
                throw new ArgumentException("Invalid customHeaderOverrides: customHeaderOverrides == null || customHeaderOverrides.Length == 0 || customHeaderOverrides.Length > 3.");
            }
            var customHeaderAsByte = (byte)customHeaderOverrides[0];
            if (customHeaderAsByte < 100 || customHeaderAsByte >= 120)
            {
                throw new ArgumentException("customHeaderOverrides[0] must have typeEnum in reserved range 100-119.");
            }
#else
            var customHeaderAsByte = 0;
#endif
            BlittableSize = blittableSize;
            PreferBlittable = preferBlittable;
            SerializerType = serializerType;
            CustomHeader = new DataTypeHeader
            {
                TEOFS = customHeaderOverrides.Length > 0 ? new TypeEnumOrFixedSize((TypeEnum)customHeaderAsByte) : default,
                TEOFS1 = customHeaderOverrides.Length > 1 ? new TypeEnumOrFixedSize(customHeaderOverrides[1], false) : default,
                TEOFS2 = customHeaderOverrides.Length > 2 ? new TypeEnumOrFixedSize(customHeaderOverrides[2], false) : default
            };
        }

        /// <summary>
        /// Prefer blittable layout if possible. Sometimes a generic type could implement (or have registered) IBinaryConverter interface
        /// but for certain concrete types still be blittable. When this property is true, we
        /// ignore the IBinaryConverter interface when a generic type is blittable but implements that interface (or has it registered).
        /// </summary>
        internal bool PreferBlittable { get; set; }

        /// <summary>
        /// When this property is positive, the type must be blittable with the specified size,
        /// otherwise Environment.FailFast method is called and the application is terminated.
        /// </summary>
        /// <remarks>
        /// StructLayout.Size has the same behavior.
        /// </remarks>
        internal int BlittableSize { get; set; }

        internal Type? SerializerType { get; set; }

        public byte KnownTypeId { get; set; }

        internal DataTypeHeader CustomHeader { get; set; }
    }
}
