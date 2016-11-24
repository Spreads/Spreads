// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


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
