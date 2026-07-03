using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using SHALLControl.Models;
using SHALLControl.Plugins;
using SHALLControl.Services;

namespace SHALLControl
{
    public partial class MainForm : Form
    {
        // ── Olive Green Dashboard Palette ───────────────────────────────
        static readonly Color C_BG      = Color.FromArgb(14,  20, 12);
        static readonly Color C_BG2     = Color.FromArgb(20,  28, 17);
        static readonly Color C_CARD    = Color.FromArgb(25,  36, 21);
        static readonly Color C_CARD_HI = Color.FromArgb(36,  54, 28);
        static readonly Color C_ACCENT  = Color.FromArgb(164, 217, 76);
        static readonly Color C_ACCENT2 = Color.FromArgb(130, 184, 47);
        static readonly Color C_GREEN   = Color.FromArgb(164, 217, 76);
        static readonly Color C_RED     = Color.FromArgb(220, 70, 90);
        static readonly Color C_TEXT    = Color.FromArgb(230, 239, 224);
        static readonly Color C_TEXT2   = Color.FromArgb(143, 158, 139);
        static readonly Color C_BORDER  = Color.FromArgb(46,  66, 38);

        private const int GAME_COUNT = 6;

        // State
        private IGamePlugin _plugin;
        private SeatController _seat;
        private GameConfig _cfg;
        private int _selGame = -1;
        private bool _active;
        private TelemetryData _latest = TelemetryData.Zero;
        private int _currentTab = 0;
        private string[] _customGamePaths = new string[GAME_COUNT];
        private Image[]  _gameImages      = new Image[GAME_COUNT];

        // Controls
        private Panel[]      _cards   = new Panel[GAME_COUNT];
        private Label[]      _cardLbl = new Label[GAME_COUNT];
        private PictureBox[] _cardImg = new PictureBox[GAME_COUNT];
        private Panel   _statusDot;
        private Label   _statusLbl, _lblSpeed, _lblLog;
        private TextBox _txtIp;
        private TouchSlider _trPitch, _trRoll, _trYaw;
        private Label    _lblPitchVal, _lblRollVal, _lblYawVal;
        private NumericUpDown _numMax;
        private Button _btnConnect, _btnStart;
        private PictureBox _pbGauges;
        private Panel _homePanel, _helpPanel, _heroPanel, _telemetryPanel, _tuningPanel, _gaugePanel;
        private Button _tabHome, _tabHelp;
        private System.Windows.Forms.Timer _uiTimer;

        // Update overlay controls
        private Panel _updateOverlay;
        private Label _updateTitle, _updateStatus, _updatePercent;
        private Panel _updateProgressBg, _updateProgressFill;
        private Button _btnInstall, _btnDismiss;
        private UpdateService _updateService;
        private string _pendingDownloadUrl;

        static readonly string[] NAMES  = { "Forza Horizon 5","Euro Truck Sim 2","F1 Series","American Truck Sim","SnowRunner","Dirt Rally" };
        static readonly string[] PROTOS = { "UDP :5300","HTTP :25555","UDP :20777","HTTP :25555","UDP :21777","UDP :20777" };
        static readonly string[] ICONS  = { "🏎","🚛","🏁","🚚","❄","🏔" };
        static readonly Color[] GCOLORS = {
            Color.FromArgb( 64,140,230),
            Color.FromArgb(240,165, 55),
            Color.FromArgb(220, 55, 85),
            Color.FromArgb( 55,185,140),
            Color.FromArgb(110,155,235),
            Color.FromArgb(165, 90,205)
        };

        public MainForm()
        {
            Text = "SHALL XR — Seat Controller";
            Size = new Size(1300, 880);
            MinimumSize = new Size(1100, 760);
            BackColor = C_BG;
            ForeColor = C_TEXT;
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;

            _seat = new SeatController("192.168.1.40");
            _seat.ConnectionChanged += (s, ok) =>
                BeginInvoke((Action)(() => SetSeatStatus(ok)));

            // ORDER MATTERS: Add Fill content first, then Left sidebar
            BuildContent();
            BuildSidebar();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 60 };
            _uiTimer.Tick += (s, e) => RefreshUI();
            _uiTimer.Start();

