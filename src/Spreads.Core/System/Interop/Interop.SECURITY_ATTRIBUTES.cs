// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    /// <summary>
    /// Blittable version of Windows BOOL type. It is convenient in situations where
    /// manual marshalling is required, or to avoid overhead of regular bool marshalling.
    /// </summary>
    /// <remarks>
    /// Some Windows APIs return arbitrary integer values although the return type is defined
    /// as BOOL. It is best to never compare BOOL to TRUE. Always use bResult != BOOL.FALSE
    /// or bResult == BOOL.FALSE .
    /// </remarks>
    internal enum BOOL : int {
        FALSE = 0,
        TRUE = 1,
    }

    internal partial class mincore
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct SECURITY_ATTRIBUTES
        {
            internal uint nLength;
            internal IntPtr lpSecurityDescriptor;
            internal BOOL bInheritHandle;
        }
    }
}
