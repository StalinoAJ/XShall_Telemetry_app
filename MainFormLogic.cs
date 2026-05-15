using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;
using SHALLControl.Models;
using SHALLControl.Plugins;

namespace SHALLControl
{
    public partial class MainForm
    {
        // ================================================================
        //  HOW TO USE PANEL
        // ================================================================
        private void BuildHelpPanel()
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
            int y = 20;
            scroll.Controls.Add(MakeLabel("How To Use", 28, y, C_TEXT, 20f, FontStyle.Bold)); y += 42;
            scroll.Controls.Add(MakeLabel("SHALL XR Seat Controller — Setup Guide", 28, y, C_TEXT2, 9.5f, FontStyle.Regular)); y += 40;

            y = AddHelpCard(scroll, y, "1", "Connect Your Seat",
                "• Ensure PC and SHALL XR seat are on same LAN\n" +
                "• Default seat IP: 192.168.1.40\n" +
                "• Enter the IP in the Seat IP field on Home tab\n" +
                "• Click  ⚡ TEST CONNECTION  to verify");

            y = AddHelpCard(scroll, y, "2", "Configure Game Telemetry",
                "Each game needs telemetry output enabled:\n\n" +
                "🏎  Forza Horizon 5\n" +
                "   Settings → HUD & Gameplay → Data Out = ON\n" +
                "   IP: 127.0.0.1   Port: 5300\n\n" +
                "🚛  Euro Truck Simulator 2\n" +
                "   Install ETS2 Telemetry Server (by Funbit)\n" +
                "   Run Ets2Telemetry.exe BEFORE launching ETS2\n\n" +
                "🏁  F1 Series (2018 — 2024)\n" +
                "   Settings → Telemetry → UDP = ON\n" +
                "   IP: 127.0.0.1   Port: 20777\n\n" +
                "🚚  American Truck Simulator\n" +
                "   Uses same Funbit Telemetry Server as ETS2\n" +
                "   Install plugin DLL into ATS bin/win_x64/plugins/\n" +
                "   Run Ets2Telemetry.exe BEFORE launching ATS\n\n" +
                "❄  SnowRunner\n" +
                "   No native telemetry — requires community bridge tool\n" +
                "   Bridge sends UDP to port 21777\n" +
                "   Check xsimulator.net for available tools\n\n" +
                "🏔  Dirt Rally / Dirt Rally 2.0\n" +
                "   Edit hardware_settings_config.xml:\n" +
                "   Documents\\My Games\\DiRT Rally 2.0\\hardwaresettings\\\n" +
                "   <udp enabled=\"true\" extradata=\"3\"\n" +
                "        ip=\"127.0.0.1\" port=\"20777\" delay=\"1\" />");

            y = AddHelpCard(scroll, y, "3", "Select & Start",
                "• Click a game card on the Home tab\n" +
                "• Adjust Pitch / Roll / Yaw intensity sliders\n" +
                "• Set Max Angle safety limit (default: 15°)\n" +
                "• Click  ▶ START  to begin motion session\n" +
                "• Launch the game and drive!");

            y = AddHelpCard(scroll, y, "4", "Custom Game Path & Image",
                "• Select a game card first\n" +
                "• Click  📁 SET GAME PATH  to set the game .exe location\n" +
                "• Click  🖼 SET GAME IMAGE  to set a custom card icon\n" +
                "  Supported formats: PNG, JPG, BMP, ICO\n" +
                "• The image appears in the top-right of the game card");

            y = AddHelpCard(scroll, y, "5", "Tune the Feel",
                "• Pitch = acceleration / braking tilt\n" +
                "• Roll = cornering lean (left/right)\n" +
                "• Yaw = steering rotation feel\n" +
                "• Start low (0.5×) and increase gradually\n" +
                "• Max Angle is a hard safety clamp (≤ 15° recommended)");

            y = AddHelpCard(scroll, y, "6", "Live Telemetry",
                "• The incoming telemetry data panel shows real-time values\n" +
                "  being received from the game plugin\n" +
                "• Pitch/Roll/Yaw = seat angles in degrees\n" +
                "• Surge/Sway/Heave = G-force components\n" +
                "• Use this to verify your game is sending data correctly");

