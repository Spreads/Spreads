// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Spreads.Utils
{
    public static class StringExtensions
    {
        [ThreadStatic]
        private static MD5 _hasher;

        /// <summary>
        /// Translate MD5 hash of a string UTF8 bytes to Guid
        /// </summary>
        public static Guid MD5Guid(this string uniqueString)
        {
            if (_hasher == null) _hasher = MD5.Create();
            var hashValue = _hasher.ComputeHash(Encoding.UTF8.GetBytes(uniqueString));
            return new Guid(hashValue);
        }

        public static byte[] MD5Bytes(this string uniqueString)
        {
            if (_hasher == null) _hasher = MD5.Create();
            return _hasher.ComputeHash(Encoding.UTF8.GetBytes(uniqueString));
        }
    }
}