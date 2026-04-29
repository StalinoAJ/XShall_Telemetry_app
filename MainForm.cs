using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading.Tasks;
using SHALLControl.Models;
using SHALLControl.Plugins;

namespace SHALLControl
{
    public partial class MainForm : Form
    {
        // ── Wine/Burgundy palette ───────────────────────────────────────
        static readonly Color C_BG      = Color.FromArgb(42, 20, 28);
        static readonly Color C_BG2     = Color.FromArgb(56, 28, 38);
        static readonly Color C_CARD    = Color.FromArgb(72, 34, 48);
        static readonly Color C_CARD_HI = Color.FromArgb(95, 48, 62);
        static readonly Color C_ACCENT  = Color.FromArgb(230, 80, 90);
        static readonly Color C_ACCENT2 = Color.FromArgb(255, 120, 100);
        static readonly Color C_GREEN   = Color.FromArgb(80, 220, 160);
        static readonly Color C_RED     = Color.FromArgb(240, 70, 80);
        static readonly Color C_TEXT    = Color.FromArgb(245, 235, 230);
        static readonly Color C_TEXT2   = Color.FromArgb(180, 140, 145);
        static readonly Color C_BORDER  = Color.FromArgb(100, 60, 70);

        // ── State ───────────────────────────────────────────────────────
        private IGamePlugin _plugin;
        private SeatController _seat;
        private GameConfig _cfg;
        private int _selGame = -1;
        private bool _active;
        private TelemetryData _latest = TelemetryData.Zero;
        private int _currentTab = 0;

        // ── Controls ────────────────────────────────────────────────────
        private Panel[] _cards = new Panel[3];
        private Label[] _cardLbl = new Label[3];
        private Panel _statusDot;
        private Label _statusLbl, _lblSpeed, _lblLog;
        private TextBox _txtIp;
        private TrackBar _trPitch, _trRoll, _trYaw;
        private Label _lblPitchVal, _lblRollVal, _lblYawVal;
        private NumericUpDown _numMax;
        private Button _btnConnect, _btnStart;
        private PictureBox _pbGauges, _pbSeat;
        private Panel _homePanel, _helpPanel;
        private Button _tabHome, _tabHelp;
        private System.Windows.Forms.Timer _uiTimer;

        static readonly string[] NAMES  = { "Forza Horizon 5", "Euro Truck Sim 2", "F1 Series" };
        static readonly string[] PROTOS = { "UDP  :5300", "HTTP :25555", "UDP  :20777" };
        static readonly string[] ICONS  = { "🏎", "🚛", "🏁" };
        static readonly Color[] GCOLORS = {
            Color.FromArgb(0, 120, 215),
            Color.FromArgb(255, 160, 50),
            Color.FromArgb(220, 40, 40)
        };

        public MainForm()
        {
            Text = "SHALL XR — Seat Controller";
            Size = new Size(1200, 780);
            MinimumSize = new Size(1050, 700);
            BackColor = C_BG;
            ForeColor = C_TEXT;
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;

            _seat = new SeatController("192.168.1.40");
            _seat.ConnectionChanged += (s, ok) =>
                BeginInvoke((Action)(() => SetSeatStatus(ok)));

            BuildContent();
            BuildSidebar();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 60 };
            _uiTimer.Tick += (s, e) => RefreshUI();
            _uiTimer.Start();

            FormClosing += async (s, e) =>
            {
                _active = false;
                _plugin?.Stop();
                await _seat.CenterAsync();
                _seat.Dispose();
            };
        }

