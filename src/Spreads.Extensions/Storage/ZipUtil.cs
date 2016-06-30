using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage {

    // We already reference the IO.FileSystem in this project, it is easier to add helpers
    // here than re-use that reference, e.g. in FSI

    /// <summary>
    /// Helper methods for working with zip files
    /// </summary>
    public static class ZipUtil {
        /// <summary>
        /// Returns an IEnumerable of tuples with archive entries full names and lazy content streams
        /// </summary>
        /// <param name="zipFilePath"></param>
        /// <returns></returns>
        public static IEnumerable<Tuple<string, Lazy<Stream>>> ReadZipContents(string zipFilePath) {
            var archive = ZipFile.OpenRead(zipFilePath);
            return archive.Entries.Select(entry => Tuple.Create(entry.FullName, new Lazy<Stream>(entry.Open)));
        }
    }
}
