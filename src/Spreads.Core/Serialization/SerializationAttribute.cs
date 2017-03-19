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
    public class SerializationAttribute : Attribute
    {
        internal static SerializationAttribute GetSerializationAttribute(Type type)
        {
            var attr = type.GetTypeInfo().GetCustomAttributes<SerializationAttribute>(true).FirstOrDefault();
            return attr;
        }

        internal static StructLayoutAttribute GetStructLayoutAttribute(Type type)
        {
            return type.GetTypeInfo().GetCustomAttributes<StructLayoutAttribute>(true).FirstOrDefault();
        }

        /// <summary>
        /// Prefer blittable layout if possible. Sometimes a generic type could implement (or have registered) IBinaryConverter interface
        /// but for certain concrete types still be blittable. When this property is true, we
        /// ignore the IBinaryConverter interface when a generic type is blittable but implements that interface (or has it registered).
        /// </summary>
        internal bool PreferBlittable { get; set; }

        /// <summary>
        /// When this property is positive, the type must be blittable with the specified size,
        /// otherwise Environment.FailFast method is called and application is terminated.
        /// StructLayout.Size has the same behavior.
        /// </summary>
        public int BlittableSize { get; set; }
    }
}