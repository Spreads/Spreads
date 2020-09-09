// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Collections
{
    internal sealed partial class DataContainer
    {
        private static readonly ConditionalWeakTable<DataContainer, Dictionary<string, object>> Attributes =
            new ConditionalWeakTable<DataContainer, Dictionary<string, object>>();

        /// <summary>
        /// Get an attribute that was set using SetAttribute() method.
        /// </summary>
        /// <param name="attributeName">Name of an attribute.</param>
        /// <returns>Return an attribute value or null is the attribute is not found.</returns>
        public object? GetAttribute(string attributeName)
        {
            if (Attributes.TryGetValue(this, out Dictionary<string, object> dic))
            {
                lock (dic)
                {
                    if (dic.TryGetValue(attributeName, out object res))
                    {
                        return res;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Set any custom attribute to a series. An attribute is available during lifetime of a series and is available via GetAttribute() method.
        /// </summary>
        public void SetAttribute(string attributeName, object attributeValue)
        {
            // GetOrCreateValue is thread-safe
            var dic = Attributes.GetOrCreateValue(this);
            lock (dic)
            {
                dic[attributeName] = attributeValue;
            }
        }
    }
}