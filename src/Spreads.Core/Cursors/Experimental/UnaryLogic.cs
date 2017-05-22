// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads.Cursors.Experimental
{
    // We could either implement entire cursor like in ArithmeticSeries or have a virtual call - to a lambda or to overriden method.
    // Tests show so far that lambda is quite fast.

    public enum UnaryLogicOp
    {
        LT,
        LE,
        EQ,
        NEQ,
        GE,
        GT,
        LTReverse,
        LEReverse,
        GEReverse,
        GTReverse,
    }

    [Obsolete("Use CursorSeries")]
    public sealed class UnaryLogicSeries<TKey, TValue, TCursor> : MapValuesSeries<TKey, TValue, bool, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        public UnaryLogicSeries(TCursor cursor, TValue comparand, UnaryLogicOp op) : base(cursor, x => Apply(x, comparand, op))
        { }

        // TODO In ArithmeticSeries switch was worse than a sequence of ifs

        private static bool Apply(TValue input, TValue comparand, UnaryLogicOp op)
        {
            if (typeof(TValue) == typeof(double))
            {
                var v1 = (double)(object)(input);
                var v2 = (double)(object)(comparand);

                switch (op)
                {
                    case UnaryLogicOp.LT:
                        return v1 < v2;

                    case UnaryLogicOp.LE:
                        return v1 <= v2;

                    case UnaryLogicOp.EQ:
                        return v1 == v2;

                    case UnaryLogicOp.NEQ:
                        return v1 != v2;

                    case UnaryLogicOp.GE:
                        return v1 >= v2;

                    case UnaryLogicOp.GT:
                        return v1 > v2;

                    case UnaryLogicOp.LTReverse:
                        return v2 < v1;

                    case UnaryLogicOp.LEReverse:
                        return v2 <= v1;

                    case UnaryLogicOp.GEReverse:
                        return v2 >= v1;

                    case UnaryLogicOp.GTReverse:
                        return v2 > v1;

                    default:
                        return false;
                }
            }

            if (typeof(TValue) == typeof(float))
            {
                var v1 = (float)(object)(input);
                var v2 = (float)(object)(comparand);

                switch (op)
                {
                    case UnaryLogicOp.LT:
                        return v1 < v2;

                    case UnaryLogicOp.LE:
                        return v1 <= v2;

                    case UnaryLogicOp.EQ:
                        return v1 == v2;

                    case UnaryLogicOp.NEQ:
                        return v1 != v2;

                    case UnaryLogicOp.GE:
                        return v1 >= v2;

                    case UnaryLogicOp.GT:
                        return v1 > v2;

                    case UnaryLogicOp.LTReverse:
                        return v2 < v1;

                    case UnaryLogicOp.LEReverse:
                        return v2 <= v1;

                    case UnaryLogicOp.GEReverse:
                        return v2 >= v1;

                    case UnaryLogicOp.GTReverse:
                        return v2 > v1;

                    default:
                        return false;
                }
            }

            if (typeof(TValue) == typeof(int))
            {
                var v1 = (int)(object)(input);
                var v2 = (int)(object)(comparand);

                switch (op)
                {
                    case UnaryLogicOp.LT:
                        return v1 < v2;

                    case UnaryLogicOp.LE:
                        return v1 <= v2;

                    case UnaryLogicOp.EQ:
                        return v1 == v2;

                    case UnaryLogicOp.NEQ:
                        return v1 != v2;

                    case UnaryLogicOp.GE:
                        return v1 >= v2;

                    case UnaryLogicOp.GT:
                        return v1 > v2;

                    case UnaryLogicOp.LTReverse:
                        return v2 < v1;

                    case UnaryLogicOp.LEReverse:
                        return v2 <= v1;

                    case UnaryLogicOp.GEReverse:
                        return v2 >= v1;

                    case UnaryLogicOp.GTReverse:
                        return v2 > v1;

                    default:
                        return false;
                }
            }

            if (typeof(TValue) == typeof(long))
            {
                var v1 = (long)(object)(input);
                var v2 = (long)(object)(comparand);

                switch (op)
                {
                    case UnaryLogicOp.LT:
                        return v1 < v2;

                    case UnaryLogicOp.LE:
                        return v1 <= v2;

                    case UnaryLogicOp.EQ:
                        return v1 == v2;

                    case UnaryLogicOp.NEQ:
                        return v1 != v2;

                    case UnaryLogicOp.GE:
                        return v1 >= v2;

                    case UnaryLogicOp.GT:
                        return v1 > v2;

                    case UnaryLogicOp.LTReverse:
                        return v2 < v1;

                    case UnaryLogicOp.LEReverse:
                        return v2 <= v1;

                    case UnaryLogicOp.GEReverse:
                        return v2 >= v1;

                    case UnaryLogicOp.GTReverse:
                        return v2 > v1;

                    default:
                        return false;
                }
            }

            if (typeof(TValue) == typeof(decimal))
            {
                var v1 = (decimal)(object)(input);
                var v2 = (decimal)(object)(comparand);

                switch (op)
                {
                    case UnaryLogicOp.LT:
                        return v1 < v2;

                    case UnaryLogicOp.LE:
                        return v1 <= v2;

                    case UnaryLogicOp.EQ:
                        return v1 == v2;

                    case UnaryLogicOp.NEQ:
                        return v1 != v2;

                    case UnaryLogicOp.GE:
                        return v1 >= v2;

                    case UnaryLogicOp.GT:
                        return v1 > v2;

                    case UnaryLogicOp.LTReverse:
                        return v2 < v1;

                    case UnaryLogicOp.LEReverse:
                        return v2 <= v1;

                    case UnaryLogicOp.GEReverse:
                        return v2 >= v1;

                    case UnaryLogicOp.GTReverse:
                        return v2 > v1;

                    default:
                        return false;
                }
            }

            return ApplyDynamic(input, comparand, op);
        }

        private static bool ApplyDynamic(TValue input, TValue _value, UnaryLogicOp op)
        {
            var v1 = (dynamic)input;
            var v2 = (dynamic)_value;
            switch (op)
            {
                case UnaryLogicOp.LT:
                    return v1 < v2;

                case UnaryLogicOp.LE:
                    return v1 <= v2;

                case UnaryLogicOp.EQ:
                    return v1 == v2;

                case UnaryLogicOp.NEQ:
                    return v1 != v2;

                case UnaryLogicOp.GE:
                    return v1 >= v2;

                case UnaryLogicOp.GT:
                    return v1 > v2;

                case UnaryLogicOp.LTReverse:
                    return v2 < v1;

                case UnaryLogicOp.LEReverse:
                    return v2 <= v1;

                case UnaryLogicOp.GEReverse:
                    return v2 >= v1;

                case UnaryLogicOp.GTReverse:
                    return v2 > v1;

                default:
                    return false;
            }
        }
    }
}