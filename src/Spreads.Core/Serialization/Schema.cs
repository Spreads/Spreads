// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.Serialization;

namespace Spreads.Serialization
{
    /// <summary>
    /// DTO for a container schema.
    /// </summary>
    public class ContainerSchema
    {
        [DataMember(Name = "type")]
        public byte ContainerType { get; set; }

        [DataMember(Name = "flags")]
        public byte ContainerFlags { get; set; }

        [DataMember(Name = "values")]
        public TypeSchema ValuesSchema { get; set; }

        [DataMember(Name = "rowkeys")]
        public TypeSchema RowKeysSchema { get; set; }

        [DataMember(Name = "columnkeys")]
        public TypeSchema ColumnKeysSchema { get; set; }

        [DataMember(Name = "columns")]
        public TypeSchema ColumnsSchema { get; set; }
    }

    /// <summary>
    /// DTO for a type schema.
    /// </summary>
    public class TypeSchema
    {
        [DataMember(Name = "dth")]
        public DataTypeHeader DataTypeHeader { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "typename")]
        public string TypeName { get; set; }

        [DataMember(Name = "typefullname")]
        public string TypeFullName { get; set; }

        [DataMember(Name = "fixedsize")]
        public short? FixedSize { get; set; }

        [DataMember(Name = "offset")]
        public short? Offset { get; set; }

        [DataMember(Name = "members")]
        public TypeSchema[] Members { get; set; }
    }
}
