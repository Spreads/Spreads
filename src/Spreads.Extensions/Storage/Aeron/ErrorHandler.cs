using System;

namespace Spreads.Storage.Aeron
{
    /// <summary>
    /// Callback interface for handling an error/exception that has occurred when processing an operation or event.
    /// </summary>
    public delegate void ErrorHandler(Exception exception);
}