            FormClosing += async (s, e) =>
            {
                _active = false; _plugin?.Stop();
                await _seat.CenterAsync(); _seat.Dispose();
            };
            _updateService = new UpdateService();
            _updateService.UpdateAvailable += (ver, url) =>
                BeginInvoke((Action)(() => ShowUpdateOverlay(ver, url)));
            _updateService.DownloadProgress += (pct, recv, total) =>
                BeginInvoke((Action)(() => UpdateProgress(pct, recv, total)));
            _updateService.StatusChanged += (msg) =>
                BeginInvoke((Action)(() => UpdateStatusText(msg)));
            _updateService.UpdateFailed += (msg) =>
                BeginInvoke((Action)(() => ShowUpdateError(msg)));
            _updateService.UpdateCompleting += () =>
                BeginInvoke((Action)(() => UpdateStatusText("Restarting…")));
            _ = _updateService.CheckAndUpdateAsync();
        }

        // ================================================================
        //  SIDEBAR  (220 px, dark green)
        // ================================================================
        private void BuildSidebar()
        {
            var sb = new Panel { Dock = DockStyle.Left, Width = 260 };
            sb.Paint += PaintSidebar;

            // Logo
            var logo = new Panel { Location=new Point(0,0), Size=new Size(260,84), BackColor=Color.Transparent };
            logo.Paint += PaintSidebarLogo;
            sb.Controls.Add(logo);

            sb.Controls.Add(SLbl("GAMES", 20, 88));

            // Game rows
            for (int i = 0; i < GAME_COUNT; i++)
            {
                int idx = i;
                var row = new Panel { Location=new Point(8, 110+i*76), Size=new Size(244,68), Cursor=Cursors.Hand, BackColor=Color.Transparent };
                row.Paint += (s,e) => PaintGameCard(e.Graphics, row, idx);
                row.Click += (s,e) => SelectGame(idx);

                var img = new PictureBox { Size=new Size(36,36), Location=new Point(row.Width-46,16), SizeMode=PictureBoxSizeMode.Zoom, BackColor=Color.Transparent, Visible=false };
                img.Click += (s,e) => SelectGame(idx);
                row.Controls.Add(img);
                _cardImg[idx]=img;

                var sel = new Label { Text="", Location=new Point(216,24), AutoSize=true, Font=new Font("Segoe UI",10f,FontStyle.Bold), ForeColor=C_ACCENT, BackColor=Color.Transparent };
                row.Controls.Add(sel);
                _cards[idx]=row; _cardLbl[idx]=sel;
                sb.Controls.Add(row);
            }

            // Divider
            int dy = 110 + GAME_COUNT*76 + 12;
            sb.Controls.Add(new Label { Location=new Point(14,dy), Size=new Size(232,1), BackColor=Color.FromArgb(50,C_BORDER) });

            // Connection
            int cy = dy + 16;
            sb.Controls.Add(SLbl("SEAT IP", 20, cy)); cy += 24;
            _txtIp = new TextBox { Text="192.168.1.40", Location=new Point(14,cy), Width=232, BackColor=Color.FromArgb(18,36,22), ForeColor=C_TEXT, BorderStyle=BorderStyle.FixedSingle, Font=new Font("Segoe UI",10f) };
            _txtIp.TextChanged += (s,e) => _seat.SetIp(_txtIp.Text.Trim());
            sb.Controls.Add(_txtIp); cy += 36;

            _btnConnect = MakeBtn("⚡  TEST CONNECTION", new Rectangle(14,cy,232,48), C_ACCENT);
            _btnConnect.ForeColor = Color.FromArgb(10,30,12);
            _btnConnect.Click += async (s,e) => await ConnectAsync();
            sb.Controls.Add(_btnConnect); cy += 56;

            _statusDot = new Panel { Size=new Size(12,12), Location=new Point(20,cy+6), BackColor=C_RED };
            MakeRound(_statusDot, 6);
            _statusLbl = new Label { Text="DISCONNECTED", Location=new Point(38,cy), AutoSize=true, ForeColor=C_RED, Font=new Font("Segoe UI",8.5f,FontStyle.Bold), BackColor=Color.Transparent };
            sb.Controls.Add(_statusDot); sb.Controls.Add(_statusLbl);

            // Tab bar
            var tabs = new Panel { Dock=DockStyle.Bottom, Height=64, BackColor=Color.FromArgb(8,16,10) };
            _tabHome = PillBtn("🏠  Home", 0); _tabHome.Location=new Point(8,8);
            _tabHelp = PillBtn("❓  Help", 1); _tabHelp.Location=new Point(132,8);
            tabs.Controls.Add(_tabHome); tabs.Controls.Add(_tabHelp);
            sb.Controls.Add(tabs);

            Controls.Add(sb);
        }

