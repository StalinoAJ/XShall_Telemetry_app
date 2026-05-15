using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SHALLControl
{
    public partial class MainForm
    {
        // ── Sidebar ──────────────────────────────────────────────────────
        private void PaintSidebar(object sender, PaintEventArgs e)
        {
            var p=(Panel)sender; var g=e.Graphics;
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var br=new LinearGradientBrush(p.ClientRectangle,C_BG2,C_BG,180f))
                g.FillRectangle(br,p.ClientRectangle);
            // right edge accent line
            using(var pen=new Pen(Color.FromArgb(55,C_ACCENT),1))
                g.DrawLine(pen,p.Width-1,0,p.Width-1,p.Height);
        }

        private void PaintSidebarLogo(object sender, PaintEventArgs e)
        {
            var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            var lr=new Rectangle(18,22,46,46);
            using(var br=new LinearGradientBrush(lr,C_ACCENT,C_ACCENT2,135f)) g.FillEllipse(br,lr);
            using(var pen=new Pen(Color.FromArgb(60,C_ACCENT),2)) g.DrawEllipse(pen,new Rectangle(lr.X-3,lr.Y-3,lr.Width+6,lr.Height+6));
            using(var f=new Font("Segoe UI",18f,FontStyle.Bold)) using(var br=new SolidBrush(C_BG))
            { var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}; g.DrawString("S",f,br,lr,sf); }
            using(var f=new Font("Segoe UI",13f,FontStyle.Bold)) using(var br=new SolidBrush(C_TEXT)) g.DrawString("SHALL XR",f,br,76,26);
            using(var f=new Font("Segoe UI",8f)) using(var br=new SolidBrush(C_TEXT2)) g.DrawString("SEAT CONTROLLER",f,br,78,48);
        }

        // ── Game list row ────────────────────────────────────────────────
        private void PaintGameCard(Graphics g, Panel card, int idx)
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            bool sel=idx==_selGame;
            var rect=new Rectangle(0,0,card.Width-1,card.Height-1);
            Color gc=GCOLORS[idx];

            using(var path=RoundRect(rect,10))
            {
                if(sel)
                {
                    using(var br=new SolidBrush(Color.FromArgb(30,gc))) g.FillPath(br,path);
                    using(var br=new SolidBrush(Color.FromArgb(12,255,255,255))) g.FillPath(br,path);
                    using(var pen=new Pen(Color.FromArgb(160,gc),1.5f)) g.DrawPath(pen,path);
                }
                else
                {
                    using(var br=new SolidBrush(Color.FromArgb(14,255,255,255))) g.FillPath(br,path);
                    using(var pen=new Pen(Color.FromArgb(28,255,255,255),1)) g.DrawPath(pen,path);
                }
            }
            // left accent bar
            using(var br=new SolidBrush(sel?gc:Color.FromArgb(55,gc)))
                g.FillRectangle(br,new Rectangle(0,10,4,card.Height-20));

            // icon
            if(_gameImages[idx]==null)
                using(var f=new Font("Segoe UI Emoji",20f)) using(var br=new SolidBrush(sel?Color.FromArgb(230,gc):Color.FromArgb(130,gc)))
                    g.DrawString(ICONS[idx],f,br,12,16);

            // name
            using(var f=new Font("Segoe UI",10.5f,sel?FontStyle.Bold:FontStyle.Regular))
            using(var br=new SolidBrush(sel?C_TEXT:Color.FromArgb(170,200,175)))
                g.DrawString(NAMES[idx],f,br,56,12);

            // protocol
            using(var f=new Font("Segoe UI",8.5f)) using(var br=new SolidBrush(C_TEXT2))
                g.DrawString(PROTOS[idx],f,br,58,36);

            // path indicator
            if(!string.IsNullOrEmpty(_customGamePaths[idx]))
                using(var f=new Font("Segoe UI",8f)) using(var br=new SolidBrush(C_GREEN))
                    g.DrawString("● path",f,br,58,52);

            // chevron
            if(sel) using(var f=new Font("Segoe UI",14f,FontStyle.Bold)) using(var br=new SolidBrush(Color.FromArgb(200,gc)))
                g.DrawString("›",f,br,card.Width-24,22);
        }

        // ── Hero Panel ───────────────────────────────────────────────────
        private void PaintHeroPanel(Graphics g, Panel p)
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            var rect=new Rectangle(0,0,p.Width-1,p.Height-1);

            if(_selGame<0)
            {
                using(var path=RoundRect(rect,16))
                {
                    using(var br=new SolidBrush(C_CARD)) g.FillPath(br,path);
                    using(var br=new SolidBrush(Color.FromArgb(10,255,255,255))) g.FillPath(br,path);
                    using(var pen=new Pen(C_BORDER,1)) g.DrawPath(pen,path);
                }
                var sf2=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
                using(var f=new Font("Segoe UI",13f)) using(var br=new SolidBrush(C_TEXT2))
                    g.DrawString("← Select a game to begin",f,br,new RectangleF(0,0,p.Width*0.7f,p.Height),sf2);
                return;
            }

            Color gc=GCOLORS[_selGame];
            using(var path=RoundRect(rect,16))
            {
                using(var br=new System.Drawing.Drawing2D.LinearGradientBrush(rect,C_CARD,C_BG2,135f)) g.FillPath(br,path);
                using(var br=new SolidBrush(Color.FromArgb(15,gc))) g.FillPath(br,path);
                
                // full-height shimmer (prevents harsh line)
                using(var br=new System.Drawing.Drawing2D.LinearGradientBrush(rect,Color.FromArgb(20,255,255,255),Color.Transparent,90f)) 
                    g.FillPath(br,path);
                    
                using(var pen=new Pen(Color.FromArgb(80,gc),1.5f)) g.DrawPath(pen,path);
                
                // bottom accent bar
                using(var br=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0,rect.Bottom-4,rect.Width,4),Color.FromArgb(200,gc),Color.FromArgb(0,gc),0f))
                    g.FillRectangle(br,rect.Left,rect.Bottom-4,rect.Width,4);
            }

            // Big emoji
            using(var f=new Font("Segoe UI Emoji",48f)) using(var br=new SolidBrush(Color.FromArgb(200,gc)))
                g.DrawString(ICONS[_selGame],f,br,20,16);

            // Game name
            using(var f=new Font("Segoe UI",24f,FontStyle.Bold)) using(var br=new SolidBrush(C_TEXT))
                g.DrawString(NAMES[_selGame],f,br,110,16);

            // Protocol chip
            var chip=new Rectangle(114,64,130,28);
            using(var path=RoundRect(chip,14)) using(var br=new SolidBrush(Color.FromArgb(60,gc))) g.FillPath(br,path);
            using(var f=new Font("Segoe UI",9.5f,FontStyle.Bold)) using(var br=new SolidBrush(gc))
                g.DrawString(PROTOS[_selGame],f,br,chip.X+10,chip.Y+5);

            // Active badge
            if(_active)
                using(var f=new Font("Segoe UI",9f,FontStyle.Bold)) using(var br=new SolidBrush(C_GREEN))
                    g.DrawString("● LIVE",f,br,256,70);
        }

        // ── Gauges ───────────────────────────────────────────────────────
        private void PaintGaugesWrap(object sender, PaintEventArgs e)
        {
            var p=(Panel)sender; var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            int pad=6, gw=(p.Width-pad*4)/3;
            Color[] acc={Color.FromArgb(100,170,255),C_ACCENT2,Color.FromArgb(200,150,255)};
            string[] lbl={"PITCH","ROLL","YAW"};
            for(int i=0;i<3;i++)
            {
                var r=new Rectangle(pad+i*(gw+pad),pad,gw,p.Height-pad*2);
                using(var path=RoundRect(r,14))
                {
                    using(var br=new System.Drawing.Drawing2D.LinearGradientBrush(r,C_CARD,C_BG2,135f)) g.FillPath(br,path);
                    using(var br=new SolidBrush(Color.FromArgb(30,acc[i]))) g.FillPath(br,path);
                    
                    // full-height shimmer (prevents harsh line)
                    using(var br=new System.Drawing.Drawing2D.LinearGradientBrush(r,Color.FromArgb(14,255,255,255),Color.Transparent,90f)) 
                        g.FillPath(br,path);
                        
                    using(var pen=new Pen(Color.FromArgb(60,acc[i]),1)) g.DrawPath(pen,path);
                }
            }
        }

        private void PaintGauges(object sender, PaintEventArgs e)
        {
            var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            int w=_pbGauges.Width, h=_pbGauges.Height, pad=6;
            int gw=(w-pad*4)/3;
            DrawGauge(g,new Rectangle(pad,2,gw,h-4),"PITCH",_seat.LastPitch,Color.FromArgb(100,170,255));
            DrawGauge(g,new Rectangle(pad*2+gw,2,gw,h-4),"ROLL",_seat.LastRoll,C_ACCENT2);
            DrawGauge(g,new Rectangle(pad*3+gw*2,2,gw,h-4),"YAW",_seat.LastYaw,Color.FromArgb(200,150,255));
        }

        private void DrawGauge(Graphics g, Rectangle b, string label, int value, Color accent)
        {
            int cx=b.Left+b.Width/2, cy=b.Top+b.Height/2, r=Math.Min(b.Width,b.Height)/2-20;
            using(var pen=new Pen(Color.FromArgb(38,accent),20f)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawArc(pen,cx-r,cy-r,r*2,r*2,150,240);}
            using(var pen=new Pen(Color.FromArgb(55,C_BORDER),8f)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawArc(pen,cx-r,cy-r,r*2,r*2,150,240);}
            float pct=Math.Min(Math.Abs(value)/15f,1f), sw=pct*120f, st=value>=0?270f:270f-sw;
            if(sw>0.5f) using(var pen=new Pen(accent,8f)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawArc(pen,cx-r,cy-r,r*2,r*2,st,sw);}
            var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
            using(var f=new Font("Segoe UI",20f,FontStyle.Bold)) using(var br=new SolidBrush(C_TEXT))
                g.DrawString((value>=0?"+":"")+value+"°",f,br,new RectangleF(cx-r,cy-r,r*2,r*2),sf);
            using(var f=new Font("Segoe UI",9.5f,FontStyle.Bold)) using(var br=new SolidBrush(C_TEXT2))
                g.DrawString(label,f,br,new RectangleF(b.Left,b.Bottom-26,b.Width,20),sf);
        }

        // ── Tuning Panel ─────────────────────────────────────────────────
        private void PaintTuningPanel(object sender, PaintEventArgs e)
        {
            var p=(Panel)sender; var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            var r=new Rectangle(0,0,p.Width-1,p.Height-1);
            using(var path=RoundRect(r,14))
            {
                using(var br=new LinearGradientBrush(r,C_CARD,C_BG2,135f)) g.FillPath(br,path);
                using(var br=new SolidBrush(Color.FromArgb(10,255,255,255))) g.FillPath(br,path);
                using(var pen=new Pen(C_BORDER,1)) g.DrawPath(pen,path);
            }
        }

        // ── Telemetry Panel ──────────────────────────────────────────────
        private void PaintTelemetryPanel(object sender, PaintEventArgs e)
        {
            var p=(Panel)sender; var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            var r=new Rectangle(0,0,p.Width-1,p.Height-1);
            using(var path=RoundRect(r,14))
            {
                using(var br=new LinearGradientBrush(r,C_CARD,C_BG2,135f)) g.FillPath(br,path);
                using(var br=new SolidBrush(Color.FromArgb(10,255,255,255))) g.FillPath(br,path);
                using(var pen=new Pen(C_BORDER,1)) g.DrawPath(pen,path);
            }
            // divider
            using(var pen=new Pen(Color.FromArgb(40,C_BORDER),1))
                g.DrawLine(pen,r.X+14,r.Y+130,r.Right-14,r.Y+130);
            // status badge
            string badge=_active?"● LIVE":"○ IDLE";
            Color bc=_active?C_GREEN:C_TEXT2;
            using(var f=new Font("Segoe UI",7.5f,FontStyle.Bold)) using(var br=new SolidBrush(bc))
            { var sz=g.MeasureString(badge,f); g.DrawString(badge,f,br,r.Right-sz.Width-12,r.Y+10); }
            
            // Draw Telemetry Values manually to prevent WinForms Label flicker
            if (_latest == null) return;
            int x=140, y=42, sp=34;
            void DrawV(string txt, Color c, ref int yy) {
                using(var f=new Font("Segoe UI",11f,FontStyle.Bold)) using(var br=new SolidBrush(c))
                    g.DrawString(txt,f,br,x,yy);
                yy+=sp;
            }
            
            DrawV(_latest.Pitch.ToString("+0.000;-0.000")+"°", Color.FromArgb(100,170,255), ref y);
            DrawV(_latest.Roll.ToString("+0.000;-0.000")+"°", C_ACCENT2, ref y);
            DrawV(_latest.Yaw.ToString("+0.000;-0.000")+"°", Color.FromArgb(200,150,255), ref y);
            DrawV(_latest.Speed.ToString("0.0")+" km/h", C_TEXT, ref y); y+=6;
            DrawV(_latest.Surge.ToString("+0.000;-0.000")+" G", Color.FromArgb(180,130,255), ref y);
            DrawV(_latest.Sway.ToString("+0.000;-0.000")+" G", Color.FromArgb(255,180,100), ref y);
            DrawV(_latest.Heave.ToString("+0.000;-0.000")+" G", Color.FromArgb(100,220,200), ref y);
            DrawV(_latest.IsValid?"● YES":"○ NO", _latest.IsValid?C_GREEN:C_RED, ref y);
        }

        // ── Helpers ──────────────────────────────────────────────────────
        private void SetSeatStatus(bool ok)
        {
            _statusDot.BackColor=ok?C_GREEN:C_RED;
            _statusLbl.ForeColor=ok?C_GREEN:C_RED;
            _statusLbl.Text=ok?"CONNECTED":"DISCONNECTED";
            MakeRound(_statusDot,5);
        }

        private void Log(string msg)
        {
            if(_lblLog.InvokeRequired){_lblLog.BeginInvoke((Action)(()=>Log(msg)));return;}
            _lblLog.Text=msg;
        }

        private static Label Lbl(string t,int x,int y,Color c,float sz,FontStyle fs)
            =>new Label{Text=t,Location=new Point(x,y),ForeColor=c,Font=new Font("Segoe UI",sz,fs),AutoSize=true,BackColor=Color.Transparent};

        // Keep MakeLabel alias for BuildHelpPanel references
        private static Label MakeLabel(string t,int x,int y,Color c,float sz,FontStyle fs) => Lbl(t,x,y,c,sz,fs);

        private Button MakeBtn(string text, Rectangle r, Color bg)
        {
            var b=new Button{Text=text,Location=r.Location,Size=r.Size,BackColor=bg,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,Font=new Font("Segoe UI",10.5f,FontStyle.Bold)};
            b.FlatAppearance.BorderSize=0;
            b.FlatAppearance.MouseOverBackColor=Color.Transparent;
            b.FlatAppearance.MouseDownBackColor=Color.Transparent;
            
            int rad = 6; // Windows 11 Fluent 6px rounded corners
            b.Region=new Region(RoundRect(new Rectangle(0,0,r.Width,r.Height),rad));
            
            bool isDown = false;
            bool isHover = false;
            b.MouseDown += (s,e) => { isDown = true; b.Invalidate(); };
            b.MouseUp += (s,e) => { isDown = false; b.Invalidate(); };
            b.MouseEnter += (s,e) => { isHover = true; b.Invalidate(); };
            b.MouseLeave += (s,e) => { isHover = false; isDown = false; b.Invalidate(); };

            b.Paint+=(s,e)=>
            {
                var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
                var rr=new Rectangle(0,0,b.Width-1,b.Height-1);
                using(var path=RoundRect(rr,rad))
                {
                    // Calculate state color
                    Color baseCol = bg;
                    if (isDown) {
                        baseCol = Color.FromArgb(Math.Max(bg.R-20,0),Math.Max(bg.G-20,0),Math.Max(bg.B-20,0));
                    } else if (isHover) {
                        baseCol = Color.FromArgb(Math.Min(bg.R+15,255),Math.Min(bg.G+15,255),Math.Min(bg.B+15,255));
                    }
                    
                    // Solid fill
                    using(var br=new SolidBrush(baseCol)) g.FillPath(br,path);
                        
                    // Win11 subtle elevation (Inner top light, outer dark border)
                    if (!isDown) {
                        var innerRect = new Rectangle(1, 1, b.Width-3, b.Height-3);
                        using(var innerPath = RoundRect(innerRect, rad-1)) {
                            using(var br = new System.Drawing.Drawing2D.LinearGradientBrush(innerRect, Color.FromArgb(45, 255,255,255), Color.Transparent, 90f)) {
                                using(var pen = new Pen(br, 1f)) g.DrawPath(pen, innerPath);
                            }
                        }
                        using(var pen=new Pen(Color.FromArgb(60, 0,0,0), 1f)) g.DrawPath(pen,path);
                    } else {
                        // Pressed state shadow
                        using(var pen=new Pen(Color.FromArgb(100, 0,0,0), 1f)) g.DrawPath(pen,path);
                    }
                }
                
                var tr=new Rectangle(0,isDown?1:0,b.Width,b.Height);
                TextRenderer.DrawText(g,b.Text,b.Font,tr,b.ForeColor,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            return b;
        }

        // Keep alias for MainFormLogic
        private Button MakeRoundedBtn(string t,Rectangle r,Color bg)=>MakeBtn(t,r,bg);

        private void MakeSlider(Panel parent,string name,int x,int y,Color accent,out TouchSlider tr,out Label lbl)
        {
            parent.Controls.Add(Lbl(name,x,y+14,C_TEXT2,9.5f,FontStyle.Bold));
            lbl=Lbl("1.0×",x+410,y+12,accent,11f,FontStyle.Bold); parent.Controls.Add(lbl);
            var L=lbl;
            tr=new TouchSlider{Minimum=0,Maximum=30,Value=10,Location=new Point(x+70,y),Width=330,Height=48,AccentColor=accent};
            tr.ValueChanged+=(s,e)=>L.Text=(((TouchSlider)s).Value/10f).ToString("0.0")+"×";
            parent.Controls.Add(tr);
        }

        private static void MakeRound(Control c,int radius)
            {var p=new GraphicsPath();p.AddEllipse(0,0,c.Width,c.Height);c.Region=new Region(p);}

        private static GraphicsPath RoundRect(Rectangle b,int r)
        {
            int d=r*2; var p=new GraphicsPath();
            p.AddArc(b.X,b.Y,d,d,180,90); p.AddArc(b.Right-d,b.Y,d,d,270,90);
            p.AddArc(b.Right-d,b.Bottom-d,d,d,0,90); p.AddArc(b.X,b.Bottom-d,d,d,90,90);
            p.CloseFigure(); return p;
        }

        private static Label MakeSep(int x,int y,int w)=>new Label{Location=new Point(x,y),Size=new Size(w,1),BackColor=C_BORDER};

        public class TouchSlider : Control
        {
            public int Minimum { get; set; } = 0;
            public int Maximum { get; set; } = 30;
            public int Value { get; set; } = 10;
            public Color AccentColor { get; set; } = Color.White;
            public event EventHandler ValueChanged;

            public TouchSlider() {
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                BackColor = C_CARD; // Seamlessly blends into the tuning panel!
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                int h = 16;
                var bgRect = new Rectangle(15, Height/2 - h/2, Width-30, h);
                
                // Glassmorphic Track Background (dark shadow inside)
                using(var path = GetRoundRect(bgRect, h/2)) {
                    using(var br = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillPath(br, path);
                    using(var pen = new Pen(Color.FromArgb(30, 255, 255, 255), 1.5f)) g.DrawPath(pen, path);
                }

                float pct = (Value - Minimum) / (float)(Maximum - Minimum);
                int fw = (int)((Width-30) * pct);
                if (fw > 0) {
                    if (fw < h) fw = h;
                    var fgRect = new Rectangle(15, Height/2 - h/2, fw, h);
                    using(var path = GetRoundRect(fgRect, h/2)) {
                        using(var br = new SolidBrush(AccentColor)) g.FillPath(br, path);
                        // Glassy Highlight
                        var glowRect = new Rectangle(fgRect.X, fgRect.Y, fgRect.Width, fgRect.Height/2);
                        if (glowRect.Height > 0 && glowRect.Width > 0) {
                            using(var hlBr = new System.Drawing.Drawing2D.LinearGradientBrush(fgRect, Color.FromArgb(120, 255,255,255), Color.Transparent, System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                                g.FillPath(hlBr, path);
                        }
                    }
                }

                int cx = 15 + fw;
                // Glassy Drop Shadow Thumb
                using(var br = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(br, cx - 14, Height/2 - 12, 32, 32);
                var thumbRect = new Rectangle(cx - 16, Height/2 - 16, 32, 32);
                using(var br = new SolidBrush(Color.White)) g.FillEllipse(br, thumbRect);
                using(var pen = new Pen(AccentColor, 3f)) g.DrawEllipse(pen, thumbRect);
            }

            private GraphicsPath GetRoundRect(Rectangle bounds, int radius)
            {
                int d = radius * 2;
                var path = new GraphicsPath();
                if (bounds.Width <= 0 || bounds.Height <= 0) return path;
                path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
                path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
                path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }

            private void UpdateVal(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) {
                    float pct = (e.X - 15) / (float)(Width - 30);
                    pct = Math.Max(0, Math.Min(1, pct));
                    int v = Minimum + (int)Math.Round(pct * (Maximum - Minimum));
                    if (v != Value) { Value = v; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
                }
            }
            protected override void OnMouseDown(MouseEventArgs e) => UpdateVal(e);
            protected override void OnMouseMove(MouseEventArgs e) => UpdateVal(e);
        }
    }
}
