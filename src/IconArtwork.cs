using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace GoogleNas {
    public static class IconArtwork {
        private static GraphicsPath Round(RectangleF r,float d) { var p=new GraphicsPath(); p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
        public static Bitmap Render(int size) {
            var bitmap=new Bitmap(size,size); using(var g=Graphics.FromImage(bitmap)) {
                g.SmoothingMode=SmoothingMode.AntiAlias; g.PixelOffsetMode=PixelOffsetMode.HighQuality; g.Clear(Color.Transparent); g.ScaleTransform(size/256f,size/256f);
                using(var shape=Round(new RectangleF(6,6,244,244),100)) {
                    using(var gradient=new LinearGradientBrush(new PointF(25,12),new PointF(216,246),Color.FromArgb(35,173,250),Color.FromArgb(0,76,203))) g.FillPath(gradient,shape);
                    using(var border=new Pen(Color.FromArgb(90,255,255,255),2)) g.DrawPath(border,shape);
                }
                using(var white=new SolidBrush(Color.White)) using(var cloud=new GraphicsPath()) {
                    cloud.StartFigure(); cloud.AddBezier(77,115,49,113,53,78,80,77); cloud.AddBezier(80,77,87,47,128,43,145,67); cloud.AddBezier(145,67,173,55,198,79,188,101); cloud.AddBezier(188,101,187,112,176,118,164,118); cloud.AddLine(164,118,77,118); cloud.CloseFigure(); g.FillPath(white,cloud);
                    g.FillRectangle(white,122,115,12,23);
                    using(var body=Round(new RectangleF(54,133,148,78),26)) g.FillPath(white,body);
                }
                using(var ink=new Pen(Color.FromArgb(9,105,215),5)) { ink.StartCap=LineCap.Round; ink.EndCap=LineCap.Round; g.DrawLine(ink,68,172,188,172); g.DrawLine(ink,75,153,137,153); g.DrawLine(ink,75,191,137,191); }
                using(var led=new SolidBrush(Color.FromArgb(17,144,230))) { g.FillEllipse(led,173,147,11,11); g.FillEllipse(led,173,185,11,11); }
            } return bitmap;
        }
        public static Icon Create() { using(var bitmap=Render(64)) { var handle=bitmap.GetHicon(); try { return (Icon)Icon.FromHandle(handle).Clone(); } finally { DestroyIcon(handle); } } }
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    }
}
