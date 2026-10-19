using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GoogleNas {
    public static class Theme {
        [ThreadStatic] internal static bool LayoutScaling;
        public static readonly Color Navy = Color.FromArgb(18,31,52), Blue = Color.FromArgb(0,113,227), Ink = Color.FromArgb(29,29,31), Muted = Color.FromArgb(110,110,115), Background = Color.FromArgb(245,245,247), Sidebar=Color.FromArgb(235,235,240);
        public static Label Label(string text, float size, bool bold, Color color) { return new Label { Text=text, Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular), ForeColor=color, AutoSize=true, BackColor=Color.Transparent, UseMnemonic=false }; }
        public static Button Button(string text, bool primary) {
            var b = new MacButton { Text=text, AutoSize=true, MinimumSize=new Size(96,40), Padding=new Padding(10,4,10,4), FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand, BackColor=primary?Blue:Color.FromArgb(232,232,237), ForeColor=primary?Color.White:Ink, Font=new Font("Segoe UI",9.5f,FontStyle.Bold), Margin=new Padding(0,0,8,8), UseMnemonic=false };
            b.FlatAppearance.BorderColor=primary?Blue:Color.FromArgb(218,226,237); b.FlatAppearance.BorderSize=primary?0:1; return b;
        }
        public static Icon Icon() { return IconArtwork.Create(); }

    }
    public class Card : Panel {
        private bool fitting;
        public Card() { BackColor=Color.White; Padding=new Padding(22); Margin=new Padding(0,0,0,18); DoubleBuffered=true; }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); if(fitting || Theme.LayoutScaling) return; fitting=true; try { foreach(Control c in Controls) if(c.Dock==DockStyle.None && (c.Anchor&AnchorStyles.Right)!=0) c.Width=Math.Max(1,ClientSize.Width-c.Left-Padding.Right); } finally { fitting=false; } }
        private GraphicsPath Shape() { return MacButton.Round(new RectangleF(0.5f,0.5f,Math.Max(1,Width-1),Math.Max(1,Height-1)),12*DeviceDpi/96f); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var shape=Shape()) using(var brush=new SolidBrush(Color.White)) e.Graphics.FillPath(brush,shape); }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var shape=Shape()) using(var pen=new Pen(Color.FromArgb(229,229,234))) e.Graphics.DrawPath(pen,shape); }
    }
    public class MacButton : Button {
        private bool hover,pressed;
        public MacButton() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); }
        public static GraphicsPath Round(RectangleF r,float radius) { float d=Math.Max(1,Math.Min(radius*2,Math.Min(r.Width,r.Height))); var p=new GraphicsPath(); p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
        protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover=false; pressed=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed=true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed=false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);
            Color fill=Enabled?BackColor:Color.FromArgb(235,235,239); if(Enabled && (pressed || hover)) { float f=pressed?0.91f:0.96f; fill=Color.FromArgb((int)(fill.R*f),(int)(fill.G*f),(int)(fill.B*f)); }
            using(var shape=Round(new RectangleF(0.5f,0.5f,Width-1,Height-1),8*DeviceDpi/96f)) { using(var brush=new SolidBrush(fill)) e.Graphics.FillPath(brush,shape); if(Focused && ShowFocusCues) using(var pen=new Pen(Theme.Blue,2)) e.Graphics.DrawPath(pen,shape); }
            var bounds=new Rectangle(Padding.Left+2,Padding.Top,Math.Max(1,Width-Padding.Horizontal-4),Math.Max(1,Height-Padding.Vertical));
            var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix;
            flags|=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(e.Graphics,Text,Font,bounds,Enabled?ForeColor:Color.FromArgb(147,147,153),flags);
        }
    }
    public class InputSurface : Panel {
        private readonly Control input;
        public InputSurface(Control control) {
            input=control; DoubleBuffered=true; BackColor=Color.White; Padding=new Padding(10,5,10,5);
            var text=control as TextBox; if(text!=null) { text.BorderStyle=BorderStyle.None; text.AutoSize=false; }
            var number=control as NumericUpDown; if(number!=null) number.BorderStyle=BorderStyle.None;
            var combo=control as ComboBox; if(combo!=null) { combo.FlatStyle=FlatStyle.Flat; Padding=new Padding(5,3,5,3); }
            control.BackColor=Color.White; control.Dock=DockStyle.Fill; control.Margin=Padding.Empty; control.GotFocus+=(a,b)=>Invalidate(); control.LostFocus+=(a,b)=>Invalidate(); Controls.Add(control);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent==null?Color.White:Parent.BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var path=MacButton.Round(new RectangleF(0.5f,0.5f,Width-1,Height-1),7*DeviceDpi/96f)) { using(var brush=new SolidBrush(Color.White)) e.Graphics.FillPath(brush,path); using(var pen=new Pen(input.Focused?Theme.Blue:Color.FromArgb(210,210,215),input.Focused?1.5f:1)) e.Graphics.DrawPath(pen,path); } }
    }
}