            y = AddHelpCard(scroll, y, "7", "Troubleshooting",
                "Seat doesn't respond?\n" +
                "  → ping 192.168.1.40 in PowerShell\n" +
                "  → Open http://192.168.1.40 in browser\n\n" +
                "No telemetry?\n" +
                "  → Verify Data Out is ON in game settings\n" +
                "  → For ETS2/ATS: ensure Telemetry Server is running\n" +
                "  → For Dirt Rally: verify XML config is saved\n\n" +
                "Jerky motion?\n" +
                "  → Lower intensity sliders\n\n" +
                "SnowRunner no data?\n" +
                "  → Install a community telemetry bridge tool\n" +
                "  → Check port 21777 is not blocked by firewall");

            y += 20;
            scroll.Controls.Add(MakeLabel("SHALL XR Seat Controller v1.1", 28, y, C_TEXT2, 8f, FontStyle.Regular));
            _helpPanel.Controls.Add(scroll);
        }

        private int AddHelpCard(Panel parent, int y, string step, string title, string body)
        {
            int bodyH = (int)(body.Split('\n').Length * 17f) + 16;
            int cardH = 48 + bodyH;

            var card = new Panel
            {
                Location = new Point(28, y), Size = new Size(750, cardH),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using (var path = RoundRect(rect, 16))
                {
                    using (var br = new LinearGradientBrush(rect, C_CARD, C_BG2, 135f))
                        g.FillPath(br, path);
                    using (var br = new SolidBrush(Color.FromArgb(10, 255, 255, 255)))
                        g.FillPath(br, path);
                    using (var pen = new Pen(C_BORDER, 1))
                        g.DrawPath(pen, path);
                }
                // Step circle
                var circleRect = new Rectangle(16, 12, 30, 30);
                using (var br = new LinearGradientBrush(circleRect, C_ACCENT, Color.FromArgb(180, 50, 60), 135f))
                    g.FillEllipse(br, circleRect);
                using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
                using (var br = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(step, f, br, circleRect, sf);
                }
                using (var f = new Font("Segoe UI", 11.5f, FontStyle.Bold))
                using (var br = new SolidBrush(C_TEXT))
                    g.DrawString(title, f, br, 56, 16);
                using (var f = new Font("Segoe UI", 9f))
                using (var br = new SolidBrush(C_TEXT2))
                    g.DrawString(body, f, br, new RectangleF(56, 46, card.Width - 76, bodyH));
            };
            parent.Controls.Add(card);
            return y + cardH + 14;
        }

