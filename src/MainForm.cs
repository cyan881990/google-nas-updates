using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace GoogleNas {
    public partial class MainForm : Form {
        private readonly Services service;
        private readonly NasService nas;
        private readonly DriveProbe driveProbe=new DriveProbe();
        private TextBox nasFolder=new TextBox(), nasShare=new TextBox(), nasUser=new TextBox(), nasPassword=new TextBox(), remoteShare=new TextBox(), remoteUser=new TextBox(), remotePassword=new TextBox();
        private ComboBox nasHost=new ComboBox(), nasLetter=new ComboBox(); private CheckBox nasReadOnly=new CheckBox(), nasCreateUser=new CheckBox();
        private Label nasNetwork, nasServer, nasConnection;
        private Settings settings;
        private string managedShare;
        private TextBox remote=new TextBox(), folder=new TextBox();
        private ComboBox letter=new ComboBox(); private NumericUpDown cache=new NumericUpDown();
        private CheckBox startup=new CheckBox(), autoMount=new CheckBox(), autoUpdate=new CheckBox();
        private Label status, dependencies, driveLabel, queued, uploading, cacheUsed, pageTitle;
        private NotifyIcon tray; private Timer timer=new Timer();
        private readonly Timer networkTimer=new Timer { Interval=3000 };
        private bool networkEventsAttached;
        private bool busy, exiting, polling, wantMount, updatingHosts;
        private DateTime nextRetry=DateTime.MinValue, nextUpdate=DateTime.UtcNow.AddHours(6);
        private List<Button> actionButtons=new List<Button>(), navigation=new List<Button>();
        private List<Panel> pages=new List<Panel>(); private Panel pageHost;
        public MainForm(bool hidden) : this(hidden,new Services()) { }
        public MainForm(bool hidden, Services backend, bool previewOnly=false) {
            service=backend; settings=service.Load(); nas=new NasService(service.Home); nasPreview=previewOnly;
            SuspendLayout(); AutoScaleMode=AutoScaleMode.None;
            Text="CYAN NAS"; FormBorderStyle=FormBorderStyle.None; Icon=Theme.Icon(); Font=new Font("Segoe UI",10); BackColor=Theme.Background;
            Size=new Size(1320,900); MinimumSize=new Size(980,680); StartPosition=FormStartPosition.CenterScreen;
            remote.Text=settings.Remote; folder.Text=settings.Folder;
            letter.DropDownStyle=ComboBoxStyle.DropDownList; for(char c='D';c<='Z';c++) letter.Items.Add(c.ToString()); letter.SelectedItem=settings.Letter;
            cache.Minimum=1; cache.Maximum=2048; cache.Value=settings.CacheGB;
            startup.Text="Khởi chạy cùng Windows"; startup.Checked=service.StartupEnabled();
            autoMount.Text="Tự gắn ổ khi mở CYAN NAS"; autoMount.Checked=settings.AutoMount;
            autoUpdate.Text="Tự kiểm tra cập nhật mỗi 6 giờ"; autoUpdate.Checked=settings.AutoUpdate;
            var values=nas.Load(); managedShare=values.Share; nasFolder.Text=values.Folder; nasShare.Text=values.Share; nasUser.Text=values.User; nasPassword.UseSystemPasswordChar=true;
            nasHost.Text=values.Host; remoteShare.Text=values.RemoteShare; remoteUser.Text=values.RemoteUser; remotePassword.UseSystemPasswordChar=true;
            nasLetter.DropDownStyle=ComboBoxStyle.DropDownList; for(char c='D';c<='Z';c++) nasLetter.Items.Add(c.ToString()); nasLetter.SelectedItem=values.Letter;
            nasReadOnly.Text="Chỉ cho phép đọc file"; nasReadOnly.Checked=values.ReadOnly;
            nasCreateUser.Text="Tạo tài khoản mới cho NAS"; nasCreateUser.Checked=values.CreateUser;
            catalogTimer.Tick+=NasCatalogRefreshTick;
            BuildDesktop(); SelectPage(0); DependencyStatus(); UpdateDriveActions(); ResumeLayout(true); FillNasList(localNasList,nas.LocalShares(),true); FillNasList(remoteNasList,nas.Connections(),false); UpdateNasActions();
            if(previewOnly) return;
            NetworkChange.NetworkAddressChanged+=NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged+=NetworkAvailabilityChanged;
            SystemEvents.PowerModeChanged+=PowerModeChanged; networkEventsAttached=true;
            networkTimer.Tick+=async(a,b)=>{ networkTimer.Stop(); if(busy || polling) { networkTimer.Start(); return; } nas.NetworkChanged(); await Tick(); };
            var menu=new ContextMenuStrip(); menu.Items.Add("Mở CYAN NAS",null,(a,b)=>ShowWindow()); menu.Items.Add("Mở ổ đĩa",null,(a,b)=>{ Process.Start("explorer.exe",Core.Quote(settings.Letter+@":\")); });
            menu.Items.Add("Thoát an toàn",null,async(a,b)=>await Run(async()=>{ await service.Stop(); exiting=true; Close(); }));
            tray=new NotifyIcon { Icon=Icon, Text="CYAN NAS", Visible=true, ContextMenuStrip=menu }; tray.DoubleClick+=(a,b)=>ShowWindow();
            tray.MouseClick+=(a,b)=>{ if(b.Button==MouseButtons.Left) ShowWindow(); };
            FormClosing+=(a,b)=>{ if(!exiting && b.CloseReason==CloseReason.UserClosing) { b.Cancel=true; WindowState=FormWindowState.Minimized; } };
            Shown+=async(a,b)=>{ if(hidden) WindowState=FormWindowState.Minimized; polling=true; try { await RefreshNasCatalogs(); await nas.MaintainConnections(); await RefreshNasCatalogs(false); } catch(Exception e) { status.Text="NAS: "+e.Message; } finally { polling=false; } timer.Interval=10000; timer.Tick+=async(x,y)=>await Tick(); timer.Start(); wantMount=settings.AutoMount && File.Exists(service.Config); if(wantMount) await Run(StartMount); };
        }
        private NasProfile NasValues() {
            string host=nasHost.Text.Trim(); int separator=host.LastIndexOf('·'); if(separator>=0) host=host.Substring(separator+1).Trim();
            return new NasProfile { Folder=nasFolder.Text.Trim(),Share=nasShare.Text.Trim(),User=nasUser.Text.Trim(),Host=host,RemoteShare=remoteShare.Text.Trim(),Letter=Convert.ToString(nasLetter.SelectedItem),ReadOnly=nasReadOnly.Checked,CreateUser=nasCreateUser.Checked,RemoteUser=remoteUser.Text.Trim() };
        }
        private void NetworkAddressChanged(object sender,EventArgs e) { ScheduleNetworkRecovery(); }
        private void NetworkAvailabilityChanged(object sender,NetworkAvailabilityEventArgs e) { ScheduleNetworkRecovery(); }
        private void PowerModeChanged(object sender,PowerModeChangedEventArgs e) { if(e.Mode==PowerModes.Resume) ScheduleNetworkRecovery(); }
        private void ScheduleNetworkRecovery() {
            if(IsDisposed || Disposing || !IsHandleCreated || exiting) return;
            try { BeginInvoke((Action)(()=>{ if(IsDisposed || Disposing || exiting) return; networkTimer.Stop(); networkTimer.Start(); })); }
            catch(InvalidOperationException) { /* Window is closing. */ }
        }
        private async Task RefreshNas() {
            var state=await nas.Status();
            nasNetwork.Text=state.Ready?"Đã kết nối Tailscale · IP máy này: "+state.Address+"\nMáy từ xa cần đăng nhập cùng mạng Tailscale.":state.State=="NotInstalled"?"Chưa cài Tailscale. Bấm Cài Tailscale để tải và cài tự động.":"Tailscale: "+state.State+" · Bấm Đăng nhập để kết nối.";
            string selected=nasHost.Text; updatingHosts=true; try { nasHost.Items.Clear(); foreach(string peer in state.Peers) nasHost.Items.Add(peer); nasHost.Text=selected; } finally { updatingHosts=false; }
        }
        private async Task Run(Func<Task> action) {
            if(busy) return; busy=true; activity.Text="Đang xử lý…"; foreach(var b in actionButtons) b.Enabled=false;
            try { var deadline=DateTime.UtcNow.AddSeconds(15); while(polling) { if(IsDisposed || exiting) return; if(DateTime.UtcNow>=deadline) throw new Exception("Tác vụ nền chưa hoàn tất. Vui lòng thử lại sau ít giây."); await Task.Delay(100); } if(!IsDisposed && !exiting) await action(); } catch(Exception e) { status.Text=e.Message; if(tray!=null) tray.ShowBalloonTip(5000,"CYAN NAS",e.Message,ToolTipIcon.Warning); if(Visible) MessageBox.Show(this,e.Message,"CYAN NAS",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
            finally { busy=false; if(!IsDisposed) { foreach(var b in actionButtons) b.Enabled=true; UpdateDriveActions(); UpdateNasActions(); if(activity.Text=="Đang xử lý…") activity.Text="Sẵn sàng."; } }
        }
        private void Save() {
            var s=new Settings { Remote=remote.Text.Trim(), Letter=Convert.ToString(letter.SelectedItem), Folder=folder.Text.Trim(), CacheGB=(int)cache.Value, AutoMount=autoMount.Checked, AutoUpdate=autoUpdate.Checked, Repository=Core.EffectiveRepository(settings.Repository) }; Core.Validate(s);
            if(service.Running && (s.Remote!=settings.Remote || s.Letter!=settings.Letter || s.Folder!=settings.Folder || s.CacheGB!=settings.CacheGB)) throw new Exception("Ngắt ổ đĩa trước khi đổi kết nối, ký tự ổ, thư mục hoặc cache.");
            service.Save(s); service.SetStartup(startup.Checked); settings=s; wantMount=s.AutoMount && File.Exists(service.Config); driveLabel.Text=settings.Letter+":";
        }
        private void DependencyStatus() { dependencies.Text="Ổ đĩa: "+(File.Exists(service.Rclone)?"sẵn sàng":"cần sửa bản cài")+"   •   Driver: "+(service.WinFspInstalled?"sẵn sàng":"chưa sẵn sàng"); }
        private async Task StartMount() {
            nextRetry=DateTime.UtcNow.AddSeconds(30); DependencyStatus(); await Task.Run(()=>service.Start(settings)); status.Text="Đang kết nối ổ "+settings.Letter+": …";
            for(int i=0;i<20;i++) { await Task.Delay(1000); if(!service.Running) throw new Exception("Không gắn được ổ. Mở Thông tin → Xem nhật ký để kiểm tra."); if(await driveProbe.Ready(settings.Letter+@":\")) { status.Text="Đã kết nối · File của bạn đã sẵn sàng"; return; } } status.Text="Đang chờ ổ đĩa sẵn sàng. Kiểm tra kết nối mạng.";
        }
        private async Task Tick() {
            if(busy || polling) return; polling=true;
            try {
                try { await nas.MaintainConnections(); await RefreshNasCatalogs(false); } catch(Exception e) { if(nasConnection!=null) nasConnection.Text="Kết nối NAS: "+e.Message; }
                if(service.Running) { var stats=await service.Rc("vfs/stats"); var d=(Dictionary<string,object>)stats["diskCache"]; queued.Text=Core.Number(d,"uploadsQueued").ToString(); uploading.Text=Core.Number(d,"uploadsInProgress").ToString(); cacheUsed.Text=(Core.Number(d,"bytesUsed")/1073741824.0).ToString("0.0")+" GB"; status.Text=Core.Number(d,"erroredFiles")>0?"Có file upload lỗi. Kiểm tra nhật ký trước khi ngắt.":"Đã kết nối · Thay đổi được đồng bộ sau khi lưu"; }
                else if(wantMount && DateTime.UtcNow>=nextRetry) await StartMount();
                if((pages[1].Visible || pages[2].Visible || pages[3].Visible) && DateTime.UtcNow>=nextNasRefresh) { nextNasRefresh=DateTime.UtcNow.AddMinutes(1); await RefreshNasCatalogs(); if(nas.Installed) await RefreshNas(); }
                if(settings.AutoUpdate && settings.Repository!="" && DateTime.UtcNow>=nextUpdate) { nextUpdate=DateTime.UtcNow.AddHours(6); await Update(true); }
            } catch(Exception ex) { status.Text=ex.Message; nextRetry=DateTime.UtcNow.AddSeconds(30); } finally { polling=false; if(!IsDisposed) UpdateDriveActions(); }
        }
        private async Task Update(bool automatic) {
            status.Text="Đang kiểm tra bản cập nhật …"; var release=await Updater.Check(settings.Repository); if(release==null) { if(!automatic) MessageBox.Show(this,"Bạn đang dùng phiên bản mới nhất.","CYAN NAS"); status.Text="Bạn đang dùng phiên bản mới nhất."; return; }
            if(!automatic && MessageBox.Show(this,"Cài bản "+release.Tag+"? Đóng file trên ổ đĩa trước khi tiếp tục. Ứng dụng sẽ khởi động lại.","Cập nhật",MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
            status.Text="Đang tải và kiểm tra "+release.Tag; string stage=await Updater.Stage(release,service.Home); await service.Stop(); Updater.Launch(stage); exiting=true; Close();
        }
        public void PreviewPage(int index) { SelectPage(index); }
        public void ShowWindow() { ShowInTaskbar=true; Show(); WindowState=FormWindowState.Normal; BringToFront(); Activate(); }
        protected override void WndProc(ref Message message) {
            if(message.Msg==Program.TaskbarCreatedMessage && tray!=null) { tray.Visible=false; tray.Visible=true; }
            if(message.Msg==Program.ShowWindowMessage) { ShowWindow(); return; }
            base.WndProc(ref message);
            if(message.Msg==0x84 && FormBorderStyle==FormBorderStyle.None) { long raw=message.LParam.ToInt64(); var point=PointToClient(new Point((short)(raw&0xffff),(short)((raw>>16)&0xffff))); var edge=WindowEdge(point); if(edge!=IntPtr.Zero) message.Result=edge; }
        }
        protected override void OnLoad(EventArgs e) {
            if(!desktopScaleApplied) using(var graphics=CreateGraphics()) ApplyDesktopScale(graphics.DpiX/96f,false);
            base.OnLoad(e);
            Rectangle area=Screen.FromControl(this).WorkingArea;
            MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));
            Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
            foreach(Panel page in pages) FitCards(page);
        }
        private static void Open(string path) { Process.Start(new ProcessStartInfo(path) { UseShellExecute=true }); }
        protected override void Dispose(bool disposing) { if(disposing) { if(networkEventsAttached) { NetworkChange.NetworkAddressChanged-=NetworkAddressChanged; NetworkChange.NetworkAvailabilityChanged-=NetworkAvailabilityChanged; SystemEvents.PowerModeChanged-=PowerModeChanged; networkEventsAttached=false; } networkTimer.Dispose(); catalogTimer.Dispose(); tips.Dispose(); if(brandImage!=null) brandImage.Dispose(); timer.Dispose(); if(tray!=null) tray.Dispose(); if(Icon!=null) Icon.Dispose(); } base.Dispose(disposing); }
    }
}
