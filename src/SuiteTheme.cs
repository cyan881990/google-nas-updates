using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GoogleNas {
 public static class SuiteTheme {
  public static readonly Color Background=Color.FromArgb(29,32,39),Sidebar=Color.FromArgb(36,40,49),Surface=Color.FromArgb(43,48,58),Input=Color.FromArgb(33,37,45),Border=Color.FromArgb(64,71,85),Blue=Color.FromArgb(62,153,245),Ink=Color.FromArgb(235,239,246),Muted=Color.FromArgb(159,171,190),Navy=Color.FromArgb(25,29,36);
  public static Button Button(string text,bool primary){return new SuiteButton{Text=text,AutoSize=false,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,BackColor=primary?Blue:Surface,ForeColor=primary?Color.White:Ink,Font=new Font("Segoe UI",9.5f),UseMnemonic=false};}
 }
 public class SuiteCard : Panel {
  public SuiteCard(){DoubleBuffered=true;BackColor=SuiteTheme.Surface;Padding=new Padding(18);Margin=new Padding(0,0,0,18);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?SuiteTheme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=MacButton.Round(new RectangleF(.5f,.5f,Math.Max(1,Width-1),Math.Max(1,Height-1)),6*DeviceDpi/96f))using(var b=new SolidBrush(SuiteTheme.Surface))e.Graphics.FillPath(b,p);}
 }
 public class SuiteInputSurface : Panel {
  public SuiteInputSurface(Control input){DoubleBuffered=true;BackColor=SuiteTheme.Input;Padding=new Padding(10,6,10,6);input.ForeColor=SuiteTheme.Ink;input.BackColor=SuiteTheme.Input;var box=input as TextBox;if(box!=null){box.BorderStyle=BorderStyle.None;box.AutoSize=false;}var combo=input as ComboBox;if(combo!=null){combo.FlatStyle=FlatStyle.Flat;if(combo.DrawMode!=DrawMode.OwnerDrawFixed){combo.DrawMode=DrawMode.OwnerDrawFixed;combo.DrawItem+=(a,e)=>{using(var bg=new SolidBrush((e.State&DrawItemState.Selected)!=0?SuiteTheme.Sidebar:SuiteTheme.Input))e.Graphics.FillRectangle(bg,e.Bounds);string text=e.Index<0?combo.Text:Convert.ToString(combo.Items[e.Index]);TextRenderer.DrawText(e.Graphics,text,combo.Font,e.Bounds,SuiteTheme.Ink,TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);};}}input.Dock=DockStyle.Fill;Controls.Add(input);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=MacButton.Round(new RectangleF(.5f,.5f,Math.Max(1,Width-1),Math.Max(1,Height-1)),4*DeviceDpi/96f))using(var pen=new Pen(SuiteTheme.Border))e.Graphics.DrawPath(pen,p);}
 }
 public class SuiteButton : MacButton {
  bool hover;
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?SuiteTheme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color fill=!Enabled?SuiteTheme.Input:hover?ControlPaint.Light(BackColor,.12f):BackColor;using(var path=Round(new RectangleF(.5f,.5f,Width-1,Height-1),5*DeviceDpi/96f))using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);var rect=new Rectangle(Padding.Left+4,0,Math.Max(1,Width-Padding.Horizontal-8),Height);if(Image!=null){int size=Math.Min(20*DeviceDpi/96,Height-8),gap=8*DeviceDpi/96;e.Graphics.DrawImage(Image,new Rectangle(Padding.Left+4,(Height-size)/2,size,size));rect.X+=size+gap;rect.Width=Math.Max(1,rect.Width-size-gap);}TextRenderer.DrawText(e.Graphics,Text,Font,rect,Enabled?ForeColor:SuiteTheme.Muted,TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if(Focused&&ShowFocusCues){var focus=ClientRectangle;focus.Inflate(-3,-3);ControlPaint.DrawFocusRectangle(e.Graphics,focus,SuiteTheme.Ink,fill);}}
 }
 public class SuiteCaptionButton : Button {
  readonly int action;bool hover;
  public SuiteCaptionButton(int value){action=value;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);TabStop=false;Cursor=Cursors.Hand;}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(hover?(action==0?Color.FromArgb(192,65,75):SuiteTheme.Surface):Parent.BackColor);float s=DeviceDpi/96f,cx=Width/2f,cy=Height/2f;using(var pen=new Pen(SuiteTheme.Ink,1.2f*s)){if(action==0){e.Graphics.DrawLine(pen,cx-4*s,cy-4*s,cx+4*s,cy+4*s);e.Graphics.DrawLine(pen,cx+4*s,cy-4*s,cx-4*s,cy+4*s);}else if(action==1)e.Graphics.DrawLine(pen,cx-5*s,cy+3*s,cx+5*s,cy+3*s);else e.Graphics.DrawRectangle(pen,cx-4*s,cy-4*s,8*s,8*s);}}
 }
 public class SuitePageTab : MacButton {
  public bool Active;
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Active)using(var pen=new Pen(SuiteTheme.Blue,3*DeviceDpi/96f))e.Graphics.DrawLine(pen,8,Height-2,Width-8,Height-2);}
 }
 public class SuiteCheckBox : CheckBox {
  public SuiteCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);ForeColor=SuiteTheme.Ink;}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?SuiteTheme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96f;int size=(int)(13*s),y=Math.Max(0,(Height-size)/2);var box=new RectangleF(1,y,size,size);using(var p=MacButton.Round(box,2*s)){using(var fill=new SolidBrush(Checked?SuiteTheme.Blue:SuiteTheme.Input))e.Graphics.FillPath(fill,p);using(var pen=new Pen(SuiteTheme.Border))e.Graphics.DrawPath(pen,p);}if(Checked)using(var pen=new Pen(Color.White,1.5f*s))e.Graphics.DrawLines(pen,new[]{new PointF(3*s,y+6*s),new PointF(6*s,y+9*s),new PointF(11*s,y+3*s)});TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(size+(int)(8*s),0,Math.Max(1,Width-size-(int)(8*s)),Height),Enabled?SuiteTheme.Ink:SuiteTheme.Muted,TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
 }
}
