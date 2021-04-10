// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    [DebuggerDisplay("{ToString()}")]
    public partial class SegmentReader<TSegment, TValue> where TSegment : struct, ISegment<TSegment, TValue>
    {
        private TSegment _segment;
        private int _length;
        private int _position;

        public void Init(TSegment data)
        {
            _segment = data;
            _length = data.Length;
            _position = 0;
        }

        public int Length => _length;
        public int Position => _position;
        public TSegment Segment => _segment;

        public bool IsInRange
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _position < _length;
        }

        public TSegment Remaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _segment.Slice(_position);
        }

        /// <summary>
        /// Advances the reader position by <paramref name="length"/> chars if the resulting position is
        /// <see cref="IsInRange"/> or at the end of the segment.
        /// Otherwise returns false and keeps the reader <see cref="Position"/> unchanged.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Advance(int length)
        {
            if (unchecked((uint)(_position + length)) > _length)
                return false;

            _position += length;
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        public bool Advance() => Advance(1);

        public ref readonly TValue Current
        {
            get
            {
                if(IsInRange)
                    return ref _segment[_position];
                return ref Unsafe.NullRef<TValue>();
            }
        }

        public override string ToString()
        {
            return $"[{Position}..{Length}]: {(IsInRange ? Segment.Slice(Position) : "<end>")}";
        }
    }
}
