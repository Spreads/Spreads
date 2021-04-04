// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// Throw helper specific to serialization.
    /// </summary>
    internal static class SerializationThrowHelper
    {
        // Note: Cannot confirm that, but having verbatim string in a method
        // prevents inlining because sting becomes a part of method body.

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowBadTypeEnum(byte typeEnumValue)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException($"TypeEnum value is out of range 0 - 126: {typeEnumValue}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowFixedSizeOutOfRange(byte typeSizeValue)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException($"Type size value is out of range 1 - 128: {typeSizeValue}");
        }
    }
}