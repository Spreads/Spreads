// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    internal class BuiltInDataTypeAttribute : Attribute
    {
        internal static BuiltInDataTypeAttribute? GetSerializationAttribute(Type type)
        {
            BuiltInDataTypeAttribute attr = type.GetTypeInfo().GetCustomAttributes<BuiltInDataTypeAttribute>(inherit: true).FirstOrDefault();
            return attr;
        }

        internal static StructLayoutAttribute? GetStructLayoutAttribute(Type type)
        {
            return type.GetTypeInfo().StructLayoutAttribute;
        }

        public BuiltInDataTypeAttribute(short blittableSize = 0, bool preferBlittable = false)
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

        internal bool PreferBlittable { get; set; }

        internal int BlittableSize { get; set; }
    }
}
