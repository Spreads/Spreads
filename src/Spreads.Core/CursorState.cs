// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads
{
    internal enum CursorState : byte
    {
        /// <summary>
        /// Not initialized.
        /// </summary>
        None = 0,

        /// <summary>
        /// Initialized via GetCursor() call of ISeries and is ready to move.
        /// The cursor is nowhere with this state.
        /// </summary>
        Initialized = 1,

        /// <summary>
        /// Started moving and is at valid position.
        /// A false move from this state must restore cursor to its position before the move.
        /// </summary>
        Moving = 2,

        /// <summary>
        /// Cursor is used for IReadOnlySeries implementation.
        /// </summary>
        Navigating = 3
    }
}