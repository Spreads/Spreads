// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads
{
    internal enum CursorState : byte
    {
        /// <summary>
        /// A cursor is not initialized or disposed. Some initialization work could be needed before moving.
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
        [Obsolete("TODO BatchMoving in not reimplemented afte 0.8 changes, rethink if another state is needed for batches")]
        BatchMoving = 3,
    }
}