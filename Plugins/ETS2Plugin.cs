using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SHALLControl.Models;

namespace SHALLControl.Plugins
{
    /// <summary>
    /// Euro Truck Simulator 2 plugin.
    /// Requires "ETS2 Telemetry Server" (by Funbit) running at localhost:25555.
    /// Download: https://github.com/Funbit/ets2-telemetry-server/releases
    /// It auto-installs the SCS SDK plugin into ETS2 and exposes:
    ///   GET http://localhost:25555/api/ets2/telemetry  → JSON
    /// </summary>
    public class ETS2Plugin : IGamePlugin
    {
        public string GameName  => "Euro Truck Simulator 2";
        public string Protocol  => "HTTP :25555";
        public bool   IsRunning => _running;

        public event EventHandler<TelemetryData> TelemetryReceived;

        private const string URL = "http://localhost:25555/api/ets2/telemetry";
        private const int POLL_MS = 80;   // ~12 Hz

        private Thread _thread;
        private volatile bool _running;
        private float _lastSpeed;

        public void Start()
        {
            if (_running) return;
            _running  = true;
            _lastSpeed = 0;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ETS2HTTP" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
        }

        private void Loop()
        {
            while (_running)
            {
                try
                {
                    string json = Get(URL);
                    if (json != null)
                    {
                        var t = Parse(json);
                        if (t != null) TelemetryReceived?.Invoke(this, t);
                    }
                }
                catch { /* server not running yet */ }

                Thread.Sleep(POLL_MS);
            }
        }

        private string Get(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 500;
            req.Method  = "GET";
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        // Minimal JSON key extraction (no external libraries)
        private float JsonFloat(string json, string key, float def = 0)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return def;
            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '.' || json[end] == 'e' || json[end] == 'E' || json[end] == '+')) end++;
            if (end == idx) return def;
            return float.TryParse(json.Substring(idx, end - idx), out float v) ? v : def;
        }

        private bool JsonBool(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            return json.IndexOf("true", idx + search.Length, 10, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private TelemetryData Parse(string json)
        {
            bool connected = JsonBool(json, "connected");
            if (!connected) return null;

            float speed    = JsonFloat(json, "speed");        // km/h
            float accelX   = JsonFloat(json, "accelerationX"); // lateral
            float accelZ   = JsonFloat(json, "accelerationZ"); // longitudinal
            float steer    = JsonFloat(json, "gameSteer");    // -1 to 1

            float speedDelta = speed - _lastSpeed;
            _lastSpeed = speed;

            // Speed deadzone — no motion below 3 km/h
            float absSpeed = Math.Abs(speed);
            if (absSpeed < 3f)
            {
                return new TelemetryData
                {
                    Pitch = 0, Roll = 0, Yaw = 0,
                    Speed = absSpeed, IsValid = true
                };
            }

            // Axes NEGATED to correct inversion
            return new TelemetryData
            {
                Pitch  = -speedDelta * 3f,          // braking = tilt forward (not back)
                Roll   = -accelX * 8f,              // left turn = lean left (not right)
                Yaw    = -steer * 5f,               // steer left = rotate left
                Surge  = accelZ,
                Sway   = accelX,
                Speed  = absSpeed,
                IsValid = true
            };
        }

        public void Dispose() => Stop();
    }
}