        private Label SLbl(string t, int x, int y) => new Label { Text=t, Location=new Point(x,y), AutoSize=true, Font=new Font("Segoe UI",7.5f,FontStyle.Bold), ForeColor=C_TEXT2, BackColor=Color.Transparent };

        private Button PillBtn(string text, int tab)
        {
            bool on = tab==0;
            var b = new Button { Text=text, Size=new Size(120,48), FlatStyle=FlatStyle.Flat, Font=new Font("Segoe UI",9.5f,FontStyle.Bold), ForeColor=on?C_BG:C_TEXT2, BackColor=on?C_ACCENT:C_CARD, Cursor=Cursors.Hand };
            b.FlatAppearance.BorderSize=0;
            b.FlatAppearance.MouseOverBackColor=C_CARD_HI;
            b.Region=new Region(RoundRect(new Rectangle(0,0,b.Width,b.Height),10));
            b.Click += (s,e) => SwitchTab(tab);
            return b;
        }

        private void SwitchTab(int tab)
        {
            _currentTab=tab;
            _homePanel.Visible=tab==0;
            _helpPanel.Visible=tab==1;
            void Style(Button b, bool on) { b.BackColor=on?C_ACCENT:C_CARD; b.ForeColor=on?C_BG:C_TEXT2; }
            Style(_tabHome, tab==0); Style(_tabHelp, tab==1);
        }

        // ================================================================
        //  CONTENT  (Fill — added BEFORE sidebar so docking is correct)
        // ================================================================
        private void BuildContent()
        {
            var content = new Panel { Dock=DockStyle.Fill, BackColor=C_BG };
            content.Paint += (s,e) =>
            {
                using (var br = new LinearGradientBrush(content.ClientRectangle, Color.FromArgb(12,24,15), Color.FromArgb(8,16,11), 160f))
                    e.Graphics.FillRectangle(br, content.ClientRectangle);
            };

            _lblLog = new Label { Dock=DockStyle.Bottom, Height=26, Text="Ready — Select a game and connect", BackColor=Color.FromArgb(16,30,19), ForeColor=C_TEXT2, Font=new Font("Segoe UI",8.5f), TextAlign=ContentAlignment.MiddleLeft, Padding=new Padding(14,0,0,0) };
            var lblVersion = new Label { Dock=DockStyle.Right, Width=100, Text="v" + Application.ProductVersion, BackColor=Color.Transparent, ForeColor=C_TEXT2, Font=new Font("Segoe UI",8.5f), TextAlign=ContentAlignment.MiddleRight, Padding=new Padding(0,0,14,0) };
            _lblLog.Controls.Add(lblVersion);

            _homePanel = new Panel { Dock=DockStyle.Fill, BackColor=Color.Transparent };
            BuildHomePanel();

            _helpPanel = new Panel { Dock=DockStyle.Fill, BackColor=Color.Transparent, Visible=false };
            BuildHelpPanel();

            content.Controls.Add(_homePanel);
            content.Controls.Add(_helpPanel);
            content.Controls.Add(_lblLog);

            // Build the update overlay (hidden by default)
            BuildUpdateOverlay(content);

            Controls.Add(content);
        }

