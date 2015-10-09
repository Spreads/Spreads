using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Threading.Tasks {


    /// <summary>
    /// Follows the logic of pivot/cont keys but uses the heaps for task scheduling
    /// </summary>
    [Obsolete("Potentially there is a way to speed up ZipN with a custom scheduler. But due to locality issues parallel gains are not obvious. Much better to split vArr in ZipN into multiple-of-64-byte regions so that each thread fills its own region and avoids false sharing.")]
    internal class ZipNTaskScheduler {

    }
}
