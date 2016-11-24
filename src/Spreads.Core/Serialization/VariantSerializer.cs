using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spreads.Buffers;
using Spreads.DataTypes;

namespace Spreads.Serialization
{
    // NB this layout should support any Variant tree (e.g. Variant containing Variants)

    /// <summary>
    /// Convert a generic object T to a pointer prefixed with version and length.
    /// 
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |  Ver  | Flg |C|    TypeEnum   |  TypeSize     | SubTypeEnum   |
    /// +---------------------------------------------------------------+
    /// |                  Count (only for arrays)                      |
    /// +---------------------------------------------------------------+
    /// |     Offsets (int[Count] only for arrays with varlen types)    |
    /// |                         ...........                           |
    /// +---------------------------------------------------------------+
    /// |              Length (single offset for varlen type)           |
    /// +---------------------------------------------------------------+
    /// |                     Serialized Payload                      ...
    /// </summary>
    public class VariantSerializer
    {
        
    }
}
