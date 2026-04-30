using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using SHALLControl.Models;

namespace SHALLControl.Plugins
{
    /// <summary>
    /// Forza Horizon 5 / Forza Motorsport UDP telemetry plugin.
    /// Configure in-game: Settings → HUD & Gameplay → Data Out = ON
    ///   IP: 127.0.0.1  Port: 5300
    /// The "Sled" packet is 232 bytes (little-endian floats).
    /// </summary>
    public class ForzaPlugin : IGamePlugin
    {
        public string GameName  => "Forza Horizon 5";
        public string Protocol  => "UDP :5300";
        public bool   IsRunning => _running;

        public event EventHandler<TelemetryData> TelemetryReceived;

        private const int PORT = 5300;
        private Thread    _thread;
        private UdpClient _udp;
        private volatile bool _running;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _udp = new UdpClient(PORT);
            _udp.Client.ReceiveTimeout = 1000;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ForzaUDP" };
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
            // Minimum packet size: 232 bytes (Sled format)
            if (d == null || d.Length < 232) return null;

            int isRaceOn = BitConverter.ToInt32(d, 0);
            if (isRaceOn == 0) return null;  // not in race

            float accelX = BitConverter.ToSingle(d, 20);  // lateral g   (sway)
            float accelY = BitConverter.ToSingle(d, 24);  // vertical g  (heave)
            float accelZ = BitConverter.ToSingle(d, 28);  // forward g   (surge)

            float angVelY = BitConverter.ToSingle(d, 48); // yaw rate

            // Speed m/s → km/h
            float velX = BitConverter.ToSingle(d, 32);
            float velY = BitConverter.ToSingle(d, 36);
            float velZ = BitConverter.ToSingle(d, 40);
            float speed = (float)Math.Sqrt(velX*velX + velY*velY + velZ*velZ) * 3.6f;

            // ── Speed deadzone: no motion below 5 km/h ──────────────────
            // Smooth fade-in from 5-15 km/h to avoid abrupt start
            const float DEADZONE = 5f;
            const float FADE_END = 15f;
            if (speed < DEADZONE)
            {
                return new TelemetryData
                {
                    Pitch = 0, Roll = 0, Yaw = 0,
                    Speed = speed, IsValid = true
                };
            }

            float speedFactor = speed >= FADE_END ? 1f
                : (speed - DEADZONE) / (FADE_END - DEADZONE);

            // ── Use acceleration (G-forces) instead of raw orientation ──
            // Pitch negated (braking = tilt forward), reduced multipliers for smoother feel
            float seatPitch = -accelZ * 1.5f * speedFactor;  // braking/accel → pitch (inverted)
            float seatRoll  =  accelX * 2f   * speedFactor;  // cornering → roll (gentler)
            float seatYaw   =  angVelY * 5f  * speedFactor;  // steering → yaw (gentler)

            return new TelemetryData
            {
                Pitch  = seatPitch,
                Roll   = seatRoll,
                Yaw    = seatYaw,
                Surge  = accelZ,
                Sway   = accelX,
                Heave  = accelY,
                Speed  = speed,
                IsValid = true
            };
        }

        public void Dispose() => Stop();
    }
}
