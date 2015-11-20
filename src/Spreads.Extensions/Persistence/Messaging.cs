//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Spreads.Persistence {


//    public enum Command : byte {
//        Unsubscribe = 0,
//        Subscribe = 1,
//        Set = 2,
//        Remove = 3,
//        Append = 4,
//        Lock =5,
//        /// <summary>
//        /// There is no more data
//        /// </summary>
//        Complete = 6
//    }




//    public class BaseCommand {
//        protected BaseCommand(Command command)
//        {
//            Command = command;
//        }
//        public Command Command { get; }
//        public string SeriesId { get; set; }
//        public long Version { get; set; }
//    }


//    public class SubscribeFromCommand : BaseCommand {
//        public SubscribeFromCommand() : base(Command.Subscribe)
//        {
            
//        }
//        /// <summary>
//        /// Bytes representation of a key from which a sender of the message want to subscribe to the SeriesId
//        /// </summary>
//        public byte[] FromKeyBytes { get; set; }
//    }

//    public class SetCommand : BaseCommand {
//        public SetCommand() : base(Command.Set) {}
//        /// <summary>
//        /// A sorted map with all new values we should apply via set
//        /// </summary>
//        public byte[] SerializedSortedMap { get; set; }
//    }

//    public class AppendCommand : BaseCommand {
//        public AppendCommand() : base(Command.Append) {}

//        /// <summary>
//        /// A sorted map to append
//        /// </summary>
//        public byte[] SerializedSortedMap { get; set; }

//        public AppendOption AppendOption { get; set; }
//    }

//    public class CompleteCommand : BaseCommand {
//        public CompleteCommand() : base(Command.Complete) {

//        }
//    }

//    public class LockCommand : BaseCommand {
//        public LockCommand() : base(Command.Complete) {

//        }
//    }

//    public class RemoveCommand : BaseCommand {
//        public RemoveCommand() : base(Command.Remove) {

//        }

//        public Lookup Direction { get; set; }
//        /// <summary>
//        /// Bytes representation of a key from which a sender of the message want to subscribe to the SeriesId
//        /// </summary>
//        public byte[] KeyBytes { get; set; }
//    }

//    public delegate void SeriesCommandHandler(BaseCommand seriesCommand);

//    /// <summary>
//    /// Sends and recives commands for series repository
//    /// </summary>
//    public interface ISeriesNode {
//        /// <summary>
//        /// Send command and wait for reply
//        /// </summary>
//        Task<BaseCommand> Send(BaseCommand command);

//        event SeriesCommandHandler OnNewData;
//        event SeriesCommandHandler OnDataLoad;
//    }



//    public delegate void BlobCommandHandler(ArraySegment<byte> blobSeriesCommand);

//    /// <summary>
//    /// Sends and recives commands for series repository as byte arrays.
//    /// Simplest inefficient messenger to use any transport that supports sending byte arrays
//    /// </summary>
//    public interface IBinaryMessenger {
//        Task<ArraySegment<byte>> Send(ArraySegment<byte> command);

//        event BlobCommandHandler OnNewData;
//        event BlobCommandHandler OnDataLoad;
//    }


//    public class BinarySeriesNode : ISeriesNode {
//        private readonly IBinaryMessenger _binaryMessenger;

//        public BinarySeriesNode(IBinaryMessenger binaryMessenger) {
//            _binaryMessenger = binaryMessenger;
//            _binaryMessenger.OnNewData += BinaryMessengerOnNewData;
//            _binaryMessenger.OnDataLoad += BinaryMessengerOnDataLoad;
//        }


//        public static BaseCommand ParseCommand(ArraySegment<byte> blobCommand)
//        {
//            try
//            {
//                var br = new BinaryReader(new MemoryStream(blobCommand.Array, blobCommand.Offset, blobCommand.Count));
//                var cmd = (Command) br.ReadByte();
//                var idLen = br.ReadInt32();
//                var seriesid = br.ReadString();
//                var version = br.ReadInt64();

