using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Persistence {

    public delegate Action<ArraySegment<byte[]>> CommandHandler(ArraySegment<byte[]> command);

    public interface IConnectionParameters
    {
        
    }

    public interface ISpreadsTransportOld
    {
        Task<ArraySegment<byte[]>> SendCommandAsync(ArraySegment<byte[]> command);
        event CommandHandler OnResponse;
        event CommandHandler OnUpdate;

        Task<bool> ConnectAsync(IConnectionParameters connectionParams);
        bool IsConnected { get; }
        void Disconnect();
    }



}
