// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;

namespace Spreads
{
    public readonly struct Position
    {
        public static Position<T> At<T>(T key) => new Position<T>(LookupEx.EQ, key);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        internal enum LookupEx : byte
        {
            First = 0,
            Last = 254,
            NewOnly = 255,
            After = Lookup.GT,
            GT = Lookup.GT,
            Before = Lookup.LT,
            LT = Lookup.LT,
            At = Lookup.EQ,
            EQ = Lookup.EQ,
            AtOrAfter = Lookup.GE,
            GE = Lookup.GE,
            AtOrBefore = Lookup.LE,
            LE = Lookup.LE,
        }
    }

    public readonly struct Position<T>
    {
        internal readonly Position.LookupEx Direction;

        internal readonly T Key;

        internal Position(Position.LookupEx direction, T key = default)
        {
            Direction = direction;
            Key = key;
        }

        public static readonly Position<T> First = default;
        public static readonly Position<T> Last = new Position<T>(Position.LookupEx.Last);
        public static readonly Position<T> NewOnly = new Position<T>(Position.LookupEx.NewOnly);

        public static Position<T> At(T key) => new Position<T>(Position.LookupEx.EQ, key);
    }
}
