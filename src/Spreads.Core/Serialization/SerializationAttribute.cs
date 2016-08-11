/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Spreads.Serialization {

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class SerializationAttribute : Attribute {

        internal static SerializationAttribute GetSerializationAttribute(Type type) {
            var attr = type.GetTypeInfo().GetCustomAttributes<SerializationAttribute>(true).FirstOrDefault();
            return attr;
        }

        internal static StructLayoutAttribute GetStructLayoutAttribute(Type type) {
            return type.GetTypeInfo().GetCustomAttributes<StructLayoutAttribute>(true).FirstOrDefault();
        }

        /// <summary>
        /// Prefer blittable layout if possible. Sometimes a generic type could implement (or have registered) IBinaryConverter interface
        /// but for certain concrete types still be blittable. When this property is true, we
        /// ignore the IBinaryConverter interface when a generic type is blittable but implements that interface (or has it registered).
        /// </summary>
        public bool PreferBlittable { get; set; }

        /// <summary>
        /// When this property is positive, the type must be blittable with the specified size,
        /// otherwise Environment.FailFast method is called and application is terminated.
        /// StructLayout.Size has the same behavior.
        /// </summary>
        public int BlittableSize { get; set; }

    }
}
