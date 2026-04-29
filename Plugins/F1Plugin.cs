using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SHALLControl.Models;

namespace SHALLControl.Plugins
{
    /// <summary>
    /// F1 (Codemasters/EA) UDP telemetry plugin.
    /// Supports F1 2018 – F1 2023 Codemasters format.
    /// In-game: Settings → Telemetry → UDP On, IP: 127.0.0.1, Port: 20777
    ///
    /// Packet layout (Motion packet, ID=0):
    ///   Bytes 0-1   : packetFormat  (uint16)
    ///   Byte  5     : packetId      (F1 2018 header=21 bytes)
    ///   Byte  20/23 : playerCarIndex
    ///   Then 20 or 22 CarMotionData structs (60 bytes each):
    ///     +36 gForceLateral, +40 gForceLongitudinal, +44 gForceVertical
    ///     +48 yaw, +52 pitch, +56 roll  (all floats, radians)
    /// </summary>
    public class F1Plugin : IGamePlugin
    {
        public string GameName  => "F1 Series";
        public string Protocol  => "UDP :20777";
        public bool   IsRunning => _running;

        public event EventHandler<TelemetryData> TelemetryReceived;

        private const int PORT = 20777;
        private Thread    _thread;
        private UdpClient _udp;
        private volatile bool _running;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _udp = new UdpClient(PORT);
            _udp.Client.ReceiveTimeout = 1000;
            _thread = new Thread(Loop) { IsBackground = true, Name = "F1UDP" };
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
                catch (SocketException) { /* timeout */ }
                catch { if (_running) Thread.Sleep(200); }
            }
        }

        private TelemetryData Parse(byte[] d)
        {
            if (d == null || d.Length < 60) return null;

            // Detect header size from packetFormat
            ushort fmt = BitConverter.ToUInt16(d, 0);

            int headerSize, playerIndexOffset, carCount;
            if (fmt == 2018)
            {
                headerSize = 21; playerIndexOffset = 20; carCount = 20;
            }
            else  // 2019/2020/2021/2022/2023
            {
                headerSize = 24; playerIndexOffset = 23; carCount = 22;
            }

            if (d.Length < headerSize + 1) return null;

            // Read packet ID
            byte packetId = d[fmt == 2018 ? 3 : 5];
            if (packetId != 0) return null;  // Only process Motion packet

            byte playerIdx = d[playerIndexOffset];
            if (playerIdx >= carCount) return null;

            int carOffset = headerSize + playerIdx * 60;
            if (d.Length < carOffset + 60) return null;

            // CarMotionData layout (60 bytes):
            //  0-11:  worldPosition (3 floats)
            // 12-23:  worldVelocity (3 floats)
            // 24-29:  worldForwardDir (3 int16)
            // 30-35:  worldRightDir   (3 int16)
            // 36:     gForceLateral   (float)
            // 40:     gForceLongitudinal (float)
            // 44:     gForceVertical  (float)
            // 48:     yaw             (float, radians)
            // 52:     pitch           (float, radians)
            // 56:     roll            (float, radians)

            float gLateral = BitConverter.ToSingle(d, carOffset + 36);
            float gLong    = BitConverter.ToSingle(d, carOffset + 40);
            float gVert    = BitConverter.ToSingle(d, carOffset + 44);
            float yaw      = BitConverter.ToSingle(d, carOffset + 48);
            float pitch    = BitConverter.ToSingle(d, carOffset + 52);
            float roll     = BitConverter.ToSingle(d, carOffset + 56);

            return new TelemetryData
            {
                Pitch  = (float)(pitch * 180.0 / Math.PI),
                Roll   = (float)(roll  * 180.0 / Math.PI),
                Yaw    = gLateral * 5f,   // lateral G → yaw feel
                Surge  = gLong,
                Sway   = gLateral,
                Heave  = gVert,
                IsValid = true
            };
        }

        public void Dispose() => Stop();
    }
}
