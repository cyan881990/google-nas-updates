using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace GoogleNas {
    public partial class MainForm {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        private Panel CreateWindowBar() {
            var bar=new Panel { Dock=DockStyle.Fill,BackColor=Theme.Background,Margin=Padding.Empty,Name="WindowBar" };
            var caption=Theme.Label("CYAN NAS",10,true,Theme.Muted); caption.AutoSize=false; caption.TextAlign=ContentAlignment.MiddleCenter; bar.Controls.Add(caption);
            Action align=()=>caption.SetBounds((bar.Width-(int)(220*UiScale))/2,0,(int)(220*UiScale),bar.Height); bar.SizeChanged+=(a,b)=>align(); align();
            MouseEventHandler drag=(a,e)=>{ if(e.Button!=MouseButtons.Left) return; if(e.Clicks==2) { ToggleWindowSize(); return; } ReleaseCapture(); SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero); };
            bar.MouseDown+=drag; caption.MouseDown+=drag;
            string[] names={"WindowClose","WindowMinimize","WindowMaximize"};
            string[] descriptions={"Đóng cửa sổ và tiếp tục chạy nền","Thu nhỏ cửa sổ","Phóng to / Khôi phục"};
            Color[] colors={Color.FromArgb(255,95,87),Color.FromArgb(254,188,46),Color.FromArgb(40,200,64)};
            for(int i=0;i<3;i++) { int action=i; var button=new WindowDot(i) { Name=names[i],AccessibleName=descriptions[i],BackColor=Theme.Background,DotColor=colors[i],Location=new Point(16+i*23,10),Size=new Size(23,24),TabStop=false,Cursor=Cursors.Hand };
                button.Click+=(a,b)=>{ if(action==0) Close(); else if(action==1) WindowState=FormWindowState.Minimized; else ToggleWindowSize(); }; bar.Controls.Add(button); button.BringToFront(); tips.SetToolTip(button,descriptions[i]); }
            return bar;
        }
        private void ToggleWindowSize() { if(WindowState==FormWindowState.Maximized) WindowState=FormWindowState.Normal; else { MaximizedBounds=Screen.FromControl(this).WorkingArea; WindowState=FormWindowState.Maximized; } }
        private void RoundWindow() { if(IsDisposed || Width<1 || Height<1) return; var old=Region; if(WindowState==FormWindowState.Maximized) Region=null; else using(var path=MacButton.Round(new RectangleF(0,0,Width,Height),14*UiScale)) Region=new Region(path); if(old!=null) old.Dispose(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if(FormBorderStyle==FormBorderStyle.None) RoundWindow(); }
        internal IntPtr WindowEdge(Point point) {
            if(WindowState!=FormWindowState.Normal) return IntPtr.Zero; int edge=Math.Max(5,(int)(6*UiScale));
            bool l=point.X<edge,r=point.X>=ClientSize.Width-edge,t=point.Y<edge,b=point.Y>=ClientSize.Height-edge;
            return new IntPtr(t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:0);
        }
    }
    public class WindowDot : Button {
        public Color DotColor; private readonly int symbol; private bool hover;
        public WindowDot(int action) { symbol=action; SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true); FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; }
        protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float size=Math.Min(Width,Height)*0.56f,x=(Width-size)/2,y=(Height-size)/2;
            using(var brush=new SolidBrush(DotColor)) e.Graphics.FillEllipse(brush,x,y,size,size);
            using(var pen=new Pen(Color.FromArgb(45,80,60,40),Math.Max(1,size/14))) e.Graphics.DrawEllipse(pen,x,y,size,size);
            if(hover || Focused) using(var pen=new Pen(Color.FromArgb(105,54,40),Math.Max(1,size/11))) { float p=size*0.3f; if(symbol==0) { e.Graphics.DrawLine(pen,x+p,y+p,x+size-p,y+size-p); e.Graphics.DrawLine(pen,x+size-p,y+p,x+p,y+size-p); } else { e.Graphics.DrawLine(pen,x+p,y+size/2,x+size-p,y+size/2); if(symbol==2) e.Graphics.DrawLine(pen,x+size/2,y+p,x+size/2,y+size-p); } }
        }
    }
}
