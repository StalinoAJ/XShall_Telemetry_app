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
    /// API: GET http://localhost:25555/api/ets2/telemetry → JSON
    ///
    /// Key JSON structure (nested):
    ///   truck.speed             → km/h
    ///   truck.gameSteer         → -1..1
    ///   truck.acceleration.x    → lateral G
    ///   truck.acceleration.z    → longitudinal G
    ///   truck.placement.pitch   → radians
    ///   truck.placement.roll    → radians
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

        // ── JSON helpers for the Funbit nested structure ────────────────

        /// <summary>Find a flat key like "speed": anywhere in json.</summary>
        private float JsonFloat(string json, string key, float def = 0)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return def;
            idx += search.Length;
            return ParseNumberAt(json, idx, def);
        }

        /// <summary>Find a nested key: first locate parent object, then child key inside it.</summary>
        private float JsonNested(string json, string parent, string child, float def = 0)
        {
            // Find "parent":{
            string search = "\"" + parent + "\"";
            int pIdx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (pIdx < 0) return def;

            // Find the opening brace after parent key
            int braceIdx = json.IndexOf('{', pIdx + search.Length);
            if (braceIdx < 0) return def;

            // Find the closing brace to limit search scope
            int closeBrace = json.IndexOf('}', braceIdx);
            if (closeBrace < 0) closeBrace = json.Length;

            // Now find "child": within that scope
            string childSearch = "\"" + child + "\":";
            int cIdx = json.IndexOf(childSearch, braceIdx, closeBrace - braceIdx, StringComparison.OrdinalIgnoreCase);
            if (cIdx < 0) return def;

            return ParseNumberAt(json, cIdx + childSearch.Length, def);
        }

        private float ParseNumberAt(string json, int idx, float def)
        {
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '.' || json[end] == 'e' || json[end] == 'E' || json[end] == '+')) end++;
            if (end == idx) return def;
            return float.TryParse(json.Substring(idx, end - idx),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float v) ? v : def;
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

            // ── Flat keys from truck object ─────────────────────────────
            float speed    = JsonFloat(json, "speed");         // km/h
            float steer    = JsonFloat(json, "gameSteer");     // -1 to 1

            // ── Nested: truck.acceleration.{x,z} ───────────────────────
            float accelX   = JsonNested(json, "acceleration", "x");  // lateral G
            float accelZ   = JsonNested(json, "acceleration", "z");  // longitudinal G

            // ── Nested: truck.placement.{pitch, roll} ──────────────────
            float placePitch = JsonNested(json, "placement", "pitch");  // radians
            float placeRoll  = JsonNested(json, "placement", "roll");   // radians

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

            // Combine acceleration + placement for better feel
            // Funbit acceleration.x is very small (~0.02 normal, ~0.5 hard corner)
            // placement.roll is in radians (tiny, ~0.001)
            // gameSteer is -1 to 1 (full lock)
            // → Need aggressive multipliers to get real seat movement
            float rollDeg  = (float)(placeRoll * 180.0 / Math.PI);
            float pitchDeg = (float)(placePitch * 180.0 / Math.PI);

            return new TelemetryData
            {
                Pitch  = -(speedDelta * 3f + pitchDeg * 2f),     // brake/accel + road slope
                Roll   = -(accelX * 35f + rollDeg * 10f),         // lateral G + body lean (boosted)
                Yaw    = -steer * 18f,                             // steering wheel (boosted)
                Surge  = accelZ,
                Sway   = accelX,
                Speed  = absSpeed,
                IsValid = true
            };
        }

        public void Dispose() => Stop();
    }
}
