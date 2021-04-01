using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads
{
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public readonly struct Segment : IEquatable<Segment>, IComparable<Segment>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Segment(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public readonly int Start;
        public readonly int Length;
        public int End => Start + Length;

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