        // ================================================================
        //  HOME — manual Resize layout (avoids docking conflicts)
        // ================================================================
        private void BuildHomePanel()
        {
            const int PAD = 10;

            // Hero
            _heroPanel = new Panel { BackColor=Color.Transparent };
            _heroPanel.Paint += (s,e) => PaintHeroPanel(e.Graphics, _heroPanel);

            _btnStart = MakeBtn("▶  START", new Rectangle(0,0,148,42), C_GREEN);
            _btnStart.ForeColor=Color.FromArgb(8,24,10); _btnStart.Enabled=false;
            _btnStart.Click += ToggleActive;
            _heroPanel.Controls.Add(_btnStart);

            _lblSpeed = new Label { Text="— km/h", AutoSize=false, Size=new Size(180,24), TextAlign=ContentAlignment.MiddleCenter, Font=new Font("Segoe UI",11f,FontStyle.Bold), ForeColor=C_ACCENT, BackColor=Color.Transparent };
            _heroPanel.Controls.Add(_lblSpeed);

            var bPath = MakeBtn("Set Game Path",  new Rectangle(0,0,160,40), C_CARD_HI); bPath.ForeColor=C_TEXT; bPath.Click+=(s,e)=>SetCustomGamePath(); _heroPanel.Controls.Add(bPath);

            // Gauges
            _gaugePanel = new Panel { BackColor=Color.Transparent };
            _gaugePanel.Paint += PaintGaugesWrap;
            _pbGauges = new PictureBox { Dock=DockStyle.Fill, BackColor=Color.Transparent };
            _pbGauges.Paint += PaintGauges;
            _gaugePanel.Controls.Add(_pbGauges);

            // Tuning
            _tuningPanel = new Panel { BackColor=Color.Transparent };
            _tuningPanel.Paint += PaintTuningPanel;
            BuildTuningControls();

            // Telemetry
            _telemetryPanel = new Panel { BackColor=Color.Transparent };
            SetDoubleBuffered(_telemetryPanel);
            SetDoubleBuffered(_heroPanel);
            SetDoubleBuffered(_gaugePanel);
            SetDoubleBuffered(_tuningPanel);
            
            _telemetryPanel.Paint += PaintTelemetryPanel;
            BuildTelemetryLabels();

            _homePanel.Controls.Add(_heroPanel);
            _homePanel.Controls.Add(_gaugePanel);
            _homePanel.Controls.Add(_tuningPanel);
            _homePanel.Controls.Add(_telemetryPanel);

            // Manual layout on resize
            _homePanel.Resize += (s,e) =>
            {
                int w=_homePanel.Width, h=_homePanel.Height;
                int x=PAD, y=PAD;
                int heroH=140, gaugeH=180, telW=340;

                _heroPanel.SetBounds(x, y, w-2*PAD, heroH);

                // Position hero child controls
                _btnStart.Size      = new Size(180, 48);
                _btnStart.Location  = new Point(_heroPanel.Width-200, 32);
                _lblSpeed.Location  = new Point(_heroPanel.Width-200, 88);
                
                bPath.Size          = new Size(160, 48);
                bPath.Location      = new Point(_heroPanel.Width-376, 32);

                y += heroH + PAD;
                _gaugePanel.SetBounds(x, y, w-2*PAD, gaugeH);

                y += gaugeH + PAD;
                int botH = h - y - PAD;
                _tuningPanel.SetBounds(x,              y, w-2*PAD-telW-PAD, botH);
                _telemetryPanel.SetBounds(x+(w-2*PAD-telW), y, telW, botH);
            };
        }

        private void BuildTuningControls()
        {
            int x=30, y=24;
            _tuningPanel.Controls.Add(Lbl("INTENSITY TUNING", x, y, C_TEXT2, 9f, FontStyle.Bold)); y+=46;
            MakeSlider(_tuningPanel,"PITCH",x,y,Color.FromArgb(100,170,255),out _trPitch,out _lblPitchVal); y+=66;
            MakeSlider(_tuningPanel,"ROLL", x,y,C_ACCENT2,                  out _trRoll, out _lblRollVal);  y+=66;
            MakeSlider(_tuningPanel,"YAW",  x,y,Color.FromArgb(200,150,255),out _trYaw,  out _lblYawVal);  y+=74;
            
            _tuningPanel.Controls.Add(Lbl("MAX ANGLE (°)", x, y+14, C_TEXT2, 9f, FontStyle.Bold)); 
            
            _numMax=new NumericUpDown { Minimum=3,Maximum=30,Value=15, Visible=false };
            _tuningPanel.Controls.Add(_numMax);

            var btnMinus = MakeBtn("−", new Rectangle(x+140, y, 56, 48), C_CARD_HI);
            var btnPlus  = MakeBtn("+", new Rectangle(x+280, y, 56, 48), C_CARD_HI);
            btnMinus.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            btnPlus.Font  = new Font("Segoe UI", 16f, FontStyle.Bold);
            
            var lblAngle = Lbl("15°", x+200, y+10, C_TEXT, 14f, FontStyle.Bold);
            lblAngle.AutoSize = false;
            lblAngle.Size = new Size(76, 32);
            lblAngle.TextAlign = ContentAlignment.MiddleCenter;

            btnMinus.Click += (s,e) => { int v = (int)_numMax.Value; if(v>3) { _numMax.Value=v-1; lblAngle.Text=_numMax.Value+"°"; } };
            btnPlus.Click  += (s,e) => { int v = (int)_numMax.Value; if(v<30) { _numMax.Value=v+1; lblAngle.Text=_numMax.Value+"°"; } };

            _tuningPanel.Controls.Add(btnMinus);
            _tuningPanel.Controls.Add(lblAngle);
            _tuningPanel.Controls.Add(btnPlus);
        }