        // ================================================================
        //  GAME LOGIC
        // ================================================================
        private string TryAutoDetectPath(int idx)
        {
            string[] subPaths = null;
            if (idx == 0) subPaths = new[] { @"ForzaHorizon5\ForzaHorizon5.exe" };
            else if (idx == 1) subPaths = new[] { @"Euro Truck Simulator 2\bin\win_x64\eurotrucks2.exe" };
            else if (idx == 2) subPaths = new[] { @"F1 23\F1_23.exe", @"F1 22\F1_22.exe", @"F1 24\F1_24.exe" };
            else if (idx == 3) subPaths = new[] { @"American Truck Simulator\bin\win_x64\amtrucks.exe" };
            else if (idx == 4) subPaths = new[] { @"SnowRunner\en_us\Sources\Bin\SnowRunner.exe" };
            else if (idx == 5) subPaths = new[] { @"DiRT Rally 2.0\dirtrally2.exe", @"DiRT Rally\drt.exe" };
            if (subPaths == null) return null;

            var libraries = new System.Collections.Generic.List<string> { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam", @"C:\XboxGames", @"D:\XboxGames", @"E:\XboxGames" };
            try {
                using(var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) {
                    if (key != null) {
                        string steamPath = key.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(steamPath)) {
                            steamPath = steamPath.Replace("/", "\\");
                            libraries.Add(steamPath);
                            string vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                            if (File.Exists(vdf)) {
                                foreach(var line in File.ReadAllLines(vdf)) {
                                    if (line.Contains("\"path\"")) {
                                        var parts = line.Split('"');
                                        if (parts.Length >= 4) {
                                            string libPath = parts[3].Replace("\\\\", "\\");
                                            libraries.Add(libPath);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            } catch { }

            foreach(var lib in libraries) {
                string common = Path.Combine(lib, "steamapps", "common");
                if (!Directory.Exists(common)) common = lib;

                if (!Directory.Exists(common)) continue;
                foreach(var sub in subPaths) {
                    string full = Path.Combine(common, sub);
                    if (File.Exists(full)) return full;
                }
            }
            return null;
        }

        private void SelectGame(int idx)
        {
            if (_active) return;
            _selGame = idx;
            
            if (string.IsNullOrEmpty(_customGamePaths[idx])) {
                string autoPath = TryAutoDetectPath(idx);
                if (!string.IsNullOrEmpty(autoPath)) {
                    _customGamePaths[idx] = autoPath;
                    Log("Auto-detected path: " + autoPath);
                } else {
                    Log("Selected: " + NAMES[idx] + " (Path not found, please set manually)");
                }
            } else {
                Log("Selected: " + NAMES[idx] + "  (" + PROTOS[idx] + ")");
            }

            for (int i = 0; i < GAME_COUNT; i++) _cards[i].Invalidate();
            _btnStart.Enabled = true;
            _heroPanel?.Invalidate();
        }

        private async System.Threading.Tasks.Task ConnectAsync()
        {
            _btnConnect.Enabled = false;
            _btnConnect.Text = "Testing…";
            Log("Testing seat connection…");
            bool ok = await _seat.TestConnectionAsync();
            SetSeatStatus(ok);
            Log(ok ? "✓ Seat responded at " + _seat.SeatIp : "✗ No response from " + _seat.SeatIp);
            _btnConnect.Enabled = true;
            _btnConnect.Text = "⚡  TEST CONNECTION";
        }

        private void ToggleActive(object sender, EventArgs e)
        {
            if (!_active) StartSession(); else StopSession();
        }

        private void StartSession()
        {
            if (_selGame < 0) { Log("Select a game first."); return; }
            _cfg = new GameConfig
            {
                Name = NAMES[_selGame],
                PitchScale = _trPitch.Value / 10f,
                RollScale = _trRoll.Value / 10f,
                YawScale = _trYaw.Value / 10f,
                MaxAngle = (int)_numMax.Value,
                ExePath = _customGamePaths[_selGame]
            };
            switch (_selGame)
            {
                case 0: _plugin = new ForzaPlugin(); break;
                case 1: _plugin = new ETS2Plugin(); break;
                case 2: _plugin = new F1Plugin(); break;
                case 3: _plugin = new ATSPlugin(); break;
                case 4: _plugin = new SnowRunnerPlugin(); break;
                case 5: _plugin = new DirtRallyPlugin(); break;
                default: _plugin = new ForzaPlugin(); break;
            }
            _plugin.TelemetryReceived += OnTelemetry;
            _plugin.Start();
            _active = true;
            _btnStart.Text = "■  STOP";
            _btnStart.BackColor = C_RED;
            _btnStart.ForeColor = Color.White;
            _btnConnect.Enabled = false;
            foreach (var c in _cards) c.Enabled = false;
            Log("▶ Started — " + _cfg.Name);
        }

        private void StopSession()
        {
            _active = false;
            _plugin?.Stop(); _plugin?.Dispose(); _plugin = null;
            _ = _seat.CenterAsync();
            _btnStart.Text = "▶  START";
            _btnStart.BackColor = C_GREEN;
            _btnStart.ForeColor = Color.FromArgb(8, 24, 10);
            _btnConnect.Enabled = true;
            foreach (var c in _cards) c.Enabled = true;
            _latest = TelemetryData.Zero;
            _heroPanel?.Invalidate();
            Log("■ Stopped — seat centered.");
        }

        private int _pktCount;
        private DateTime _lastPktLog = DateTime.MinValue;

        private void OnTelemetry(object sender, TelemetryData t)
        {
            _latest = t;
            _pktCount++;

            // Log first packet and then every 5 seconds for diagnostics
            if (_pktCount == 1)
            {
                BeginInvoke((Action)(() => Log("📡 First telemetry packet received! Data is flowing.")));
            }
            else if ((DateTime.UtcNow - _lastPktLog).TotalSeconds >= 5)
            {
                _lastPktLog = DateTime.UtcNow;
                int p = _seat.LastPitch, r = _seat.LastRoll, y = _seat.LastYaw;
                BeginInvoke((Action)(() => Log($"📡 Pkts: {_pktCount}  Seat → P:{p} R:{r} Y:{y}  Connected:{_seat.IsConnected}")));
            }

            _seat.Send(t, _cfg);
        }

        private void RefreshUI()
        {
            if (_latest == null || _currentTab != 0) return;
            _pbGauges.Invalidate();
            _lblSpeed.Text = _active ? _latest.Speed.ToString("0.0") + " km/h" : "— km/h";
            _lblPitchVal.Text = (_trPitch.Value / 10f).ToString("0.0") + "×";
            _lblRollVal.Text  = (_trRoll.Value  / 10f).ToString("0.0") + "×";
            _lblYawVal.Text   = (_trYaw.Value   / 10f).ToString("0.0") + "×";
            _telemetryPanel?.Invalidate();
        }
    }
}
