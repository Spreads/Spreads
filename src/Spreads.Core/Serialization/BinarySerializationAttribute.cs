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
        internal static BinarySerializationAttribute GetSerializationAttribute(Type type)
        {
            var attr = type.GetTypeInfo().GetCustomAttributes<BinarySerializationAttribute>(true).FirstOrDefault();
            return attr;
        }

        internal static StructLayoutAttribute GetStructLayoutAttribute(Type type)
        {
            return type.GetTypeInfo().StructLayoutAttribute;
        }

        //public BinarySerializationAttribute()
        //{ }

        public BinarySerializationAttribute(int blittableSize = 0, bool preferBlittable = false)
        {
            if (!preferBlittable && (blittableSize <= 0 || blittableSize > 256))
            {
                Environment.FailFast("SerializationAttribute: blittableSize <= 0 || blittableSize > 255");
            }
            BlittableSize = blittableSize;
            PreferBlittable = preferBlittable;
        }

        public BinarySerializationAttribute(Type converterType)
        {
            ConverterType = converterType;
        }

        internal BinarySerializationAttribute(TypeEnum typeEnum, int blittableSize = 0, bool preferBlittable = false, Type converterType = null)
        {
#if SPREADS
            if (typeEnum != TypeEnum.None && (int)typeEnum < (int)TypeEnum.Binary && converterType != null)
            {
                Environment.FailFast("Cannot use Converter for fixed-sized types");
            }
#endif
            BlittableSize = blittableSize;
            PreferBlittable = preferBlittable;
            ConverterType = converterType;
            TypeEnum = typeEnum;
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

        internal Type ConverterType { get; set; }

        public byte KnownTypeId { get; set; }

        /// <summary>
        /// Need this override for Spreads types defined outside this assembly
        /// </summary>
        internal TypeEnum TypeEnum { get; set; }
    }
}