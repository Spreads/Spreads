// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.Buffers
{
    public partial class SegmentReader<TSegment, TValue>
    {
        public Scope GetScope() => new(this);

        public PeekScope GetPeekScope() => new(this);

        public readonly ref struct Scope
        {
            private readonly SegmentReader<TSegment, TValue> _reader;
            private readonly int _startPosition;

            public SegmentReader<TSegment, TValue> Reader => _reader;
            public int StartPosition => _startPosition;

            public Scope(SegmentReader<TSegment, TValue> reader)
            {
                _reader = reader;
                _startPosition = _reader._position;
            }

            public void Rollback() => _reader._position = _startPosition;
        }

        public readonly ref struct PeekScope
        {
            private readonly Scope _scope;

            public Scope Scope => _scope;

            public PeekScope(SegmentReader<TSegment, TValue> reader)
            {
                _scope = new Scope(reader);
            }

            public void Dispose() => _scope.Rollback();
        }
    }
}