        private void BuildTelemetryLabels()
        {
            int x=30, y=42, g=34;
            _telemetryPanel.Controls.Add(Lbl("TELEMETRY", x, 16, C_TEXT2, 9f, FontStyle.Bold));
            void R(string n, ref int yy)
            {
                _telemetryPanel.Controls.Add(Lbl(n, x, yy, C_TEXT2, 10f, FontStyle.Regular)); yy+=g;
            }
            R("Pitch", ref y);
            R("Roll",  ref y);
            R("Yaw",   ref y);
            R("Speed", ref y); y+=6;
            R("Surge", ref y);
            R("Sway",  ref y);
            R("Heave", ref y);
            R("Valid", ref y);
        }

        public static void SetDoubleBuffered(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered", System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, c, new object[] { true });
        }

        private void SetCustomGamePath()
        {
            if (_selGame<0){Log("Select a game first.");return;}
            using (var d=new OpenFileDialog{Title="Select executable",Filter="Executable|*.exe|All|*.*"})
            {
                if (!string.IsNullOrEmpty(_customGamePaths[_selGame])) d.InitialDirectory=Path.GetDirectoryName(_customGamePaths[_selGame]);
                if (d.ShowDialog()==DialogResult.OK){ _customGamePaths[_selGame]=d.FileName; _cards[_selGame].Invalidate(); Log("Path: "+d.FileName); }
            }
        }

        private void SetGameImage()
        {
            if (_selGame<0){Log("Select a game first.");return;}
            using (var d=new OpenFileDialog{Title="Select image",Filter="Images|*.png;*.jpg;*.bmp;*.ico|All|*.*"})
            {
                if (d.ShowDialog()==DialogResult.OK)
                    try { var img=Image.FromFile(d.FileName); _gameImages[_selGame]=img; _cardImg[_selGame].Image=img; _cardImg[_selGame].Visible=true; _cards[_selGame].Invalidate(); Log("Image set."); }
                    catch(Exception ex){Log("Error: "+ex.Message);}
            }
        }

        // ================================================================
        //  UPDATE OVERLAY
        // ================================================================
        private void BuildUpdateOverlay(Panel parent)
        {
            _updateOverlay = new Panel { Visible=false, BackColor=Color.FromArgb(180, 0, 0, 0) };
            _updateOverlay.Dock = DockStyle.Fill;
            SetDoubleBuffered(_updateOverlay);

            // Card container (centered via Resize)
            var card = new Panel { Size=new Size(480, 260), BackColor=Color.Transparent };
            SetDoubleBuffered(card);
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using (var path = RoundRect(r, 18))
                {
                    using (var br = new LinearGradientBrush(r, Color.FromArgb(30, 44, 26), Color.FromArgb(18, 28, 16), 135f))
                        g.FillPath(br, path);
                    using (var br = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                        g.FillPath(br, path);
                    using (var pen = new Pen(Color.FromArgb(120, C_ACCENT), 1.5f))
                        g.DrawPath(pen, path);
                }
            };

            // Title
            _updateTitle = new Label { Text="Update Available", Location=new Point(28, 22), AutoSize=true, Font=new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor=C_TEXT, BackColor=Color.Transparent };
            card.Controls.Add(_updateTitle);

            // Status
            _updateStatus = new Label { Text="A new version is available.", Location=new Point(28, 58), Size=new Size(420, 22), Font=new Font("Segoe UI", 9.5f), ForeColor=C_TEXT2, BackColor=Color.Transparent };
            card.Controls.Add(_updateStatus);

            // Progress bar background
            _updateProgressBg = new Panel { Location=new Point(28, 96), Size=new Size(424, 18), BackColor=Color.Transparent };
            SetDoubleBuffered(_updateProgressBg);
            _updateProgressBg.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, _updateProgressBg.Width - 1, _updateProgressBg.Height - 1);
                using (var path = RoundRect(r, 9))
                {
                    using (var br = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                        g.FillPath(br, path);
                    using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                        g.DrawPath(pen, path);
                }
            };
            _updateProgressBg.Visible = false;
            card.Controls.Add(_updateProgressBg);

