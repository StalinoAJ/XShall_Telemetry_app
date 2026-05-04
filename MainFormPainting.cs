using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SHALLControl
{
    public partial class MainForm
    {
        // ================================================================
        //  GDI+ PAINTING
        // ================================================================
        private void PaintHeader(object sender, PaintEventArgs e)
        {
            var p = (Panel)sender;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Glass gradient bg
            using (var br = new LinearGradientBrush(p.ClientRectangle,
                Color.FromArgb(55, 28, 38), Color.FromArgb(42, 20, 28), 90f))
                g.FillRectangle(br, p.ClientRectangle);
            using (var br = new LinearGradientBrush(p.ClientRectangle,
                Color.FromArgb(12, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                g.FillRectangle(br, p.ClientRectangle);
            using (var pen = new Pen(C_BORDER, 1))
                g.DrawLine(pen, 0, p.Height - 1, p.Width, p.Height - 1);
            using (var f = new Font("Segoe UI", 18f, FontStyle.Bold))
            using (var br = new SolidBrush(C_TEXT))
                g.DrawString("SHALL XR", f, br, 20, 14);
            using (var f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var br = new SolidBrush(C_ACCENT))
                g.DrawString("SEAT CONTROLLER", f, br, 22, 42);
        }

        private void PaintGameCard(Graphics g, Panel card, int idx)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool sel = idx == _selGame;
            var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);

            using (var path = RoundRect(rect, 18))
            {
                // Gradient glass fill
                Color c1 = sel ? Color.FromArgb(130, GCOLORS[idx].R, GCOLORS[idx].G, GCOLORS[idx].B) : C_CARD;
                Color c2 = sel ? Color.FromArgb(50, GCOLORS[idx].R, GCOLORS[idx].G, GCOLORS[idx].B) : C_BG2;
                using (var br = new LinearGradientBrush(rect, c1, c2, 135f))
                    g.FillPath(br, path);
                // Glass shimmer
                using (var br = new LinearGradientBrush(rect,
                    Color.FromArgb(sel ? 40 : 18, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255), 135f))
                    g.FillPath(br, path);
                // Border
                using (var pen = new Pen(sel ? Color.FromArgb(180, GCOLORS[idx]) : C_BORDER, sel ? 2f : 1f))
                    g.DrawPath(pen, path);
            }

            // Color dot
            using (var br = new SolidBrush(GCOLORS[idx]))
                g.FillEllipse(br, 16, 18, 14, 14);

            // Icon emoji (top right) — only show if no custom image is set
            if (_gameImages[idx] == null)
            {
                using (var f = new Font("Segoe UI Emoji", 20f))
                using (var br = new SolidBrush(Color.FromArgb(160, 255, 255, 255)))
                    g.DrawString(ICONS[idx], f, br, card.Width - 48, 10);
            }

            // Game name
            using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (var br = new SolidBrush(C_TEXT))
                g.DrawString(NAMES[idx], f, br, 16, 44);

            // Protocol
            using (var f = new Font("Segoe UI", 8.5f))
            using (var br = new SolidBrush(C_TEXT2))
                g.DrawString(PROTOS[idx], f, br, 16, 68);

            // Custom path indicator
            if (!string.IsNullOrEmpty(_customGamePaths[idx]))
            {
                using (var f = new Font("Segoe UI", 7f))
                using (var br = new SolidBrush(C_GREEN))
                    g.DrawString("📁 Path set", f, br, 16, 86);
            }
        }

        private void PaintGauges(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = _pbGauges.Width, h = _pbGauges.Height;

            int gw = w / 3;
            DrawGauge(g, new Rectangle(0, 0, gw, h), "PITCH", _seat.LastPitch, Color.FromArgb(100, 170, 255));
            DrawGauge(g, new Rectangle(gw, 0, gw, h), "ROLL", _seat.LastRoll, C_GREEN);
            DrawGauge(g, new Rectangle(gw * 2, 0, gw, h), "YAW", _seat.LastYaw, C_ACCENT2);
        }

        private void DrawGauge(Graphics g, Rectangle bounds, string label, int value, Color accent)
        {
            int cx = bounds.Left + bounds.Width / 2;
            int cy = bounds.Top + bounds.Height / 2 + 4;
            int r = Math.Min(bounds.Width, bounds.Height) / 2 - 20;

            // Outer glow
            using (var pen = new Pen(Color.FromArgb(18, accent), 18f))
            { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, 150, 240); }
            // Background arc
            using (var pen = new Pen(C_BORDER, 7f))
            { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, 150, 240); }
            // Value arc
            float pct = Math.Min(Math.Abs(value) / 15f, 1f);
            float sweep = pct * 120f;
            float start = value >= 0 ? 270f : 270f - sweep;
            if (sweep > 0.5f)
                using (var pen = new Pen(accent, 7f))
                { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, start, sweep); }

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var f = new Font("Segoe UI", 20f, FontStyle.Bold))
            using (var br = new SolidBrush(C_TEXT))
                g.DrawString((value >= 0 ? "+" : "") + value + "°", f, br, new RectangleF(cx - r, cy - r, r * 2, r * 2), sf);
            using (var f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var br = new SolidBrush(C_TEXT2))
                g.DrawString(label, f, br, new RectangleF(bounds.Left, bounds.Bottom - 22, bounds.Width, 20), sf);
        }

        private void PaintTelemetryPanel(object sender, PaintEventArgs e)
        {
            var p = (Panel)sender;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(14, 0, p.Width - 28, p.Height - 4);

            using (var path = RoundRect(rect, 16))
            {
                // Dark glass background
                using (var br = new SolidBrush(Color.FromArgb(50, 26, 34)))
                    g.FillPath(br, path);
                using (var br = new LinearGradientBrush(rect,
                    Color.FromArgb(12, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 135f))
                    g.FillPath(br, path);
                using (var pen = new Pen(C_BORDER, 1))
                    g.DrawPath(pen, path);
            }

            // Divider line between columns
            using (var pen = new Pen(Color.FromArgb(60, C_BORDER), 1))
                g.DrawLine(pen, rect.Left + rect.Width / 2, rect.Top + 36, rect.Left + rect.Width / 2, rect.Bottom - 16);

            // Active indicator
            if (_active)
            {
                using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                using (var br = new SolidBrush(C_GREEN))
                    g.DrawString("● RECEIVING", f, br, rect.Right - 100, rect.Top + 12);
            }
            else
            {
                using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                using (var br = new SolidBrush(C_TEXT2))
                    g.DrawString("○ IDLE", f, br, rect.Right - 60, rect.Top + 12);
            }
        }

        // ================================================================
        //  HELPERS
        // ================================================================
        private void SetSeatStatus(bool ok)
        {
            _statusDot.BackColor = ok ? C_GREEN : C_RED;
            _statusLbl.ForeColor = ok ? C_GREEN : C_RED;
            _statusLbl.Text = ok ? "CONNECTED" : "DISCONNECTED";
            MakeRound(_statusDot, 6);
        }

        private void Log(string msg)
        {
            if (_lblLog.InvokeRequired) { _lblLog.BeginInvoke((Action)(() => Log(msg))); return; }
            _lblLog.Text = msg;
        }

        private static Label MakeLabel(string text, int x, int y, Color fore, float size, FontStyle style)
        {
            return new Label
            {
                Text = text, Location = new Point(x, y), ForeColor = fore,
                Font = new Font("Segoe UI", size, style), AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        /// <summary>Creates a button with rounded corners and glass gradient.</summary>
        private Button MakeRoundedBtn(string text, Rectangle r, Color bgColor)
        {
            var b = new Button
            {
                Text = text, Location = r.Location, Size = r.Size,
                BackColor = bgColor, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = C_CARD_HI;
            // Rounded corners
            b.Region = new Region(RoundRect(new Rectangle(0, 0, r.Width, r.Height), 12));
            b.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
                using (var path = RoundRect(rect, 12))
                {
                    using (var br = new LinearGradientBrush(rect, b.BackColor,
                        Color.FromArgb(Math.Max(b.BackColor.R - 30, 0), Math.Max(b.BackColor.G - 30, 0), Math.Max(b.BackColor.B - 30, 0)), 90f))
                        g.FillPath(br, path);
                    // Glass shimmer top half
                    var topHalf = new Rectangle(0, 0, b.Width, b.Height / 2);
                    using (var br = new LinearGradientBrush(topHalf,
                        Color.FromArgb(50, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                        g.FillRectangle(br, topHalf);
                }
                // Draw text
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using (var br = new SolidBrush(b.ForeColor))
                    g.DrawString(b.Text, b.Font, br, new RectangleF(0, 0, b.Width, b.Height), sf);
            };
            return b;
        }

        private static Label MakeSep(int x, int y, int w)
        {
            return new Label { Location = new Point(x, y), Size = new Size(w, 1), BackColor = C_BORDER };
        }

        private void MakeSlider(Panel parent, string name, int y, Color accent, out TrackBar tr, out Label lbl)
        {
            parent.Controls.Add(MakeLabel(name, 20, y, C_TEXT2, 8f, FontStyle.Bold));
            lbl = MakeLabel("1.0×", 196, y, accent, 8.5f, FontStyle.Bold);
            parent.Controls.Add(lbl);
            var theLbl = lbl;
            tr = new TrackBar
            {
                Minimum = 0, Maximum = 30, Value = 10,
                Location = new Point(16, y + 16), Width = 220,
                TickFrequency = 5, LargeChange = 5, SmallChange = 1,
                BackColor = C_BG2
            };
            tr.ValueChanged += (s, e) => theLbl.Text = (((TrackBar)s).Value / 10f).ToString("0.0") + "×";
            parent.Controls.Add(tr);
        }

        private static void MakeRound(Control c, int radius)
        {
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, c.Width, c.Height);
            c.Region = new Region(path);
        }

        private static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
