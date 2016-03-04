/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {

    public static class StringExtensions {

        /// <summary>
        ///     Translate MD5 hash of a string to Guid with zero epoch
        /// </summary>
        public static Guid MD5Guid(this string uniqueString) {
            var hasher = MD5.Create();
            var hashValue = hasher.ComputeHash(Encoding.UTF8.GetBytes(uniqueString));
            return new Guid(hashValue);
        }

        public static byte[] MD5Bytes(this string uniqueString) {
            var hasher = MD5.Create();
            return hasher.ComputeHash(Encoding.UTF8.GetBytes(uniqueString));
        }

    }
}
