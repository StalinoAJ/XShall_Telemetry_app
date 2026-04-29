using System;
using SHALLControl.Models;

namespace SHALLControl.Plugins
{
    public interface IGamePlugin : IDisposable
    {
        string GameName  { get; }
        string Protocol  { get; }   // e.g. "UDP 5300"
        bool   IsRunning { get; }

        void Start();
        void Stop();

        event EventHandler<TelemetryData> TelemetryReceived;
    }
}
