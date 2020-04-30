// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.InteropServices;

namespace Spreads
{
    // int32, we could later reuse negative space for other flags if needed. For MoveNext we should be able to do just `>0` to continue

    // ReSharper disable once EnumUnderlyingTypeIsInt do not change to shorter
    public enum CursorState : byte
    {
        /// <summary>
        /// A cursor is not initialized or is disposed. Some (re)initialization work may be needed before moving.
        /// </summary>
        Disposed = 0,

        /// <summary>
        /// A cursor is initialized via GetCursor() call of ISeries and is ready to move.
        /// The cursor is "nowhere" with this state (before the first element for <see cref="ICursor{TKey,TValue}.MoveNext()"/>, after the last element for <see cref="ICursor{TKey,TValue}.MovePrevious()"/>).
        /// </summary>
        Initialized = 1,

        /// <summary>
        /// A cursor has started moving and is at a valid position.
        /// A false move from this state must restore the cursor to its position before the move.
        /// </summary>
        Moving = 2,
        
        // /// <summary>
        // /// A cursor has started batch moving and is at a valid position.
        // /// </summary>
        Batch = 3
    }
}