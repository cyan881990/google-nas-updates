using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace GoogleNas {
 public class SuiteWindow : Form {
  public readonly Panel Body=new Panel();
  public Func<bool> CanDismiss=()=>true;
  readonly Panel chrome=new Panel();readonly Label caption=new Label();readonly Button close;
  public SuiteWindow(){
   FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
   AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Segoe UI",10);BackColor=Theme.Background;
   Icon=SuiteGlyph.Icon(SuiteIdentity.Server);Size=new Size(640,400);MinimumSize=new Size(460,300);KeyPreview=true;
   chrome.Dock=DockStyle.Top;chrome.Height=48;chrome.BackColor=Color.FromArgb(250,251,253);Controls.Add(chrome);
   caption.Dock=DockStyle.Fill;caption.TextAlign=ContentAlignment.MiddleCenter;caption.Font=new Font("Segoe UI",9,FontStyle.Bold);caption.ForeColor=Theme.Muted;caption.UseMnemonic=false;chrome.Controls.Add(caption);
   var closeArea=new Panel{Dock=DockStyle.Left,Width=60,BackColor=chrome.BackColor};chrome.Controls.Add(closeArea);
   var brand=new PictureBox{Dock=DockStyle.Right,Width=48,Padding=new Padding(12),SizeMode=PictureBoxSizeMode.CenterImage,Image=SuiteGlyph.Render(SuiteIdentity.Server,24)};chrome.Controls.Add(brand);Disposed+=(a,b)=>brand.Image.Dispose();caption.BringToFront();
   close=new WindowDot(0){Name="DialogClose",AccessibleName="Đóng cửa sổ",Location=new Point(14,12),Size=new Size(23,24),BackColor=chrome.BackColor,TabStop=false,DotColor=Color.FromArgb(255,95,87)};closeArea.Controls.Add(close);close.BringToFront();close.Click+=(a,b)=>Dismiss();
   caption.MouseDown+=Drag;chrome.MouseDown+=Drag;
   Body.Dock=DockStyle.Fill;Body.BackColor=Theme.Background;Body.Padding=new Padding(20);Controls.Add(Body);Body.BringToFront();
   TextChanged+=(a,b)=>caption.Text=Text;
  }
  void Drag(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
  internal void Dismiss(){if(CanDismiss!=null&&!CanDismiss())return;DialogResult=DialogResult.Cancel;Close();}
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==Keys.Escape){Dismiss();return true;}return base.ProcessCmdKey(ref msg,keyData);}
  protected override void OnFormClosing(FormClosingEventArgs e){if(CanDismiss!=null&&!CanDismiss())e.Cancel=true;base.OnFormClosing(e);}
  protected override void OnLoad(EventArgs e){base.OnLoad(e);var screen=Screen.FromControl(Owner??this).WorkingArea;MinimumSize=new Size(Math.Min(MinimumSize.Width,screen.Width),Math.Min(MinimumSize.Height,screen.Height));Size=new Size(Math.Min(Width,screen.Width),Math.Min(Height,screen.Height));if(Owner!=null){Location=new Point(Math.Max(screen.Left,Math.Min(screen.Right-Width,Owner.Left+(Owner.Width-Width)/2)),Math.Max(screen.Top,Math.Min(screen.Bottom-Height,Owner.Top+(Owner.Height-Height)/2)));Icon=Owner.Icon;}}
  protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(Width<1||Height<1)return;using(var path=MacButton.Round(new RectangleF(0,0,Width,Height),14*DeviceDpi/96f)){var previous=Region;Region=new Region(path);if(previous!=null)previous.Dispose();}}
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ReleaseCapture();
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
 }
 public class SuiteNoticeWindow : SuiteWindow {
  public SuiteNoticeWindow(string text,string title,MessageBoxButtons buttons,MessageBoxIcon icon){
   Text=title;Size=new Size(640,390);MinimumSize=new Size(460,320);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=Padding.Empty};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));Body.Controls.Add(layout);
   var card=new Card{Dock=DockStyle.Fill,Padding=new Padding(22),Margin=Padding.Empty};layout.Controls.Add(card,0,0);
   var titleLabel=SuiteForm.Label(title,16,true);titleLabel.AutoSize=false;titleLabel.Dock=DockStyle.Top;titleLabel.Height=46;
   var message=new TextBox{Name="NoticeText",TabStop=false,Text=text,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Font=new Font("Segoe UI",10)};
   card.Controls.Add(message);card.Controls.Add(titleLabel);message.BringToFront();
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(0,16,0,0)};layout.Controls.Add(actions,0,1);
   var okay=SuiteForm.Button(buttons==MessageBoxButtons.YesNo?"Xác nhận":"Đã hiểu",true);okay.Name="NoticeAccept";okay.DialogResult=buttons==MessageBoxButtons.YesNo?DialogResult.Yes:DialogResult.OK;actions.Controls.Add(okay);
   if(buttons==MessageBoxButtons.YesNo){var cancel=SuiteForm.Button("Hủy");cancel.Name="NoticeCancel";cancel.DialogResult=DialogResult.No;actions.Controls.Add(cancel);AcceptButton=cancel;CancelButton=cancel;}else{AcceptButton=okay;CancelButton=okay;}
   Shown+=(a,b)=>{message.Select(0,0);ActiveControl=(Control)AcceptButton;};
  }
 }
 public static class SuiteNotice {
  public static DialogResult Show(IWin32Window owner,string text,string title=null,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None){
   var form=owner as Form;using(var dialog=new SuiteNoticeWindow(text,title??(form==null?"CYAN NAS":form.Text),buttons,icon))return owner==null?dialog.ShowDialog():dialog.ShowDialog(owner);
  }
  public static DialogResult Show(string text,string title){return Show(null,text,title);}
 }
 public class SuitePromptWindow : SuiteWindow {
  public readonly ComboBox Choice=new ComboBox();
  public SuitePromptWindow(string title,string help,IEnumerable<string> choices,string value){
   Text=title;Size=new Size(680,500);MinimumSize=new Size(460,370);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));Body.Controls.Add(layout);
   var heading=SuiteForm.Label("Cấp quyền kết nối",18,true);heading.Dock=DockStyle.Fill;layout.Controls.Add(heading,0,0);
   var helpCard=new Card{Dock=DockStyle.Fill,Padding=new Padding(18),Margin=Padding.Empty};var helpText=new TextBox{Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.White,Dock=DockStyle.Fill,Text=help,ScrollBars=ScrollBars.Vertical};helpCard.Controls.Add(helpText);layout.Controls.Add(helpCard,0,1);
   Choice.DropDownStyle=ComboBoxStyle.DropDown;if(choices!=null)Choice.Items.AddRange(choices.Cast<object>().ToArray());Choice.Text=value??"";Choice.AccessibleName="Lựa chọn cấp quyền";var surface=new InputSurface(Choice){Dock=DockStyle.Fill,Margin=new Padding(0,14,0,8)};layout.Controls.Add(surface,0,2);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(0,16,0,0)};layout.Controls.Add(actions,0,3);var next=SuiteForm.Button("Tiếp tục",true);next.DialogResult=DialogResult.OK;var cancel=SuiteForm.Button("Hủy");cancel.DialogResult=DialogResult.Cancel;actions.Controls.Add(next);actions.Controls.Add(cancel);AcceptButton=next;CancelButton=cancel;
  }
 }
}
