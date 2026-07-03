using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SHALLControl.Models;

namespace SHALLControl
{
    /// <summary>
    /// Sends position commands to the SHALL XR motion seat via its built-in HTTP CGI API.
    /// Endpoint: GET http://192.168.1.40/pos.cgi?pitch=X&roll=Y&yaw=Z&button2=send
    /// </summary>
    public class SeatController : IDisposable
    {
        private string _ip;
        private readonly HttpClient _http;
        private readonly WashoutFilter _pitchWashout;
        private readonly WashoutFilter _rollWashout;
        private readonly WashoutFilter _yawWashout;
        private DateTime _lastSend = DateTime.MinValue;
        private const int MIN_INTERVAL_MS = 50;  // max 20 Hz to seat

        // Super Smooth EMA State
        private double _emaPitch, _emaRoll, _emaYaw;
        private bool _firstEma = true;

        public bool IsConnected { get; private set; }
        public string SeatIp => _ip;

        // Last values sent (for UI display)
        public int LastPitch { get; private set; }
        public int LastRoll  { get; private set; }
        public int LastYaw   { get; private set; }

        public event EventHandler<bool> ConnectionChanged;

        public SeatController(string ip = "192.168.1.40")
        {
            _ip = ip;
            _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(400) };
            // Low-pass for sustained G-force tilt (2Hz), High-pass for fast bumps (0.5Hz cutoff)
            _pitchWashout = new WashoutFilter(2.0, 0.5, 20.0);
            _rollWashout  = new WashoutFilter(2.0, 0.5, 20.0);
            _yawWashout   = new WashoutFilter(2.0, 0.5, 20.0);
        }

        public void SetIp(string ip) { _ip = ip; }

        /// <summary>Ping the seat to check connectivity.</summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                string url = $"http://{_ip}/pos.cgi?pitch=0&roll=0&yaw=0&button2=send";
                var resp = await _http.GetAsync(url);
                IsConnected = resp.IsSuccessStatusCode;
            }
            catch { IsConnected = false; }
            ConnectionChanged?.Invoke(this, IsConnected);
            return IsConnected;
        }

        /// <summary>
        /// Send filtered, scaled telemetry to the seat.
        /// Call this from the plugin TelemetryReceived event.
        /// </summary>
        public void Send(TelemetryData t, GameConfig cfg)
        {
            if (t == null || !t.IsValid) return;

            // Rate-limit to avoid overwhelming the seat
            if ((DateTime.UtcNow - _lastSend).TotalMilliseconds < MIN_INTERVAL_MS) return;
            _lastSend = DateTime.UtcNow;

            // 1. Sustained Forces (Tilt Coordination)
            // Use Surge (Longitudinal G) for Pitch and Sway (Lateral G) for Roll.
            // Scale factors assume 1G = ~15 degrees tilt (standard simulation practice).
            double pitchSustained = -t.Surge * 15.0 * cfg.PitchScale; 
            double rollSustained  =  t.Sway  * 15.0 * cfg.RollScale;

            // 2. Transient Forces (Direct Orientation / Bumps)
            // Use the Pitch/Roll/Yaw provided by the plugin as the source for transients.
            double pitchTransient = t.Pitch * cfg.PitchScale;
            double rollTransient  = t.Roll  * cfg.RollScale;
            double yawTransient   = t.Yaw   * cfg.YawScale;

            // Anti-windup: clamp inputs before the filter so internal state
            // never accumulates beyond the hardware limit, preventing oscillation
            // at ±MaxAngle when the chair locks to its extreme position.
            double maxD = cfg.MaxAngle;
            pitchSustained = Math.Max(-maxD, Math.Min(maxD, pitchSustained));
            rollSustained  = Math.Max(-maxD, Math.Min(maxD, rollSustained));
            pitchTransient = Math.Max(-maxD, Math.Min(maxD, pitchTransient));
            rollTransient  = Math.Max(-maxD, Math.Min(maxD, rollTransient));
            yawTransient   = Math.Max(-maxD, Math.Min(maxD, yawTransient));

            // Apply Washout Filtering
            int pitch = Clamp(_pitchWashout.Filter(pitchSustained, pitchTransient), cfg.MaxAngle);
            int roll  = Clamp(_rollWashout .Filter(rollSustained,  rollTransient),  cfg.MaxAngle);
            int yaw   = Clamp(_yawWashout  .Filter(0,              yawTransient),   cfg.MaxAngle);

            if (cfg.SuperSmooth)
            {
                if (_firstEma) { _emaPitch = pitch; _emaRoll = roll; _emaYaw = yaw; _firstEma = false; }
                
                // 15% adjustment per frame = very heavy dampening for graceful movement
                _emaPitch += (pitch - _emaPitch) * 0.15;
                _emaRoll  += (roll  - _emaRoll)  * 0.15;
                _emaYaw   += (yaw   - _emaYaw)   * 0.15;
                
                pitch = Clamp(_emaPitch, cfg.MaxAngle);
                roll  = Clamp(_emaRoll,  cfg.MaxAngle);
                yaw   = Clamp(_emaYaw,   cfg.MaxAngle);
            }
            else
            {
                _firstEma = true;
            }

            LastPitch = pitch; LastRoll = roll; LastYaw = yaw;

            // Fire-and-forget (don't block the telemetry thread)
            string url = $"http://{_ip}/pos.cgi?pitch={pitch}&roll={roll}&yaw={yaw}&button2=send";
            _ = _http.GetAsync(url).ContinueWith(task =>
            {
                bool ok = !task.IsFaulted && task.Result?.IsSuccessStatusCode == true;
                if (IsConnected != ok) { IsConnected = ok; ConnectionChanged?.Invoke(this, ok); }
            });
        }

        /// <summary>Return seat to neutral center position.</summary>
        public async Task CenterAsync()
        {
            _pitchWashout.Reset(); _rollWashout.Reset(); _yawWashout.Reset();
            _firstEma = true;
            LastPitch = 0; LastRoll = 0; LastYaw = 0;
            try
            {
                await _http.GetAsync($"http://{_ip}/pos.cgi?pitch=0&roll=0&yaw=0&button2=send");
            }
            catch { }
        }

        private static int Clamp(double value, int maxAngle)
            => (int)Math.Max(-maxAngle, Math.Min(maxAngle, value));

        public void Dispose()
        {
            _ = CenterAsync();
            _http?.Dispose();
        }
    }
}
