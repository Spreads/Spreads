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
        /// A cursor is initialized via GetCursor() call of ISeries and is ready to move.
        /// The cursor is nowhere with this state.
        /// </summary>
        Initialized = 1,

        /// <summary>
        /// A cursor has started moving and is at valid position.
        /// A false move from this state must restore the cursor to its position before the move.
        /// </summary>
        Moving = 2,

        /// <summary>
        /// A cursor has started batch moving and is at a valid position.
        /// A false move from this state must restore the cursor to its position before the move.
        /// </summary>
        BatchMoving = 3,

        /// <summary>
        /// A cursor is used for IReadOnlySeries implementation.
        /// </summary>
        Navigating = 255
    }
}