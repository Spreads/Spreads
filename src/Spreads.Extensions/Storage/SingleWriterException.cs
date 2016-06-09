using System;

namespace Spreads.Storage
{
    public class SingleWriterException : InvalidOperationException {
        public SingleWriterException() : base("The series is already opened for write. Only a single writer is allowed.") { }
    }
}