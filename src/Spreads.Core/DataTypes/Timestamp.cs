using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spreads.DataTypes {

    public struct Timestamp {
        /// <summary>
        /// Nanoseconds since UNIX epoch
        /// </summary>
        private long _nanoseconds; // long is enough for 584 years
    }
}
