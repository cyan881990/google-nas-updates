using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace GoogleNas {
 public class CyanAccountWindow : SuiteWindow {
  readonly CyanSession session;readonly TextBox email=new TextBox(),password=new TextBox();readonly Label status=SuiteForm.Label("",10),details=SuiteForm.Label("",10);
  readonly Button login=SuiteForm.Button("Đăng nhập",true),verify=SuiteForm.Button("Kiểm tra license"),logout=SuiteForm.Button("Đăng xuất thiết bị");bool working;
  public CyanAccountWindow(CyanSession value){
   session=value;Text="Tài khoản CYANPRO · CYAN NAS";Size=new Size(720,700);MinimumSize=new Size(600,620);CanDismiss=()=>!working;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=Padding.Empty};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,65));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,210));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,66));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));Body.Controls.Add(layout);
   var title=SuiteForm.Label("Một tài khoản. Quản lý mọi thiết bị.",19,true);title.Dock=DockStyle.Fill;title.AutoSize=false;layout.Controls.Add(title,0,0);
   var card=new SuiteCard{Dock=DockStyle.Fill,Padding=new Padding(18),Margin=Padding.Empty};layout.Controls.Add(card,0,1);
   var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,78));foreach(int h in new[]{28,52,28,52})fields.RowStyles.Add(new RowStyle(SizeType.Absolute,h));card.Controls.Add(fields);
   fields.Controls.Add(SuiteForm.Label("Email tài khoản CYANPRO",10,true),0,0);fields.SetColumnSpan(fields.GetControlFromPosition(0,0),2);email.Name="CyanEmail";email.Text=session==null?"":session.Email;fields.Controls.Add(new SuiteInputSurface(email){Dock=DockStyle.Fill,Margin=new Padding(0,0,0,8)},0,1);fields.SetColumnSpan(fields.GetControlFromPosition(0,1),2);
   fields.Controls.Add(SuiteForm.Label("Mật khẩu",10,true),0,2);password.Name="CyanPassword";password.UseSystemPasswordChar=true;fields.Controls.Add(new SuiteInputSurface(password){Dock=DockStyle.Fill,Margin=new Padding(0,0,8,0)},0,3);var show=SuiteForm.Button("Hiện");show.Dock=DockStyle.Fill;show.Margin=Padding.Empty;show.Click+=(a,b)=>{password.UseSystemPasswordChar=!password.UseSystemPasswordChar;show.Text=password.UseSystemPasswordChar?"Hiện":"Ẩn";};fields.Controls.Add(show,1,3);
   var info=new SuiteCard{Dock=DockStyle.Fill,Padding=new Padding(18),Margin=new Padding(0,14,0,0)};details.AutoSize=false;details.Dock=DockStyle.Fill;details.ForeColor=SuiteTheme.Muted;info.Controls.Add(details);layout.Controls.Add(info,0,2);
   var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(0,16,0,8)};for(int i=0;i<3;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));int index=0;foreach(var button in new[]{login,verify,logout}){button.Dock=DockStyle.Fill;button.Margin=new Padding(index==0?0:5,0,index==2?0:5,0);actions.Controls.Add(button,index++,0);}layout.Controls.Add(actions,0,3);
   var links=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));Button account=SuiteForm.Button("Quản lý trên CYANPRO"),register=SuiteForm.Button("Tạo tài khoản");account.Dock=register.Dock=DockStyle.Fill;account.Click+=(a,b)=>Open("https://cyanpro.app");register.Click+=(a,b)=>Open("https://cyanpro.app/register");links.Controls.Add(account,0,0);links.Controls.Add(register,1,0);layout.Controls.Add(links,0,4);
   status.AutoSize=false;status.Dock=DockStyle.Bottom;status.Height=36;status.ForeColor=SuiteTheme.Muted;info.Controls.Add(status);details.BringToFront();
   login.Click+=async(a,b)=>await Work(async()=>{if(string.IsNullOrWhiteSpace(email.Text)||password.Text.Length==0)throw new Exception("Nhập email và mật khẩu CYANPRO.");try{await session.Login(email.Text,password.Text);}finally{password.Clear();}});
   verify.Click+=async(a,b)=>await Work(()=>session.Verify());logout.Click+=async(a,b)=>{if(SuiteNotice.Show(this,"Đăng xuất và trả lại suất thiết bị? Các ổ đang dùng được giữ để bạn có thể ngắt an toàn; kết nối mới yêu cầu đăng nhập.","Đăng xuất CYANPRO",MessageBoxButtons.YesNo)==DialogResult.Yes)await Work(()=>session.Logout());};AcceptButton=login;
   if(session!=null)session.Changed+=UpdateState;Disposed+=(a,b)=>{if(session!=null)session.Changed-=UpdateState;password.Clear();};UpdateState();
  }
  void UpdateState(){if(IsDisposed)return;details.Text=session==null?"Đăng nhập bằng tài khoản CYANPRO để quản lý license và thiết bị.\n\nMật khẩu không được lưu. Phiên đăng nhập được mã hóa theo tài khoản Windows.":session.Details;verify.Enabled=logout.Enabled=!working&&session!=null&&session.HasSession;status.Text=session==null?"Bản xem trước giao diện":session.Status;}
  async Task Work(Func<Task> work){if(working||session==null)return;working=true;login.Enabled=verify.Enabled=logout.Enabled=false;status.Text="Đang kết nối CYANPRO…";string error=null;try{await work();}catch(Exception e){error=e is CyanApiException||e is System.Net.WebException?e is CyanApiException?e.Message:"Không kết nối được CYANPRO. Kiểm tra Internet và thử lại.":e.Message;}finally{working=false;login.Enabled=true;UpdateState();if(error!=null)status.Text=error;}}
  static void Open(string url){Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
 }
}
