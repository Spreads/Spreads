// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Spreads.Serialization.Utf8Json;

namespace Spreads.DataTypes
{
    /// <summary>
    /// Date stored as a number of days since zero.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Json
    {
        private readonly ArraySegment<byte> _value;

        public Json(ArraySegment<byte> value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return Encoding.UTF8.GetString(_value.Array, _value.Offset, _value.Count);
        }

        public T Deserialize<T>()
        {
            return JsonSerializer.Deserialize<T>(_value.Array, _value.Offset);
        }
    }    
}
