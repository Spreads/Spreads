using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Persistence {

    public enum Command : byte {
        Unsubscribe = 0,
        Subscribe = 1,
        Set,
        Remove,
        Append,
        Lock
    }

    public class BaseCommand {
        protected BaseCommand(Command command)
        {
            Command = command;
        }
        public Command Command { get; }
        public string SeriesId { get; set; }
    }


    public class SubscribeFromCommand : BaseCommand {
        public SubscribeFromCommand() : base(Command.Subscribe)
        {
            
        }
        /// <summary>
        /// Bytes representation of a key from which a sender of the message want to subscribe to the SeriesId
        /// </summary>
        public byte[] FromKeyBytes { get; set; }
    }

    public class SetCommand : BaseCommand {
        public SetCommand() : base(Command.Set) {}
        /// <summary>
        /// A sorted map with all new values we should apply via set
        /// </summary>
        public byte[] SerializedSortedMap { get; set; }
    }

    public class AppendCommand : BaseCommand {
        public AppendCommand() : base(Command.Append) {}

        /// <summary>
        /// A sorted map to append
        /// </summary>
        public byte[] SerializedSortedMap { get; set; }

        public AppendOption AppendOption { get; set; }
    }


    public delegate void SeriesCommandHandler(BaseCommand seriesCommand);

    /// <summary>
    /// Sends and recives commands for series repository
    /// </summary>
    public interface ISeriesNode {
        /// <summary>
        /// Send command and wait for reply
        /// </summary>
        Task<BaseCommand> Send(BaseCommand command);

        event SeriesCommandHandler OnCommand;
    }



    public delegate void BlobCommandHandler(byte[] blobSeriesCommand);

    /// <summary>
    /// Sends and recives commands for series repository as byte arrays.
    /// Simplest inefficient messenger to use any transport that supports sending byte arrays
    /// </summary>
    public interface IBinaryMessenger {
        Task<byte[]> Send(byte[] command);

        event BlobCommandHandler OnCommand;
    }


    public class BlobSeriesNode : ISeriesNode {
        private readonly IBinaryMessenger _binaryMessenger;

        public BlobSeriesNode(IBinaryMessenger binaryMessenger) {
            _binaryMessenger = binaryMessenger;
            _binaryMessenger.OnCommand += BinaryMessengerOnCommand;
        }


        private static BaseCommand ParseCommand(byte[] blobCommand)
        {
            var br = new BinaryReader(new MemoryStream(blobCommand));
            var cmd = (Command) br.ReadByte();
            var idLen = br.ReadInt32();
            var seriesid = br.ReadString();
            
            switch (cmd)
            {
                case Command.Unsubscribe:
                    throw new NotImplementedException("TODO");
                    break;
                case Command.Subscribe:
                    var keyLen = br.ReadInt32();
                    var keyBytes = br.ReadBytes(keyLen);
                    return new SubscribeFromCommand
                    {
                        FromKeyBytes = keyBytes,
                        SeriesId = seriesid
                    };
                case Command.Set:
                    var setBodyLen = br.ReadInt32();
                    var setBodyBytes = br.ReadBytes(setBodyLen);
                    return new SetCommand()
                    {
                        SeriesId = seriesid,
                        SerializedSortedMap = setBodyBytes
                    };
                case Command.Remove:
                    throw new NotImplementedException("TODO");
                    break;
                case Command.Append:
                    var appendOption = (AppendOption) br.ReadByte();
                    var appendBodyLen = br.ReadInt32();
                    var appendBodyBytes = br.ReadBytes(appendBodyLen);
                    return new AppendCommand()
                    {
                        SeriesId = seriesid,
                        AppendOption = appendOption,
                        SerializedSortedMap = appendBodyBytes
                    };
                case Command.Lock:
                    throw new NotImplementedException("TODO");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void BinaryMessengerOnCommand(byte[] blobSeriesCommand)
        {
            var command = ParseCommand(blobSeriesCommand);
            OnCommand?.Invoke(command);
        }

        public async Task<BaseCommand> Send(BaseCommand command)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            // command type header
            bw.Write((byte) command.Command);
            // seriesid lenght
            bw.Write(command.SeriesId.Length);
            // series id
            bw.Write(command.SeriesId);

            switch (command.Command)
            {
                case Command.Unsubscribe:
                    throw new NotImplementedException("TODO");
                    break;

                case Command.Subscribe:
                    var subscribe = command as SubscribeFromCommand;
                    Trace.Assert(subscribe != null);
                    bw.Write(subscribe.FromKeyBytes.Length);
                    bw.Write(subscribe.FromKeyBytes);
                    break;

                case Command.Set:
                    var set = command as SetCommand;
                    Trace.Assert(set != null);
                    bw.Write(set.SerializedSortedMap.Length);
                    bw.Write(set.SerializedSortedMap);
                    break;

                case Command.Remove:
                    throw new NotImplementedException("TODO");
                    break;

                case Command.Append:
                    var append = command as AppendCommand;
                    Trace.Assert(append != null);
                    bw.Write((byte) append.AppendOption);
                    bw.Write(append.SerializedSortedMap.Length);
                    bw.Write(append.SerializedSortedMap);
                    break;

                case Command.Lock:
                    throw new NotImplementedException("TODO");
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
            bw.Flush();

            var response = await _binaryMessenger.Send(ms.ToArray());
            var responseCommand = ParseCommand(response);
            return responseCommand;
        }

        public event SeriesCommandHandler OnCommand;
    }
}
