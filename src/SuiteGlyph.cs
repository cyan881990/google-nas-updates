using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace GoogleNas {
 public static class SuiteGlyph {
  public static Bitmap Image(string name,int size,Color color){
   var bmp=new Bitmap(size,size);using(var g=Graphics.FromImage(bmp)){g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/24f,size/24f);using(var pen=new Pen(color,1.7f)){pen.StartCap=pen.EndCap=LineCap.Round;
    if(name=="plus"){g.DrawLine(pen,12,4,12,20);g.DrawLine(pen,4,12,20,12);}
    else if(name=="refresh"){g.DrawArc(pen,4,4,16,16,35,285);g.DrawLines(pen,new[]{new PointF(20,3),new PointF(20,9),new PointF(14,9)});}
    else if(name=="play"){using(var fill=new SolidBrush(color))g.FillPolygon(fill,new[]{new PointF(7,4),new PointF(20,12),new PointF(7,20)});}
    else if(name=="stop"){using(var fill=new SolidBrush(color))g.FillRectangle(fill,5,5,14,14);}
    else if(name=="trash"){g.DrawLine(pen,4,6,20,6);g.DrawLines(pen,new[]{new PointF(7,6),new PointF(8,21),new PointF(16,21),new PointF(17,6)});g.DrawLines(pen,new[]{new PointF(9,5),new PointF(9,3),new PointF(15,3),new PointF(15,5)});g.DrawLine(pen,11,10,11,17);g.DrawLine(pen,14,10,14,17);}
    else if(name=="folder"){g.DrawLines(pen,new[]{new PointF(3,8),new PointF(3,4),new PointF(9,4),new PointF(12,7),new PointF(21,7),new PointF(21,20),new PointF(3,20),new PointF(3,8),new PointF(20,8)});}
    else if(name=="cloud"||name=="client"){g.DrawArc(pen,2,8,10,10,90,180);g.DrawArc(pen,7,3,12,14,180,210);g.DrawArc(pen,14,9,8,9,270,180);g.DrawLine(pen,6,18,18,18);}
    else if(name=="user"){g.DrawEllipse(pen,8,3,8,8);g.DrawArc(pen,4,12,16,15,180,180);}
    else if(name=="network"){g.DrawEllipse(pen,9,2,6,6);g.DrawRectangle(pen,2,17,6,5);g.DrawRectangle(pen,16,17,6,5);g.DrawLine(pen,12,8,12,13);g.DrawLine(pen,5,13,19,13);g.DrawLine(pen,5,13,5,17);g.DrawLine(pen,19,13,19,17);}
    else if(name=="settings"){for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,12+(float)Math.Cos(a)*8,12+(float)Math.Sin(a)*8,12+(float)Math.Cos(a)*10,12+(float)Math.Sin(a)*10);}g.DrawEllipse(pen,5,5,14,14);g.DrawEllipse(pen,9,9,6,6);}
    else if(name=="activity"){g.DrawLines(pen,new[]{new PointF(2,13),new PointF(7,13),new PointF(10,4),new PointF(14,20),new PointF(17,11),new PointF(22,11)});}
    else{g.DrawRectangle(pen,3,4,18,16);g.DrawLine(pen,3,13,21,13);g.DrawLine(pen,6,17,8,17);g.DrawLine(pen,11,17,13,17);}
   }}return bmp;
  }
  public static Bitmap Provider(string id,int size){
   string[] ids={"smb","ftp","ftps","sftp","webdav","s3","b2","dropbox","onedrive","drive","azureblob","azurefiles","gcs"};
   string[] codes={"NAS","FTP","TLS","SSH","DAV","S3","B2","DBX","OD","G","AZ","AF","GCS"};
   Color[] colors={Color.FromArgb(76,155,231),Color.FromArgb(76,175,200),Color.FromArgb(80,176,143),Color.FromArgb(218,164,73),Color.FromArgb(155,127,218),Color.FromArgb(224,156,64),Color.FromArgb(216,101,117),Color.FromArgb(93,139,232),Color.FromArgb(73,163,220),Color.FromArgb(83,177,134),Color.FromArgb(72,150,220),Color.FromArgb(65,169,184),Color.FromArgb(190,151,77)};
   int index=Array.IndexOf(ids,id);if(index<0)index=0;var bmp=new Bitmap(size,size);
   using(var g=Graphics.FromImage(bmp)){g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/64f,size/64f);using(var shape=MacButton.Round(new RectangleF(1,1,62,62),12))using(var fill=new SolidBrush(Color.FromArgb(43,colors[index])))g.FillPath(fill,shape);
    using(var icon=Image(index>4?"cloud":"drive",31,colors[index]))g.DrawImage(icon,16,5,31,31);
    using(var font=new Font("Segoe UI",9,FontStyle.Bold,GraphicsUnit.Pixel))using(var brush=new SolidBrush(colors[index]))using(var format=new StringFormat{Alignment=StringAlignment.Center})g.DrawString(codes[index],font,brush,new RectangleF(1,43,62,17),format);
   }return bmp;
  }
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
  public static Bitmap Render(bool server,int size){
   var bmp=new Bitmap(size,size);using(var g=Graphics.FromImage(bmp)){g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/64f,size/64f);
    using(var shape=MacButton.Round(new RectangleF(1,1,62,62),12))using(var bg=new SolidBrush(Color.FromArgb(29,36,47)))g.FillPath(bg,shape);
    using(var cyan=new SolidBrush(Color.FromArgb(66,191,218)))g.FillRectangle(cyan,13,13,11,38);
    using(var blue=new SolidBrush(Color.FromArgb(62,153,245)))g.FillRectangle(blue,24,13,27,11);
    using(var blue=new SolidBrush(Color.FromArgb(43,118,191)))g.FillRectangle(blue,24,40,27,11);
    using(var edge=new Pen(Color.FromArgb(75,113,148),1.2f))using(var shape=MacButton.Round(new RectangleF(1,1,62,62),12))g.DrawPath(edge,shape);
   }return bmp;
  }
  public static Icon Icon(bool server){using(var bmp=Render(server,64)){var handle=bmp.GetHicon();try{return (Icon)System.Drawing.Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}}}
 }
}