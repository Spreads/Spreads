// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Generated code, edit Symbol.cs and copy its changes to Symbol.tt to make changes (TODO leave only Symbol.tt)

using System;
using System.Diagnostics;
using System.Text;
using Spreads.Buffers;

namespace Spreads.DataTypes {

	// See https://codeblog.jonskeet.uk/2011/04/05/of-memory-and-strings/
    // why this has a lot of sense: on x64 a string takes 26 + length * 2,
    // so we always win for small strings even with padding.
	
}