        // ================================================================
        //  SIDEBAR
        // ================================================================
        private void BuildSidebar()
        {
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 72, BackColor = Color.FromArgb(32, 14, 20) };
            sidebar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Glass gradient overlay
                using (var br = new LinearGradientBrush(sidebar.ClientRectangle,
                    Color.FromArgb(20, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    g.FillRectangle(br, sidebar.ClientRectangle);
                using (var pen = new Pen(C_BORDER, 1))
                    g.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
                // Logo circle
                var logoRect = new Rectangle(16, 16, 40, 40);
                using (var br = new LinearGradientBrush(logoRect, C_ACCENT, Color.FromArgb(180, 50, 60), 135f))
                    g.FillEllipse(br, logoRect);
                using (var f = new Font("Segoe UI", 16f, FontStyle.Bold))
                using (var br = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("S", f, br, logoRect, sf);
                }
            };
            _tabHome = MakeSidebarBtn("🏠", 76, 0);
            _tabHelp = MakeSidebarBtn("❓", 136, 1);
            sidebar.Controls.AddRange(new Control[] { _tabHome, _tabHelp });
            Controls.Add(sidebar);
        }

        private Button MakeSidebarBtn(string icon, int y, int tabIdx)
        {
            var btn = new Button
            {
                Text = icon, Location = new Point(10, y),
                Size = new Size(52, 48), FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Emoji", 16f),
                ForeColor = tabIdx == 0 ? C_ACCENT : C_TEXT2,
                BackColor = Color.Transparent, Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = C_CARD;
            btn.Click += (s, e) => SwitchTab(tabIdx);
            return btn;
        }

        private void SwitchTab(int tab)
        {
            _currentTab = tab;
            _homePanel.Visible = tab == 0;
            _helpPanel.Visible = tab == 1;
            _tabHome.ForeColor = tab == 0 ? C_ACCENT : C_TEXT2;
            _tabHelp.ForeColor = tab == 1 ? C_ACCENT : C_TEXT2;
        }

        // ================================================================
        //  CONTENT AREA
        // ================================================================
        private void BuildContent()
        {
            var content = new Panel { Dock = DockStyle.Fill, BackColor = C_BG };

            // Background glass paint
            content.Paint += (s, e) =>
            {
                using (var br = new LinearGradientBrush(content.ClientRectangle,
                    Color.FromArgb(48, 24, 32), Color.FromArgb(38, 18, 26), 135f))
                    e.Graphics.FillRectangle(br, content.ClientRectangle);
            };

            _lblLog = new Label
            {
                Text = "Ready — Select a game and test connection",
                Dock = DockStyle.Bottom, Height = 32,
                BackColor = Color.FromArgb(32, 14, 20),
                ForeColor = C_TEXT2, Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0)
            };
            _homePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            BuildHomePanel();

            _helpPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };
            BuildHelpPanel();

            // WinForms dock order: Fill first, then fixed-size docked controls
            content.Controls.Add(_homePanel);
            content.Controls.Add(_helpPanel);
            content.Controls.Add(_lblLog);     // Bottom - added after Fill

            Controls.Add(content);              // Fill - added before sidebar
        }

        private void BuildHomePanel()
        {
            // ── Header ──────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.Transparent };
            header.Paint += PaintHeader;
            _statusDot = new Panel { Size = new Size(12, 12), BackColor = C_RED };
            MakeRound(_statusDot, 6);
            _statusLbl = new Label
            {
                Text = "DISCONNECTED", ForeColor = C_RED,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                AutoSize = true, BackColor = Color.Transparent
            };
            header.Controls.AddRange(new Control[] { _statusDot, _statusLbl });
            header.Resize += (s, e) =>
            {
                _statusDot.Location = new Point(header.Width - 180, 28);
                _statusLbl.Location = new Point(header.Width - 162, 26);
            };
            // header will be added after body (docking order)

            // ── Main body (scrollable) ──────────────────────────────────
            var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };

            // ── Right tuning panel ──────────────────────────────────────
            var rightPanel = new Panel
            {
                Dock = DockStyle.Right, Width = 260, BackColor = Color.Transparent
            };
            rightPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(4, 4, rightPanel.Width - 8, rightPanel.Height - 8);
                using (var path = RoundRect(r, 16))
                {
                    using (var br = new SolidBrush(Color.FromArgb(50, 26, 34)))
                        g.FillPath(br, path);
                    using (var br = new LinearGradientBrush(r,
                        Color.FromArgb(18, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 135f))
                        g.FillPath(br, path);
                    using (var pen = new Pen(C_BORDER, 1))
                        g.DrawPath(pen, path);
                }
            };

