// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.Serialization;

namespace Spreads.Serialization
{
    /// <summary>
    /// DTO for generic schema that uses BinarySerializer.
    /// </summary>
    internal struct GenericSchema
    {
        [DataMember(Name = "dth")]
        public DataTypeHeader DataTypeHeader { get; set; }

        [DataMember(Name = "typedef")]
        public TypeDef TypeDefinition { get; set; }

        public struct TypeDef
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "fullname")]
            public string FullName { get; set; }

            [DataMember(Name = "teofs")]
            public TypeEnumOrFixedSize TEOFS { get; set; }

            [DataMember(Name = "fixedsize")]
            public short FixedSize { get; set; }

            [DataMember(Name = "offset")]
            public short Offset { get; set; }

            [DataMember(Name = "members")]
            public TypeDef[] Members { get; set; }
        }
    }
}
