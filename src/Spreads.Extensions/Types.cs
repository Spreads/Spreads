using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {
    
    // TODO (@VB) I have somewhere almost complete implementation of Price structs, copy it here

    public struct Price
    {
        private ulong value;
        private const int precisionOffset = 60;
        private const ulong precisionMask = (2 ^ (64 - precisionOffset) - 1UL) << precisionOffset;
    }
}
