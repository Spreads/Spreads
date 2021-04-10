using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Buffers
{

    public interface ISegment<T> : IEquatable<T>, IComparable<T>
    {
        int Start { get; }
        int Length { get; }
        int End { get; }

        bool IsEmpty { get; }

        T Slice(int start);
        T Slice(int start, int length);
    }

    public interface ISegment<TSegment, TValue> : ISegment<TSegment>
    {
        ref readonly TValue First { get; }
        ref readonly TValue this[int index] { get; }
        ReadOnlySpan<TValue> Span { get; }
    }

    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public readonly struct Segment : ISegment<Segment>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Segment(int start, int length)
        {
            _start = start;
            _length = length;
        }

        [FieldOffset(0)]
        private readonly int _start;
        [FieldOffset(4)]
        private readonly int _length;

        public int Start => _start;
        public int Length => _length;
        public int End => Start + Length;
        public bool IsEmpty => _length == 0;

        public Segment Slice(int start)
        {
            if ((uint) start > (uint) _length)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
            return new Segment(_start + start, _length - start);
        }

        public Segment Slice(int start, int length)
        {
            ThrowHelper.EnsureOffsetLength(start, length, _length);
            return new Segment(_start + start, length);
        }

        public static Segment FromStartEnd(int start, int end)
        {
            return new(start, end - start);
        }

        public bool OverlapsWith(Segment span)
        {
            return Start < span.End && End > span.Start;
        }

        public override string ToString() => $"{Start}..{End}";

        public bool Equals(Segment other)
        {
            return Start == other.Start && Length == other.Length;
        }

        public override bool Equals(object? obj)
        {
            return obj is Segment other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Start * 397) ^ Length.GetHashCode();
            }
        }

        public int CompareTo(Segment other)
        {
            int startComparison = Start.CompareTo(other.Start);
            if (startComparison != 0)
                return startComparison;
            return Length.CompareTo(other.Length);
        }

        public void Deconstruct(out int start, out int length)
        {
            start = Start;
            length = Length;
        }
    }
}
