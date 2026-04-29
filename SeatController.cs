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
        private readonly ButterworthFilter _pitchFilter;
        private readonly ButterworthFilter _rollFilter;
        private readonly ButterworthFilter _yawFilter;
        private DateTime _lastSend = DateTime.MinValue;
        private const int MIN_INTERVAL_MS = 50;  // max 20 Hz to seat

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
            _pitchFilter = new ButterworthFilter(5.0, 20.0);
            _rollFilter  = new ButterworthFilter(5.0, 20.0);
            _yawFilter   = new ButterworthFilter(5.0, 20.0);
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

            // Apply per-game scaling then filter
            double rawPitch = t.Pitch * cfg.PitchScale;
            double rawRoll  = t.Roll  * cfg.RollScale;
            double rawYaw   = t.Yaw   * cfg.YawScale;

            int pitch = Clamp(_pitchFilter.Filter(rawPitch), cfg.MaxAngle);
            int roll  = Clamp(_rollFilter .Filter(rawRoll),  cfg.MaxAngle);
            int yaw   = Clamp(_yawFilter  .Filter(rawYaw),   cfg.MaxAngle);

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
            _pitchFilter.Reset(); _rollFilter.Reset(); _yawFilter.Reset();
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
