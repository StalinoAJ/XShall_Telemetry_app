using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using SHALLControl.Models;
using SHALLControl.Plugins;
using SHALLControl.Services;

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

        // ── Game count ──────────────────────────────────────────────────
        private const int GAME_COUNT = 6;

        // ── State ───────────────────────────────────────────────────────
        private IGamePlugin _plugin;
        private SeatController _seat;
        private GameConfig _cfg;
        private int _selGame = -1;
        private bool _active;
        private TelemetryData _latest = TelemetryData.Zero;
        private int _currentTab = 0;
        private string[] _customGamePaths = new string[GAME_COUNT];
        private Image[] _gameImages = new Image[GAME_COUNT];

        // ── Controls ────────────────────────────────────────────────────
        private Panel[] _cards = new Panel[GAME_COUNT];
        private Label[] _cardLbl = new Label[GAME_COUNT];
        private PictureBox[] _cardImg = new PictureBox[GAME_COUNT];
        private Panel _statusDot;
        private Label _statusLbl, _lblSpeed, _lblLog;
        private TextBox _txtIp;
        private TrackBar _trPitch, _trRoll, _trYaw;
        private Label _lblPitchVal, _lblRollVal, _lblYawVal;
        private NumericUpDown _numMax;
        private Button _btnConnect, _btnStart;
        private PictureBox _pbGauges;
        private Panel _homePanel, _helpPanel;
        private Panel _telemetryPanel;
        private Button _tabHome, _tabHelp;
        private System.Windows.Forms.Timer _uiTimer;

        // ── Live telemetry labels ───────────────────────────────────────
        private Label _lblTelPitch, _lblTelRoll, _lblTelYaw;
        private Label _lblTelSurge, _lblTelSway, _lblTelHeave;
        private Label _lblTelSpeed2, _lblTelValid;

        static readonly string[] NAMES  = {
            "Forza Horizon 5", "Euro Truck Sim 2", "F1 Series",
            "American Truck Sim", "SnowRunner", "Dirt Rally"
        };
        static readonly string[] PROTOS = {
            "UDP  :5300", "HTTP :25555", "UDP  :20777",
            "HTTP :25555", "UDP  :21777", "UDP  :20777"
        };
        static readonly string[] ICONS  = { "🏎", "🚛", "🏁", "🚚", "❄", "🏔" };
        static readonly Color[] GCOLORS = {
            Color.FromArgb(0, 120, 215),
            Color.FromArgb(255, 160, 50),
            Color.FromArgb(220, 40, 40),
            Color.FromArgb(60, 180, 120),
            Color.FromArgb(100, 160, 220),
            Color.FromArgb(200, 140, 50)
        };

        public MainForm()
        {
            Text = "SHALL XR — Seat Controller";
            Size = new Size(1280, 860);
            MinimumSize = new Size(1100, 750);
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

            // Check for updates asynchronously
            _ = new UpdateService().CheckAndUpdateAsync();
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

            // Game cards - 2 rows of 3
            leftPanel.Controls.Add(MakeLabel("SELECT GAME", 20, 10, C_TEXT2, 8.5f, FontStyle.Bold));
            for (int i = 0; i < GAME_COUNT; i++)
            {
                int idx = i;
                int row = i / 3;
                int col = i % 3;
                var card = new Panel
                {
                    Location = new Point(20 + col * 200, 36 + row * 140),
                    Size = new Size(190, 130), Cursor = Cursors.Hand
                };
                card.Paint += (s, e) => PaintGameCard(e.Graphics, card, idx);
                card.Click += (s, e) => SelectGame(idx);

                // Game image (small icon in top-right, replaces emoji when set)
                var imgBox = new PictureBox
                {
                    Size = new Size(36, 36),
                    Location = new Point(card.Width - 46, 8),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                imgBox.Click += (s, e) => SelectGame(idx);
                card.Controls.Add(imgBox);
                _cardImg[idx] = imgBox;

                var lSel = MakeLabel("", 14, 105, C_ACCENT2, 7.5f, FontStyle.Bold);
                lSel.Click += (s, e) => SelectGame(idx);
                card.Controls.Add(lSel);
                _cards[idx] = card;
                _cardLbl[idx] = lSel;
                leftPanel.Controls.Add(card);
            }

            // Buttons row (below 2 rows of cards)
            int btnY = 36 + 2 * 140 + 10;
            _btnConnect = MakeRoundedBtn("⚡  TEST CONNECTION", new Rectangle(20, btnY, 240, 42), C_ACCENT);
            _btnConnect.Click += async (s, e) => await ConnectAsync();
            _btnStart = MakeRoundedBtn("▶  START", new Rectangle(270, btnY, 160, 42), C_GREEN);
            _btnStart.ForeColor = Color.FromArgb(20, 10, 15);
            _btnStart.Enabled = false;
            _btnStart.Click += ToggleActive;
            leftPanel.Controls.AddRange(new Control[] { _btnConnect, _btnStart });

            // IP input row
            leftPanel.Controls.Add(MakeLabel("SEAT IP", 450, btnY + 8, C_TEXT2, 8f, FontStyle.Bold));
            _txtIp = new TextBox
            {
                Text = "192.168.1.40", Location = new Point(510, btnY + 5),
                Width = 130, BackColor = C_CARD, ForeColor = C_TEXT,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5f)
            };
            _txtIp.TextChanged += (s, e) => _seat.SetIp(_txtIp.Text.Trim());
            leftPanel.Controls.Add(_txtIp);

            // Custom game path button
            var btnSetPath = MakeRoundedBtn("📁  SET GAME PATH", new Rectangle(20, btnY + 52, 200, 36), C_CARD_HI);
            btnSetPath.ForeColor = C_TEXT;
            btnSetPath.Click += (s, e) => SetCustomGamePath();
            leftPanel.Controls.Add(btnSetPath);

            // Set game image button
            var btnSetImg = MakeRoundedBtn("🖼  SET GAME IMAGE", new Rectangle(230, btnY + 52, 200, 36), C_CARD_HI);
            btnSetImg.ForeColor = C_TEXT;
            btnSetImg.Click += (s, e) => SetGameImage();
            leftPanel.Controls.Add(btnSetImg);

            // Gauges (angle display)
            _pbGauges = new PictureBox
            {
                Location = new Point(0, btnY + 100), Size = new Size(660, 170), BackColor = Color.Transparent
            };
            _pbGauges.Paint += PaintGauges;
            leftPanel.Controls.Add(_pbGauges);

            // ── Live telemetry data panel ────────────────────────────────
            _telemetryPanel = new Panel
            {
                Location = new Point(0, btnY + 278),
                Size = new Size(660, 220),
                BackColor = Color.Transparent
            };
            _telemetryPanel.Paint += PaintTelemetryPanel;
            BuildTelemetryLabels();
            leftPanel.Controls.Add(_telemetryPanel);

            // WinForms dock order: Fill first, then Right
            body.Controls.Add(leftPanel);       // Fill - added first
            body.Controls.Add(rightPanel);      // Right - added after Fill

            // body=Fill added first, header=Top added after
            _homePanel.Controls.Add(body);
            _homePanel.Controls.Add(header);
        }

        private void BuildTelemetryLabels()
        {
            int startX = 28, startY = 42;
            int col2X = 340;

            _telemetryPanel.Controls.Add(MakeLabel("INCOMING TELEMETRY DATA", 28, 12, C_TEXT2, 8.5f, FontStyle.Bold));

            // Column 1
            _telemetryPanel.Controls.Add(MakeLabel("Pitch:", startX, startY, C_TEXT2, 9f, FontStyle.Regular));
            _lblTelPitch = MakeLabel("0.000°", startX + 80, startY, Color.FromArgb(100, 170, 255), 9.5f, FontStyle.Bold);
            _telemetryPanel.Controls.Add(_lblTelPitch);

            _telemetryPanel.Controls.Add(MakeLabel("Roll:", startX, startY + 28, C_TEXT2, 9f, FontStyle.Regular));
            _lblTelRoll = MakeLabel("0.000°", startX + 80, startY + 28, C_GREEN, 9.5f, FontStyle.Bold);
            _telemetryPanel.Controls.Add(_lblTelRoll);

            _telemetryPanel.Controls.Add(MakeLabel("Yaw:", startX, startY + 56, C_TEXT2, 9f, FontStyle.Regular));
            _lblTelYaw = MakeLabel("0.000°", startX + 80, startY + 56, C_ACCENT2, 9.5f, FontStyle.Bold);
            _telemetryPanel.Controls.Add(_lblTelYaw);

            _telemetryPanel.Controls.Add(MakeLabel("Speed:", startX, startY + 84, C_TEXT2, 9f, FontStyle.Regular));
            _lblTelSpeed2 = MakeLabel("0.0 km/h", startX + 80, startY + 84, C_TEXT, 9.5f, FontStyle.Bold);
            _telemetryPanel.Controls.Add(_lblTelSpeed2);

            // Column 2
            _telemetryPanel.Controls.Add(MakeLabel("Surge:", col2X, startY, C_TEXT2, 9f, FontStyle.Regular));
            _lblTelSurge = MakeLabel("0.000 G", col2X + 80, startY, Color.FromArgb(180, 130, 255), 9.5f, FontStyle.Bold);
            _telemetryPanel.Controls.Add(_lblTelSurge);

            _telemetryPanel.Controls.Add(MakeLabel("Sway:", col2X, startY + 28, C_TEXT2, 9f, FontStyle.Regular));
            _lblTelSway = MakeLabel("0.000 G", col2X + 80, startY + 28, Color.FromArgb(255, 180, 100), 9.5f, FontStyle.Bold);
            _telemetryPanel.Controls.Add(_lblTelSway);

            _telemetryPanel.Controls.Add(MakeLabel("Heave:", col2X, startY + 56, C_TEXT2, 9f, FontStyle.Regular));
            _lblTelHeave = MakeLabel("0.000 G", col2X + 80, startY + 56, Color.FromArgb(100, 220, 220), 9.5f, FontStyle.Bold);
            _telemetryPanel.Controls.Add(_lblTelHeave);

            _telemetryPanel.Controls.Add(MakeLabel("Valid:", col2X, startY + 84, C_TEXT2, 9f, FontStyle.Regular));
            _lblTelValid = MakeLabel("—", col2X + 80, startY + 84, C_TEXT2, 9.5f, FontStyle.Bold);
            _telemetryPanel.Controls.Add(_lblTelValid);
        }

        // ================================================================
        //  CUSTOM PATH & IMAGE
        // ================================================================
        private void SetCustomGamePath()
        {
            if (_selGame < 0)
            {
                Log("Select a game first before setting its path.");
                return;
            }

            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select game executable for " + NAMES[_selGame];
                dlg.Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*";
                if (!string.IsNullOrEmpty(_customGamePaths[_selGame]))
                    dlg.InitialDirectory = Path.GetDirectoryName(_customGamePaths[_selGame]);
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _customGamePaths[_selGame] = dlg.FileName;
                    Log("Game path set: " + dlg.FileName);
                }
            }
        }

        private void SetGameImage()
        {
            if (_selGame < 0)
            {
                Log("Select a game first before setting its image.");
                return;
            }

            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select image for " + NAMES[_selGame];
                dlg.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico|All files (*.*)|*.*";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var img = Image.FromFile(dlg.FileName);
                        _gameImages[_selGame] = img;
                        _cardImg[_selGame].Image = img;
                        _cards[_selGame].Invalidate();
                        Log("Image set for " + NAMES[_selGame]);
                    }
                    catch (Exception ex)
                    {
                        Log("Failed to load image: " + ex.Message);
                    }
                }
            }
        }
    }
}
