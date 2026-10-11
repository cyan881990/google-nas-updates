using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GoogleNas {
    public static class Theme {
        public static readonly Color Navy = Color.FromArgb(18,31,52), Blue = Color.FromArgb(39,99,235), Ink = Color.FromArgb(25,40,63), Muted = Color.FromArgb(108,123,144), Background = Color.FromArgb(241,245,250);
        public static Label Label(string text, float size, bool bold, Color color) { return new Label { Text=text, Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular), ForeColor=color, AutoSize=true, BackColor=Color.Transparent }; }
        public static Button Button(string text, bool primary) {
            var b = new Button { Text=text, AutoSize=true, MinimumSize=new Size(100,42), Padding=new Padding(12,4,12,4), FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand, BackColor=primary?Blue:Color.White, ForeColor=primary?Color.White:Ink, Font=new Font("Segoe UI",10,FontStyle.Bold), Margin=new Padding(0,0,10,10) };
            b.FlatAppearance.BorderColor=primary?Blue:Color.FromArgb(218,226,237); b.FlatAppearance.BorderSize=primary?0:1; return b;
        }
        public static Icon Icon() {
            using(var bmp=new Bitmap(32,32)) {
                using(var g=Graphics.FromImage(bmp)) { g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(Color.Transparent); using(var brush=new SolidBrush(Blue)) g.FillEllipse(brush,1,1,30,30); using(var pen=new Pen(Color.White,2)) { g.DrawRectangle(pen,8,10,16,12); g.DrawLine(pen,8,17,24,17); g.DrawEllipse(pen,19,19,1,1); } }
                IntPtr handle=bmp.GetHicon(); try { return (Icon)System.Drawing.Icon.FromHandle(handle).Clone(); } finally { DestroyIcon(handle); }
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    }
    public class Card : Panel {
        public Card() { BackColor=Color.White; Padding=new Padding(22); Margin=new Padding(0,0,0,18); DoubleBuffered=true; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using(var p=new Pen(Color.FromArgb(223,231,240))) e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1); }
    }
}
