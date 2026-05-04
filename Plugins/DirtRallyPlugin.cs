using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SHALLControl.Models;

namespace SHALLControl.Plugins
{
    /// <summary>
    /// Dirt Rally / Dirt Rally 2.0 UDP telemetry plugin.
    /// Uses the Codemasters UDP protocol on port 20777.
    ///
    /// Enable in hardware_settings_config.xml:
    ///   Documents\My Games\DiRT Rally 2.0\hardwaresettings\
    ///     <udp enabled="true" extradata="3" ip="127.0.0.1" port="20777" delay="1" />
    ///
    /// Codemasters "extradata=3" packet (264 bytes):
    ///   Offset  0: float time
    ///   Offset  4: float lapTime
    ///   Offset  8: float lapDistance
    ///   Offset 12: float totalDistance
    ///   Offset 16: float posX
    ///   Offset 20: float posY
    ///   Offset 24: float posZ
    ///   Offset 28: float speed           (m/s)
    ///   Offset 32: float velX            (m/s)
    ///   Offset 36: float velY            (m/s)
    ///   Offset 40: float velZ            (m/s)
    ///   Offset 44: float rollX           (forward dir X)
    ///   Offset 48: float rollY           (forward dir Y)
    ///   Offset 52: float rollZ           (forward dir Z)
    ///   Offset 56: float pitchX          (right dir X)
    ///   Offset 60: float pitchY          (right dir Y)
    ///   Offset 64: float pitchZ          (right dir Z)
    ///   Offset 68: float suspRL
    ///   ...
    ///   Offset 136: float gForceLateral
    ///   Offset 140: float gForceLongitudinal
    ///   ...
    /// </summary>
    public class DirtRallyPlugin : IGamePlugin
    {
        public string GameName  => "Dirt Rally";
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
            _thread = new Thread(Loop) { IsBackground = true, Name = "DirtUDP" };
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
            // Minimum for the Codemasters packet
            if (d == null || d.Length < 64) return null;

            float time  = BitConverter.ToSingle(d, 0);
            if (time <= 0) return null;  // not in stage

            float speed = BitConverter.ToSingle(d, 28);  // m/s
            float speedKmh = Math.Abs(speed) * 3.6f;

            // Velocity components for surge/sway
            float velX = BitConverter.ToSingle(d, 32);
            float velY = BitConverter.ToSingle(d, 36);
            float velZ = BitConverter.ToSingle(d, 40);

            // Forward direction vector (roll orientation)
            float fwdX = BitConverter.ToSingle(d, 44);
            float fwdY = BitConverter.ToSingle(d, 48);
            float fwdZ = BitConverter.ToSingle(d, 52);

            // Right direction vector (pitch orientation)
            float rightX = BitConverter.ToSingle(d, 56);
            float rightY = BitConverter.ToSingle(d, 60);
            float rightZ = BitConverter.ToSingle(d, 64);

            // Derive pitch and roll from direction vectors
            // fwdY = vertical component of forward direction → pitch
            // rightY = vertical component of right direction → roll
            float pitchRad = (float)Math.Asin(Math.Max(-1, Math.Min(1, fwdY)));
            float rollRad  = (float)Math.Asin(Math.Max(-1, Math.Min(1, rightY)));

            float pitchDeg = (float)(pitchRad * 180.0 / Math.PI);
            float rollDeg  = (float)(rollRad  * 180.0 / Math.PI);

            // G-forces (if packet is large enough to contain them)
            float gLat = 0, gLong = 0;
            if (d.Length >= 144)
            {
                gLat  = BitConverter.ToSingle(d, 136);
                gLong = BitConverter.ToSingle(d, 140);
            }

            // Speed deadzone: no motion below 5 km/h
            const float DEADZONE = 5f;
            const float FADE_END = 15f;
            if (speedKmh < DEADZONE)
            {
                return new TelemetryData
                {
                    Pitch = 0, Roll = 0, Yaw = 0,
                    Speed = speedKmh, IsValid = true
                };
            }

            float speedFactor = speedKmh >= FADE_END ? 1f
                : (speedKmh - DEADZONE) / (FADE_END - DEADZONE);

            // Rally cars: aggressive pitch & roll from terrain + G-forces
            float seatPitch = (-pitchDeg * 1.8f + gLong * 2f) * speedFactor;
            float seatRoll  = (rollDeg * 2.5f + gLat * 3f) * speedFactor;
            float seatYaw   = gLat * 6f * speedFactor;

            return new TelemetryData
            {
                Pitch   = seatPitch,
                Roll    = seatRoll,
                Yaw     = seatYaw,
                Surge   = gLong,
                Sway    = gLat,
                Speed   = speedKmh,
                IsValid = true
            };
        }

        public void Dispose() => Stop();
    }
}
