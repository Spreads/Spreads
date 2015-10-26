//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Spreads.Persistence {

//    public enum CommandCode : byte {
//        Subscribe = 0,
//        Unsubscribe = 1,
//        CreateSeries = 2,
//        UpdateSeries = 3,
//        SeriesDefinition = 4,
//        SetData = 5,
//        RemoveData = 6,
//        CompleteData = 7,
//        AcquireLock = 8,
//        ReleaseLock = 9,
//        Error = 10,
//        Resend = 11,
//        Batch = 12,
//        Share = 13,
//    }

//    [Flags]
//    public enum CommandFlags : byte {
//        None = 0,
//        /// <summary>
//        /// If set, indicates that payload was compressed using Blosc
//        /// </summary>
//        Compressed = 1,
//        /// <summary>
//        /// If set, indicates that payload was encrypted. FormatVersion property of a data event should indicate the encryption scheme used.
//        /// </summary>
//        Encrypted = 2,
//        /// <summary>
//        /// Data length is variable. First 4 bytes of payload contain data length. Otherwise data is packed sequentially and its width is known by series id.
//        /// </summary>
//        VariableLength = 4,
//    }

//    public interface ICommandPayload
//    {
//        /// <summary>
//        /// Buffer segment containing a command payload. Does not includes variable leangth header.
//        /// </summary>
//        ArraySegment<byte> Bytes { get; }
//    }

//    public interface ICommand<out T> where T : ICommandPayload {
//        ArraySegment<byte> SeriesId { get; }

//        long SequenceId { get; }

//        CommandCode Command { get; }
//        CommandFlags Flags { get; }
//        byte FormatVersion { get; }
//        short ElementCount { get; }

//        int VariableDataLength { get; }
//        T Payload { get; }
//    }
//}
