// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Core.Tests
{
    public class ClassWithAPrivateField
    {
        private int _size;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int MethodThatAccessesSize()
        {
            // whatever code that uses size
            return _size;
        }
    }

    public class AnotherClass
    {
        private ClassWithAPrivateField _instance;

        public void Test()
        {
            // _size is not visible here, could MethodThatAccessesSize be ever JIT-inlined?
            Console.WriteLine(_instance.MethodThatAccessesSize());
        }
    }

    internal class UnsafeTests
    {
    }
}