            // Progress bar fill
            _updateProgressFill = new Panel { Location=new Point(1, 1), Size=new Size(0, 16), BackColor=Color.Transparent };
            SetDoubleBuffered(_updateProgressFill);
            _updateProgressFill.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                if (_updateProgressFill.Width < 8) return;
                var r = new Rectangle(0, 0, _updateProgressFill.Width - 1, _updateProgressFill.Height - 1);
                using (var path = RoundRect(r, 8))
                {
                    using (var br = new LinearGradientBrush(r, C_ACCENT, C_ACCENT2, 0f))
                        g.FillPath(br, path);
                    // Glassy highlight on top half
                    var hlRect = new Rectangle(0, 0, _updateProgressFill.Width, _updateProgressFill.Height / 2);
                    if (hlRect.Width > 0 && hlRect.Height > 0)
                        using (var br = new LinearGradientBrush(new Rectangle(0, 0, _updateProgressFill.Width, _updateProgressFill.Height), Color.FromArgb(90, 255, 255, 255), Color.Transparent, System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                            g.FillPath(br, path);
                }
            };
            _updateProgressBg.Controls.Add(_updateProgressFill);

            // Percent label
            _updatePercent = new Label { Text="0%", Location=new Point(28, 120), AutoSize=true, Font=new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor=C_ACCENT, BackColor=Color.Transparent, Visible=false };
            card.Controls.Add(_updatePercent);

            // Install button
            _btnInstall = MakeBtn("⬇  Install Update", new Rectangle(28, 160, 200, 48), C_ACCENT);
            _btnInstall.ForeColor = Color.FromArgb(10, 30, 12);
            _btnInstall.Click += async (s, e) =>
            {
                if (string.IsNullOrEmpty(_pendingDownloadUrl)) return;
                _btnInstall.Enabled = false;
                _btnInstall.Text = "Downloading…";
                _btnDismiss.Visible = false;
                _updateProgressBg.Visible = true;
                _updatePercent.Visible = true;
                await _updateService.PerformUpdateAsync(_pendingDownloadUrl);
            };
            card.Controls.Add(_btnInstall);

            // Dismiss button
            _btnDismiss = MakeBtn("Not Now", new Rectangle(248, 160, 140, 48), C_CARD_HI);
            _btnDismiss.ForeColor = C_TEXT;
            _btnDismiss.Click += (s, e) =>
            {
                _updateOverlay.Visible = false;
                Log("Update skipped.");
            };
            card.Controls.Add(_btnDismiss);

            _updateOverlay.Controls.Add(card);

            // Center the card on resize
            _updateOverlay.Resize += (s, e) =>
            {
                card.Location = new Point(
                    (_updateOverlay.Width - card.Width) / 2,
                    (_updateOverlay.Height - card.Height) / 2
                );
            };

            parent.Controls.Add(_updateOverlay);
            _updateOverlay.BringToFront();
        }

        private void ShowUpdateOverlay(string version, string downloadUrl)
        {
            _pendingDownloadUrl = downloadUrl;
            _updateTitle.Text = "🔄  Update Available";
            _updateStatus.Text = $"Version {version} is ready to install.";
            _updateProgressBg.Visible = false;
            _updatePercent.Visible = false;
            _btnInstall.Enabled = true;
            _btnInstall.Text = "⬇  Install Update";
            _btnDismiss.Visible = true;
            _updateOverlay.Visible = true;
            Log($"Update {version} available.");
        }

        private void UpdateProgress(int percent, long received, long total)
        {
            int maxW = _updateProgressBg.Width - 2;
            int fillW = Math.Max(0, Math.Min(maxW, (int)(maxW * percent / 100.0)));
            _updateProgressFill.Width = fillW;
            _updateProgressFill.Invalidate();

            if (total > 0)
            {
                double mb = received / (1024.0 * 1024.0);
                double totalMb = total / (1024.0 * 1024.0);
                _updatePercent.Text = $"{percent}%  —  {mb:0.0} / {totalMb:0.0} MB";
            }
            else
            {
                _updatePercent.Text = $"{percent}%";
            }
        }

        private void UpdateStatusText(string msg)
        {
            _updateStatus.Text = msg;
            _btnInstall.Text = msg;
            _btnInstall.Enabled = false;
        }

        private void ShowUpdateError(string msg)
        {
            _updateStatus.Text = msg;
            _updateStatus.ForeColor = C_RED;
            _btnInstall.Visible = false;
            _btnDismiss.Text = "Close";
            _btnDismiss.Visible = true;
            Log(msg);
        }
    }
}
