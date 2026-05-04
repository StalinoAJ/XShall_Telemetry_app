using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using SHALLControl.Models;

namespace SHALLControl.Plugins
{
    /// <summary>
    /// American Truck Simulator plugin.
    /// Uses the same "ETS2 Telemetry Server" (by Funbit) — it supports both
    /// ETS2 and ATS on the same REST endpoint:
    ///   GET http://localhost:25555/api/ets2/telemetry → JSON
    ///
    /// Install: place ets2-telemetry-server.dll into
    ///   <ATS install>/bin/win_x64/plugins/
    /// Run Ets2Telemetry.exe BEFORE launching ATS.
    /// </summary>
    public class ATSPlugin : IGamePlugin
    {
        public string GameName  => "American Truck Sim";
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
            _running   = true;
            _lastSpeed = 0;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ATSHTTP" };
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

        // ── JSON helpers (same as ETS2 — Funbit format) ────────────────

        private float JsonFloat(string json, string key, float def = 0)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return def;
            idx += search.Length;
            return ParseNumberAt(json, idx, def);
        }

        private float JsonNested(string json, string parent, string child, float def = 0)
        {
            string search = "\"" + parent + "\"";
            int pIdx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (pIdx < 0) return def;
            int braceIdx = json.IndexOf('{', pIdx + search.Length);
            if (braceIdx < 0) return def;
            int closeBrace = json.IndexOf('}', braceIdx);
            if (closeBrace < 0) closeBrace = json.Length;
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

            float speed    = JsonFloat(json, "speed");
            float steer    = JsonFloat(json, "gameSteer");
            float accelX   = JsonNested(json, "acceleration", "x");
            float accelZ   = JsonNested(json, "acceleration", "z");
            float placePitch = JsonNested(json, "placement", "pitch");
            float placeRoll  = JsonNested(json, "placement", "roll");

            float speedDelta = speed - _lastSpeed;
            _lastSpeed = speed;

            float absSpeed = Math.Abs(speed);
            if (absSpeed < 3f)
            {
                return new TelemetryData
                {
                    Pitch = 0, Roll = 0, Yaw = 0,
                    Speed = absSpeed, IsValid = true
                };
            }

            float rollDeg  = (float)(placeRoll * 180.0 / Math.PI);
            float pitchDeg = (float)(placePitch * 180.0 / Math.PI);

            return new TelemetryData
            {
                Pitch  = -(speedDelta * 3f + pitchDeg * 2f),
                Roll   = -(accelX * 35f + rollDeg * 10f),
                Yaw    = -steer * 18f,
                Surge  = accelZ,
                Sway   = accelX,
                Speed  = absSpeed,
                IsValid = true
            };
        }

        public void Dispose() => Stop();
    }
}
