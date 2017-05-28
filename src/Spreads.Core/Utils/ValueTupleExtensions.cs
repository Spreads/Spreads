// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Spreads.Utils
{
    public static class ValueTupleExtensions
    {
        public static bool And(this (bool, bool) tuple)
        {
            return tuple.Item1 && tuple.Item2;
        }

        public static bool Or(this (bool, bool) tuple)
        {
            return tuple.Item1 || tuple.Item2;
        }

        public static (TKey key, TValue value) AsTuple<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp)
        {
            return (kvp.Key, kvp.Value);
        }

    }
}