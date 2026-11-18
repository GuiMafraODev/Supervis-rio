
using System;
using System.Collections.Generic;

namespace SupervisorioECA.Bridge.Opc
{
    public interface IOpcClient : IDisposable
    {
        bool IsConnected { get; }

        string ServerName { get; }

        event EventHandler ConnectionStateChanged;

        void Connect();

        void Disconnect();

        IList<OpcValue> ReadAll();

        OpcValue Read(string tagId);
    }
}
