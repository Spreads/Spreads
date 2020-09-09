using System;

namespace Spreads
{
    public struct Unit : IComparable<Unit>, IEquatable<Unit>
    {
        public int CompareTo(Unit other) => 0;

        public bool Equals(Unit other) => true;

        public override bool Equals(object? obj) => obj is Unit other && Equals(other);

        public override int GetHashCode() => 0;
    }
}