//                switch (cmd)
//                {
//                    case Command.Unsubscribe:
//                        throw new NotImplementedException("TODO");
//                        break;
//                    case Command.Subscribe:
//                        var keyLen = br.ReadInt32();
//                        var keyBytes = br.ReadBytes(keyLen);
//                        return new SubscribeFromCommand
//                        {
//                            Version = version,
//                            FromKeyBytes = keyBytes,
//                            SeriesId = seriesid
//                        };
//                    case Command.Set:
//                        var setBodyLen = br.ReadInt32();
//                        var setBodyBytes = br.ReadBytes(setBodyLen);
//                        return new SetCommand()
//                        {
//                            Version = version,
//                            SeriesId = seriesid,
//                            SerializedSortedMap = setBodyBytes
//                        };
//                    case Command.Remove:
//                        var direction = (Lookup) br.ReadInt32();
//                        var remKeyLen = br.ReadInt32();
//                        var remKeyBytes = br.ReadBytes(remKeyLen);
//                        return new RemoveCommand
//                        {
//                            Direction = direction,
//                            Version = version,
//                            KeyBytes = remKeyBytes,
//                            SeriesId = seriesid
//                        };
//                    case Command.Append:
//                        var appendOption = (AppendOption) br.ReadByte();
//                        var appendBodyLen = br.ReadInt32();
//                        var appendBodyBytes = br.ReadBytes(appendBodyLen);
//                        return new AppendCommand()
//                        {
//                            Version = version,
//                            SeriesId = seriesid,
//                            AppendOption = appendOption,
//                            SerializedSortedMap = appendBodyBytes
//                        };
//                    case Command.Lock:
//                        throw new NotImplementedException("TODO");
//                        break;
//                    case Command.Complete:
//                        return new CompleteCommand()
//                        {
//                            Version = version,
//                            SeriesId = seriesid
//                        };
//                    default:
//                        return null;
//                }
//            }
//            catch
//            {
//                return null;
//            }
//        }

//        private void BinaryMessengerOnNewData(ArraySegment<byte> blobSeriesCommand)
//        {
//            var command = ParseCommand(blobSeriesCommand);
//            OnNewData?.Invoke(command);
//        }

//        private void BinaryMessengerOnDataLoad(ArraySegment<byte> blobSeriesCommand) {
//            var command = ParseCommand(blobSeriesCommand);
//            OnDataLoad?.Invoke(command);
//        }

//        public async Task<BaseCommand> Send(BaseCommand command)
//        {
//            var bytes = SerializeCommand(command);

//            var response = await _binaryMessenger.Send(new ArraySegment<byte>(bytes));
//            var responseCommand = ParseCommand(response);
//            return responseCommand;
//        }

//        public static byte[] SerializeCommand(BaseCommand command)
//        {
//            var ms = new MemoryStream();
//            var bw = new BinaryWriter(ms);

//            // command type header
//            bw.Write((byte) command.Command);
//            // seriesid lenght
//            bw.Write(command.SeriesId.Length);
//            // series id
//            bw.Write(command.SeriesId);
//            bw.Write(command.Version);

//            switch (command.Command)
//            {
//                case Command.Unsubscribe:
//                    throw new NotImplementedException("TODO");
//                    break;

//                case Command.Subscribe:
//                    var subscribe = command as SubscribeFromCommand;
//                    Trace.Assert(subscribe != null);
//                    bw.Write(subscribe.FromKeyBytes.Length);
//                    bw.Write(subscribe.FromKeyBytes);
//                    break;

//                case Command.Set:
//                    var set = command as SetCommand;
//                    Trace.Assert(set != null);
//                    bw.Write(set.SerializedSortedMap.Length);
//                    bw.Write(set.SerializedSortedMap);
//                    break;

//                case Command.Remove:
//                    var remove = command as RemoveCommand;
//                    Trace.Assert(remove != null);
//                    bw.Write((int) remove.Direction);
//                    bw.Write(remove.KeyBytes.Length);
//                    bw.Write(remove.KeyBytes);
//                    break;

//                case Command.Append:
//                    var append = command as AppendCommand;
//                    Trace.Assert(append != null);
//                    bw.Write((byte) append.AppendOption);
//                    bw.Write(append.SerializedSortedMap.Length);
//                    bw.Write(append.SerializedSortedMap);
//                    break;

//                case Command.Lock:
//                    throw new NotImplementedException("TODO");
//                    break;
//                case Command.Complete:
//                    var complete = command as CompleteCommand;
//                    Trace.Assert(complete != null);
//                    break;
//                default:
//                    throw new ArgumentOutOfRangeException();
                
//            }
//            bw.Flush();
//            var bytes = ms.ToArray();
//            return bytes;
//        }

//        public event SeriesCommandHandler OnNewData;
//        public event SeriesCommandHandler OnDataLoad;
//    }
//}
