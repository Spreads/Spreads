using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Spreads.Serialization {


    // NB very likely this is bullshit and we only need IBinaryConverter interface
    // and thouroughly tested default implementation. All schemas could be hidden 
    // inside the interface. There will alway be indirection, but with an interface 
    // we only have a single indirect call via an interface.
    // Also, e.g. SBE reuses type instances and wraps around buffers, but out interface
    // doesn't account for that. In cursors that instances could be reused inside CurrentValue getter.

    /// <summary>
    /// Format for binary serialization.
    /// </summary>
    public enum SerializationFormat : byte
    {
        /// <summary>
        /// Try direct conversion using:
        /// * Blittable when possible (check Marshal.SizeOf then pinned array)
        /// * Arrays of blittable types
        /// * IBinaryConverter interface
        /// Fallback to BSON with recursive use of the three methods above for nested members when possible.
        /// </summary>
        Default = 0,
        /// <summary>
        /// BSON as converted by JSON.NET
        /// </summary>
        BSON = 1,
        /// <summary>
        /// JSON as converted by JSON.NET |> UTF8.GetBytes
        /// </summary>
        JSON = 2,
        /// <summary>
        /// Protocol Buffers based on protobuf-net
        /// </summary>
        ProtocolBuffers = 3,
        /// <summary>
        /// Using IBinaryConverter (a type must implement that interface)
        /// </summary>
        Custom = 255
    }

    // TODO (low) test multiple attributes, e.g. start with default, then bson, json, PB, custom

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class SerializationAttribute : Attribute {

        internal static SerializationAttribute GetSerializationAttribute(Type type)
        {
            var attr =  type.GetCustomAttributes<SerializationAttribute>(true).FirstOrDefault();
            return attr;
        }

        internal static StructLayoutAttribute GetStructLayoutAttribute(Type type) {
            return type.GetCustomAttributes<StructLayoutAttribute>(true).FirstOrDefault();
        }

        /// <summary>
        /// Prefer blittable layout if possible. Sometimes a generic type could implement (or have registered) IBinaryConverter interface
        /// but for certain concrete types still be blittable. When this property is true, we
        /// ignore the IBinaryConverter interface when a generic type is blittable but implements/has that interface.
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
