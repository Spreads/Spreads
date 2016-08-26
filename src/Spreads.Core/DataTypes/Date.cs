using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Spreads.DataTypes {
    // TODO these are stubs until we define what separate Date and Time are
    // Obvious solution for Date is signed day number from zero, ignoring calendar issues
    // For Time, max precision (nanos - only 4 sec, only millis will work)

    // NB Remeber that we need these for untyped access, e.g. when other side of 
    // interop knows nothing and need to present at least something from data,
    // e.g. at least print


    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 4)]
    public struct Date {
        private readonly int _value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 4)]
    public struct Time {
        private readonly int _value;
    }
}
