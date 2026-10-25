using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace GoogleNas {
 public class UnifiedSuiteForm : Form {
  internal readonly SuiteForm ServerView,ClientView;
  readonly SuitePageTab serverTab,clientTab;readonly Panel host=new Panel();readonly bool preview;
  readonly NotifyIcon tray;readonly System.Windows.Forms.Timer updates=new System.Windows.Forms.Timer{Interval=30000};
  bool exiting,closing,checking;DateTime nextUpdate=DateTime.UtcNow.AddMinutes(5);
  internal bool ServerSelected {get;private set;}
  public UnifiedSuiteForm(bool selectServer=true,bool hidden=false,bool previewOnly=false,string directory=null){
   preview=previewOnly;Text="CYAN NAS";Icon=SuiteGlyph.Icon(false);Font=new Font("Segoe UI",10);ForeColor=SuiteTheme.Ink;BackColor=SuiteTheme.Background;FormBorderStyle=FormBorderStyle.None;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Size=new Size(1240,900);MinimumSize=new Size(960,700);StartPosition=FormStartPosition.CenterScreen;
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Absolute,68));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
   var bar=new Panel{Dock=DockStyle.Fill,BackColor=SuiteTheme.Navy,Margin=Padding.Empty};root.Controls.Add(bar,0,0);
   var logo=new PictureBox{Image=SuiteGlyph.Render(false,26),Dock=DockStyle.Left,Width=50,SizeMode=PictureBoxSizeMode.CenterImage};bar.Controls.Add(logo);
   var title=SuiteForm.Label("CYAN NAS     ·     "+Core.Version,10,true);title.AutoSize=false;title.Dock=DockStyle.Fill;title.TextAlign=ContentAlignment.MiddleLeft;bar.Controls.Add(title);title.BringToFront();
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Right,Width=132,WrapContents=false,Margin=Padding.Empty};bar.Controls.Add(buttons);title.BringToFront();
   foreach(int action in new[]{1,2,0}){int value=action;var button=new SuiteCaptionButton(value){Size=new Size(44,42),Margin=Padding.Empty,AccessibleName=value==0?"Đóng cửa sổ, tiếp tục chạy nền":value==1?"Thu nhỏ":"Phóng to / khôi phục"};button.Click+=(a,b)=>{if(value==0)Close();else if(value==1)WindowState=FormWindowState.Minimized;else{MaximizedBounds=Screen.FromControl(this).WorkingArea;WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}};buttons.Controls.Add(button);}
   MouseEventHandler drag=(a,b)=>{if(b.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};title.MouseDown+=drag;bar.MouseDown+=drag;
   var roles=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=SuiteTheme.Sidebar,Padding=new Padding(20,6,20,0),Margin=Padding.Empty};roles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));roles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));root.Controls.Add(roles,0,1);
   serverTab=new SuitePageTab{Name="ServerTab",Text="SERVER  ·  Chia sẻ ổ cứng",Dock=DockStyle.Fill,Margin=new Padding(0,0,8,0),Font=new Font("Segoe UI",12,FontStyle.Bold),Image=SuiteGlyph.Image("drive",40,SuiteTheme.Blue),BackColor=SuiteTheme.Sidebar,ForeColor=SuiteTheme.Ink};
   clientTab=new SuitePageTab{Name="ClientTab",Text="CLIENT  ·  Kết nối NAS & cloud",Dock=DockStyle.Fill,Margin=new Padding(8,0,0,0),Font=serverTab.Font,Image=SuiteGlyph.Image("cloud",40,SuiteTheme.Blue),BackColor=SuiteTheme.Sidebar,ForeColor=SuiteTheme.Ink};roles.Controls.Add(serverTab,0,0);roles.Controls.Add(clientTab,1,0);
   host.Dock=DockStyle.Fill;host.Margin=Padding.Empty;root.Controls.Add(host,0,2);
   string basePath=directory??Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   ServerView=new SuiteForm(true,false,preview,Path.Combine(basePath,"CyanNasServer"),true){TopLevel=false,Dock=DockStyle.Fill};ClientView=new SuiteForm(false,false,preview,Path.Combine(basePath,"CyanNasClient"),true){TopLevel=false,Dock=DockStyle.Fill};
   ServerView.HostExit=()=>ExitSafely(ServerView);ClientView.HostExit=()=>ExitSafely(ClientView);host.Controls.Add(ServerView);host.Controls.Add(ClientView);
   serverTab.Click+=(a,b)=>SelectRole(true);clientTab.Click+=(a,b)=>SelectRole(false);
   Shown+=(a,b)=>{ServerView.Show();ClientView.Show();SelectRole(selectServer);if(hidden)WindowState=FormWindowState.Minimized;};
   FormClosing+=(a,b)=>{if(!exiting&&!preview&&b.CloseReason==CloseReason.UserClosing){b.Cancel=true;WindowState=FormWindowState.Minimized;}};
   if(!preview){new Services(SuiteIdentity.Home);if(!File.Exists(Path.Combine(SuiteIdentity.Home,"suite-preferences.json")))SuiteUpdate.SetAutomatic(SuiteIdentity.Home,SuiteUpdate.Automatic(Path.Combine(basePath,"CyanNasServer"))||SuiteUpdate.Automatic(Path.Combine(basePath,"CyanNasClient")));
    var menu=new ContextMenuStrip();menu.Items.Add("Mở CYAN NAS",null,(a,b)=>ShowWindow());menu.Items.Add("SERVER",null,(a,b)=>{SelectRole(true);ShowWindow();});menu.Items.Add("CLIENT",null,(a,b)=>{SelectRole(false);ShowWindow();});menu.Items.Add("Thoát an toàn",null,async(a,b)=>await RequestExit());tray=new NotifyIcon{Text="CYAN NAS",Icon=Icon,Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=(a,b)=>ShowWindow();tray.MouseClick+=(a,b)=>{if(b.Button==MouseButtons.Left)ShowWindow();};
    updates.Tick+=async(a,b)=>{if(checking||DateTime.UtcNow<nextUpdate||!SuiteUpdate.Automatic(SuiteIdentity.Home))return;checking=true;nextUpdate=DateTime.UtcNow.AddHours(6);try{if(await SuiteUpdate.Available())tray.ShowBalloonTip(5000,"CYAN NAS","Có phiên bản mới. Mở Cài đặt → Kiểm tra cập nhật.",ToolTipIcon.Info);}finally{checking=false;}};updates.Start();
   }
  }
  internal void SelectRole(bool server){ServerSelected=server;ServerView.Visible=server;ClientView.Visible=!server;(server?ServerView:ClientView).BringToFront();serverTab.Active=server;clientTab.Active=!server;serverTab.BackColor=server?SuiteTheme.Surface:SuiteTheme.Sidebar;clientTab.BackColor=!server?SuiteTheme.Surface:SuiteTheme.Sidebar;serverTab.ForeColor=server?SuiteTheme.Blue:SuiteTheme.Muted;clientTab.ForeColor=!server?SuiteTheme.Blue:SuiteTheme.Muted;serverTab.Invalidate();clientTab.Invalidate();if(!preview)(server?ServerView:ClientView).SyncHostPreferences();}
  public void ShowWindow(){Show();WindowState=FormWindowState.Normal;Activate();}
  async Task RequestExit(){try{await ExitSafely(null);}catch(Exception e){SuiteNotice.Show(this,e.Message,"Chưa thể thoát an toàn");}}
  internal async Task ExitSafely(SuiteForm caller){if(closing)return;if((ServerView.IsWorking&&caller!=ServerView)||(ClientView.IsWorking&&caller!=ClientView))throw new Exception("Đang xử lý thao tác NAS. Chờ hoàn tất rồi thoát an toàn.");closing=true;updates.Stop();try{await ClientView.StopForHost();await ServerView.StopForHost();exiting=true;Close();}catch{ClientView.ResumeForHost();ServerView.ResumeForHost();updates.Start();throw;}finally{closing=false;}}
  protected override void OnLoad(EventArgs e){base.OnLoad(e);var area=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));}
  protected override void WndProc(ref Message m){if(m.Msg==SuiteProgram.ShowMessage){if(m.WParam.ToInt32()==1)SelectRole(true);else if(m.WParam.ToInt32()==2)SelectRole(false);ShowWindow();return;}base.WndProc(ref m);if(m.Msg==0x84&&WindowState==FormWindowState.Normal){long raw=m.LParam.ToInt64();var p=PointToClient(new Point((short)(raw&65535),(short)((raw>>16)&65535)));int edge=Math.Max(6,6*DeviceDpi/96);bool l=p.X<edge,r=p.X>=ClientSize.Width-edge,t=p.Y<edge,b=p.Y>=ClientSize.Height-edge;if(l||r||t||b)m.Result=new IntPtr(t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:11);}}
  protected override void Dispose(bool disposing){if(disposing){updates.Dispose();if(tray!=null)tray.Dispose();}base.Dispose(disposing);}
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ReleaseCapture();
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr w,int m,IntPtr p,IntPtr l);
 }
}