            int ry = 20;
            rightPanel.Controls.Add(MakeLabel("INTENSITY TUNING", 20, ry, C_TEXT2, 8.5f, FontStyle.Bold));
            ry += 30;
            MakeSlider(rightPanel, "PITCH", ry, Color.FromArgb(100, 170, 255), out _trPitch, out _lblPitchVal); ry += 62;
            MakeSlider(rightPanel, "ROLL", ry, C_GREEN, out _trRoll, out _lblRollVal); ry += 62;
            MakeSlider(rightPanel, "YAW", ry, C_ACCENT2, out _trYaw, out _lblYawVal); ry += 72;
            rightPanel.Controls.Add(MakeSep(20, ry, 216)); ry += 20;
            rightPanel.Controls.Add(MakeLabel("MAX ANGLE (°)", 20, ry, C_TEXT2, 8.5f, FontStyle.Bold)); ry += 24;
            _numMax = new NumericUpDown
            {
                Minimum = 3, Maximum = 30, Value = 15,
                Location = new Point(20, ry), Width = 80,
                BackColor = C_CARD, ForeColor = C_TEXT
            };
            rightPanel.Controls.Add(_numMax); ry += 46;
            rightPanel.Controls.Add(MakeSep(20, ry, 216)); ry += 20;
            rightPanel.Controls.Add(MakeLabel("LIVE TELEMETRY", 20, ry, C_TEXT2, 8.5f, FontStyle.Bold)); ry += 24;
            _lblSpeed = MakeLabel("Speed:  0.0 km/h", 20, ry, C_TEXT, 9.5f, FontStyle.Regular);
            rightPanel.Controls.Add(_lblSpeed);

            // ── Left content ────────────────────────────────────────────
            var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            // Game cards row
            leftPanel.Controls.Add(MakeLabel("SELECT GAME", 20, 10, C_TEXT2, 8.5f, FontStyle.Bold));
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var card = new Panel
                {
                    Location = new Point(20 + i * 200, 36),
                    Size = new Size(190, 130), Cursor = Cursors.Hand
                };
                card.Paint += (s, e) => PaintGameCard(e.Graphics, card, idx);
                card.Click += (s, e) => SelectGame(idx);
                var lSel = MakeLabel("", 14, 105, C_ACCENT2, 7.5f, FontStyle.Bold);
                lSel.Click += (s, e) => SelectGame(idx);
                card.Controls.Add(lSel);
                _cards[idx] = card;
                _cardLbl[idx] = lSel;
                leftPanel.Controls.Add(card);
            }

            // Buttons row
            _btnConnect = MakeRoundedBtn("⚡  TEST CONNECTION", new Rectangle(20, 180, 240, 42), C_ACCENT);
            _btnConnect.Click += async (s, e) => await ConnectAsync();
            _btnStart = MakeRoundedBtn("▶  START", new Rectangle(270, 180, 160, 42), C_GREEN);
            _btnStart.ForeColor = Color.FromArgb(20, 10, 15);
            _btnStart.Enabled = false;
            _btnStart.Click += ToggleActive;
            leftPanel.Controls.AddRange(new Control[] { _btnConnect, _btnStart });

            // IP input row
            leftPanel.Controls.Add(MakeLabel("SEAT IP", 450, 188, C_TEXT2, 8f, FontStyle.Bold));
            _txtIp = new TextBox
            {
                Text = "192.168.1.40", Location = new Point(510, 185),
                Width = 130, BackColor = C_CARD, ForeColor = C_TEXT,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5f)
            };
            _txtIp.TextChanged += (s, e) => _seat.SetIp(_txtIp.Text.Trim());
            leftPanel.Controls.Add(_txtIp);

            // Gauges
            _pbGauges = new PictureBox
            {
                Location = new Point(0, 236), Size = new Size(660, 170), BackColor = Color.Transparent
            };
            _pbGauges.Paint += PaintGauges;
            leftPanel.Controls.Add(_pbGauges);

            // Seat preview
            _pbSeat = new PictureBox
            {
                Location = new Point(0, 410), Size = new Size(660, 260), BackColor = Color.Transparent
            };
            _pbSeat.Paint += PaintSeat;
            leftPanel.Controls.Add(_pbSeat);

            // WinForms dock order: Fill first, then Right
            body.Controls.Add(leftPanel);       // Fill - added first
            body.Controls.Add(rightPanel);      // Right - added after Fill

            // body=Fill added first, header=Top added after
            _homePanel.Controls.Add(body);
            _homePanel.Controls.Add(header);
        }
    }
}
