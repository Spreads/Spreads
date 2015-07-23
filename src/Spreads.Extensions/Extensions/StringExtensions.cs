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
        
    }
}
