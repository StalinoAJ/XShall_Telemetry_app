using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SHALLControl.Models;

namespace SHALLControl.Plugins
{
    /// <summary>
    /// SnowRunner telemetry plugin.
    /// 
    /// SnowRunner does NOT have native telemetry output.
    /// This plugin listens for UDP data on port 21777 from a community
    /// memory-reading bridge tool (e.g. SnowRunner Telemetry Bridge).
    ///
    /// Expected UDP packet (little-endian, 48 bytes minimum):
    ///   Offset  0: float speed       (km/h)
    ///   Offset  4: float rpm
    ///   Offset  8: float steer       (-1 to 1)
    ///   Offset 12: float pitch       (radians)
    ///   Offset 16: float roll        (radians)
    ///   Offset 20: float yaw         (radians)
    ///   Offset 24: float accelX      (lateral G)
    ///   Offset 28: float accelY      (vertical G)
    ///   Offset 32: float accelZ      (longitudinal G)
    ///   Offset 36: float suspFL
    ///   Offset 40: float suspFR
    ///   Offset 44: float suspRL
    ///
    /// If no bridge is available, the plugin waits silently.
    /// </summary>
    public class SnowRunnerPlugin : IGamePlugin
    {
        public string GameName  => "SnowRunner";
        public string Protocol  => "UDP :21777";
        public bool   IsRunning => _running;

        public event EventHandler<TelemetryData> TelemetryReceived;

        private const int PORT = 21777;
        private Thread    _thread;
        private UdpClient _udp;
        private volatile bool _running;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _udp = new UdpClient(PORT);
            _udp.Client.ReceiveTimeout = 1000;
            _thread = new Thread(Loop) { IsBackground = true, Name = "SnowUDP" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _udp?.Close(); } catch { }
        }

        private void Loop()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] data = _udp.Receive(ref ep);
                    var t = Parse(data);
                    if (t != null) TelemetryReceived?.Invoke(this, t);
                }
                catch (SocketException) { /* timeout — keep waiting */ }
                catch { if (_running) Thread.Sleep(200); }
            }
        }

        private TelemetryData Parse(byte[] d)
        {
            if (d == null || d.Length < 36) return null;

            float speed   = BitConverter.ToSingle(d, 0);   // km/h
            float steer   = BitConverter.ToSingle(d, 8);   // -1 to 1
            float pitch   = BitConverter.ToSingle(d, 12);  // radians
            float roll    = BitConverter.ToSingle(d, 16);  // radians
            float yaw     = BitConverter.ToSingle(d, 20);  // radians
            float accelX  = BitConverter.ToSingle(d, 24);  // lateral G
            float accelZ  = BitConverter.ToSingle(d, 32);  // longitudinal G

            float absSpeed = Math.Abs(speed);

            // Off-road: lower deadzone (2 km/h) — slow crawling still matters
            if (absSpeed < 2f)
            {
                return new TelemetryData
                {
                    Pitch = 0, Roll = 0, Yaw = 0,
                    Speed = absSpeed, IsValid = true
                };
            }

            float pitchDeg = (float)(pitch * 180.0 / Math.PI);
            float rollDeg  = (float)(roll  * 180.0 / Math.PI);

            // SnowRunner is slow & terrain-heavy — emphasize body tilt
            return new TelemetryData
            {
                Pitch   = -(pitchDeg * 3f + accelZ * 2f),
                Roll    = -(rollDeg * 3.5f + accelX * 2.5f),
                Yaw     = -steer * 12f,
                Surge   = accelZ,
                Sway    = accelX,
                Speed   = absSpeed,
                IsValid = true
            };
        }

        public void Dispose() => Stop();
    }
}
