// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable once CheckNamespace

using System;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class ILSub : Attribute
    {
        public ILSub(string il)
        {
        }
    }

    /// <summary>
    /// A marker attribute to add manually aggressive inlining attribute to IL.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal class RewriteAILAttribute : Attribute
    {

    }

}