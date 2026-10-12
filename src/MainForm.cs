using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace GoogleNas {
    public class MainForm : Form {
        private readonly Services service;
        private readonly NasService nas;
        private TextBox nasFolder=new TextBox(), nasShare=new TextBox(), nasUser=new TextBox(), nasPassword=new TextBox(), remoteShare=new TextBox();
        private ComboBox nasHost=new ComboBox(), nasLetter=new ComboBox(); private CheckBox nasReadOnly=new CheckBox(), nasCreateUser=new CheckBox();
        private Label nasNetwork, nasServer, nasConnection;
        private Settings settings;
        private TextBox remote=new TextBox(), folder=new TextBox();
        private ComboBox letter=new ComboBox(); private NumericUpDown cache=new NumericUpDown();
        private CheckBox startup=new CheckBox(), autoMount=new CheckBox(), autoUpdate=new CheckBox();
        private Label status, dependencies, driveLabel, queued, uploading, cacheUsed, pageTitle;
        private NotifyIcon tray; private Timer timer=new Timer();
        private bool busy, exiting, polling, wantMount;
        private DateTime nextRetry=DateTime.MinValue, nextUpdate=DateTime.UtcNow.AddHours(6);
        private List<Button> actionButtons=new List<Button>(), navigation=new List<Button>();
        private List<Panel> pages=new List<Panel>(); private Panel pageHost;
        public MainForm(bool hidden) : this(hidden,new Services()) { }
        public MainForm(bool hidden, Services backend, bool previewOnly=false) {
            service=backend; settings=service.Load();
            nas=new NasService(service.Home);
            SuspendLayout();
            AutoScaleDimensions=new SizeF(96,96);
            Text="CYAN NAS"; Icon=Theme.Icon(); Font=new Font("Segoe UI",10); BackColor=Theme.Background; AutoScaleMode=AutoScaleMode.Dpi;
            Size=new Size(1120,840); MinimumSize=new Size(1050,800); StartPosition=FormStartPosition.CenterScreen;
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1, Margin=Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,220)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Controls.Add(layout);
            var side=new Panel { Dock=DockStyle.Fill, BackColor=Theme.Navy, Margin=Padding.Empty }; layout.Controls.Add(side,0,0);
            TextAt(side,"CYAN NAS",21,true,Color.White,22,32); TextAt(side,"YOUR CLOUD. YOUR DRIVE.",8,true,Color.FromArgb(133,160,196),24,77);
            string[] names={"Tổng quan","Cấu hình ổ đĩa","NAS từ xa","Cập nhật","Thông tin"};
            for(int i=0;i<names.Length;i++) {
                int index=i; var nav=new Button { Text=names[i], TextAlign=ContentAlignment.MiddleLeft, Padding=new Padding(18,0,0,0), FlatStyle=FlatStyle.Flat, ForeColor=Color.FromArgb(184,198,218), BackColor=Theme.Navy, Size=new Size(196,46), Location=new Point(12,142+i*54), Cursor=Cursors.Hand };
                nav.FlatAppearance.BorderSize=0; nav.Click+=(a,b)=>SelectPage(index); navigation.Add(nav); side.Controls.Add(nav);
            }
            var sideFooter=Theme.Label("DESKTOP EDITION\nv"+Core.Version+" · Windows x64",9,false,Color.FromArgb(133,160,196)); sideFooter.Dock=DockStyle.Bottom; sideFooter.AutoSize=false; sideFooter.Height=80; sideFooter.Padding=new Padding(24,16,0,0); side.Controls.Add(sideFooter);
            var content=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(28,26,28,20), ColumnCount=1, RowCount=3, Margin=Padding.Empty };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,84)); content.RowStyles.Add(new RowStyle(SizeType.Percent,100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute,40)); layout.Controls.Add(content,1,0);
            var header=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty };
            pageTitle=Theme.Label("Tổng quan",24,true,Theme.Ink); pageTitle.Margin=new Padding(0,0,0,4); header.Controls.Add(pageTitle);
            var subtitle=Theme.Label("Không gian làm việc của bạn, kết nối với Google Drive.",10,false,Theme.Muted); subtitle.Margin=Padding.Empty; header.Controls.Add(subtitle); content.Controls.Add(header,0,0);
            pageHost=new Panel { Dock=DockStyle.Fill, Margin=Padding.Empty }; content.Controls.Add(pageHost,0,1);
            var footer=Theme.Label("Đóng cửa sổ để thu nhỏ · Mở lại bằng taskbar, shortcut hoặc biểu tượng khay hệ thống",9,false,Theme.Muted); footer.Dock=DockStyle.Fill; footer.Padding=new Padding(0,14,0,0); content.Controls.Add(footer,0,2);
            remote.Text=settings.Remote; folder.Text=settings.Folder;
            letter.DropDownStyle=ComboBoxStyle.DropDownList; for(char c='D';c<='Z';c++) letter.Items.Add(c.ToString()); letter.SelectedItem=settings.Letter;
            cache.Minimum=1; cache.Maximum=2048; cache.Value=settings.CacheGB;
            startup.Text="Khởi chạy khi đăng nhập Windows"; startup.Checked=service.StartupEnabled(); autoMount.Text="Tự gắn ổ và kết nối lại khi cần"; autoMount.Checked=settings.AutoMount;
            autoUpdate.Text="Tự cập nhật bản ổn định (kiểm tra mỗi 6 giờ)"; autoUpdate.Checked=settings.AutoUpdate;
            BuildOverview(); BuildSettings(); BuildNas(); BuildUpdates(); BuildAbout(); SelectPage(0); DependencyStatus();
            status.TextChanged+=(a,b)=>{ footer.Text=status.Text; };
            ResumeLayout(true);
            if(previewOnly) return;
            var menu=new ContextMenuStrip(); menu.Items.Add("Mở CYAN NAS",null,(a,b)=>ShowWindow()); menu.Items.Add("Mở ổ đĩa",null,(a,b)=>{ if(Directory.Exists(settings.Letter+@":\")) Open(settings.Letter+@":\"); });
            menu.Items.Add("Thoát an toàn",null,async(a,b)=>await Run(async()=>{ await service.Stop(); exiting=true; Close(); }));
            tray=new NotifyIcon { Icon=Icon, Text="CYAN NAS", Visible=true, ContextMenuStrip=menu }; tray.DoubleClick+=(a,b)=>ShowWindow();
            tray.MouseClick+=(a,b)=>{ if(b.Button==MouseButtons.Left) ShowWindow(); };
            FormClosing+=(a,b)=>{ if(!exiting && b.CloseReason==CloseReason.UserClosing) { b.Cancel=true; WindowState=FormWindowState.Minimized; } };
            Shown+=async(a,b)=>{ if(hidden) WindowState=FormWindowState.Minimized; wantMount=settings.AutoMount && File.Exists(service.Config); if(wantMount) await Run(StartMount); timer.Interval=10000; timer.Tick+=async(x,y)=>await Tick(); timer.Start(); };
        }
        private FlowLayoutPanel Page() {
            var p=new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Margin=Padding.Empty, Visible=false }; pageHost.Controls.Add(p); pages.Add(p);
            p.SizeChanged+=(a,b)=>FitCards(p); return p;
        }
        private void FitCards(FlowLayoutPanel p) {
            // Preserve a readable logical width on small screens; the page can scroll.
            int minimum=(int)Math.Round(600*CurrentAutoScaleDimensions.Width/96);
            foreach(Control c in p.Controls) c.Width=Math.Max(minimum,p.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-3);
        }
        private Card Card(FlowLayoutPanel page,int height) { var c=new Card { Width=780, Height=height }; page.Controls.Add(c); return c; }
        private Label TextAt(Control p,string text,float size,bool bold,Color color,int x,int y) { var l=Theme.Label(text,size,bold,color); l.Location=new Point(x,y); p.Controls.Add(l); return l; }
        private FlowLayoutPanel Buttons(Control parent,int y) { var f=new FlowLayoutPanel { Location=new Point(22,y), Size=new Size(720,54), Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right }; parent.Controls.Add(f); return f; }
        private void Action(FlowLayoutPanel f,string text,bool primary,Func<Task> action) { var b=Theme.Button(text,primary); b.Click+=async(a,e)=>await Run(action); actionButtons.Add(b); f.Controls.Add(b); }
        private void BuildOverview() {
            var page=Page(); var drive=Card(page,252); TextAt(drive,"Ổ ĐĨA GOOGLE DRIVE",9,true,Theme.Muted,22,22);
            driveLabel=TextAt(drive,settings.Letter+":",40,true,Theme.Blue,22,52); TextAt(drive,"Google Drive",19,true,Theme.Ink,138,62); TextAt(drive,"Truy cập file trực tiếp trong Windows Explorer",10,false,Theme.Muted,138,98);
            status=TextAt(drive,File.Exists(service.Config)?"Sẵn sàng kết nối":"Kết nối tài khoản Google để bắt đầu",10,false,Theme.Muted,22,143); status.AutoSize=false; status.Size=new Size(730,45); status.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var buttons=Buttons(drive,192); Action(buttons,"Gắn ổ đĩa",true,async()=>{ Save(); wantMount=true; await StartMount(); });
            Action(buttons,"Mở trong Explorer",false,()=>{ if(!Directory.Exists(settings.Letter+@":\")) throw new Exception("Ổ đĩa chưa sẵn sàng."); Open(settings.Letter+@":\"); return Task.FromResult(0); });
            Action(buttons,"Ngắt kết nối",false,async()=>{ await service.Stop(); wantMount=false; status.Text="Đã ngắt kết nối"; });
            var metrics=Card(page,134); var grid=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=3, RowCount=1 }; for(int i=0;i<3;i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.33f)); metrics.Controls.Add(grid);
            queued=Metric(grid,0,"CHỜ UPLOAD"); uploading=Metric(grid,1,"ĐANG UPLOAD"); cacheUsed=Metric(grid,2,"CACHE TRÊN MÁY");
            var intro=Card(page,157); TextAt(intro,"Bắt đầu với tài khoản của bạn",14,true,Theme.Ink,22,19); TextAt(intro,"Các thành phần ổ đĩa đã được tích hợp trong bộ cài.\nĐăng nhập Google, cấp quyền truy cập và gắn ổ để làm việc.",10,false,Theme.Muted,22,51);
            Action(Buttons(intro,99),"Kết nối Google",true,async()=>{ Save(); await service.Connect(settings); status.Text="Đã kết nối Google. Bạn có thể gắn ổ đĩa."; });
        }
        private Label Metric(TableLayoutPanel g,int col,string name) { var p=new Panel { Dock=DockStyle.Fill }; g.Controls.Add(p,col,0); TextAt(p,name,8,true,Theme.Muted,0,0); return TextAt(p,"—",24,true,Theme.Ink,0,27); }
        private void BuildSettings() {
            var page=Page(); var card=Card(page,295); TextAt(card,"Thiết lập ổ đĩa",15,true,Theme.Ink,22,18); TextAt(card,"Chọn vị trí hiển thị và dung lượng cache trên máy.",10,false,Theme.Muted,22,51);
            Field(card,"Tên kết nối",remote,91); Field(card,"Ký tự ổ đĩa",letter,133); Field(card,"Thư mục trong Drive",folder,175); Field(card,"Giới hạn cache (GB)",cache,217); TextAt(card,"Để trống thư mục để sử dụng toàn bộ My Drive.",9,false,Theme.Muted,238,258);
            var automation=Card(page,225); TextAt(automation,"Khởi chạy tự động",15,true,Theme.Ink,22,18); CheckAt(automation,startup,62); CheckAt(automation,autoMount,98);
            Action(Buttons(automation,154),"Lưu cấu hình",true,()=>{ Save(); status.Text="Đã lưu cấu hình"; return Task.FromResult(0); });
            var tip=Card(page,115); TextAt(tip,"Làm việc với cache",12,true,Theme.Ink,22,18); TextAt(tip,"File được tải khi bạn mở; thay đổi sẽ được upload sau khi lưu.\nGiới hạn cache có thể tăng tạm thời khi file đang mở hoặc chờ upload.",10,false,Theme.Muted,22,50);
        }
        private void BuildUpdates() {
            var page=Page(); var card=Card(page,285); TextAt(card,"Luôn cập nhật",17,true,Theme.Ink,22,20); TextAt(card,"Phiên bản hiện tại",10,false,Theme.Muted,22,63); TextAt(card,"v"+Core.Version,25,true,Theme.Blue,22,88);
            TextAt(card,"Kiểm tra và cài phiên bản ổn định mới nhất.",10,false,Theme.Muted,22,144); CheckAt(card,autoUpdate,179);
            var buttons=Buttons(card,224); Action(buttons,"Kiểm tra cập nhật",true,async()=>{ Save(); await Update(false); }); Action(buttons,"Lưu tùy chọn",false,()=>{ Save(); status.Text="Đã lưu tùy chọn cập nhật"; return Task.FromResult(0); });
            var note=Card(page,136); TextAt(note,"Cập nhật khi ổ đĩa đã sẵn sàng",13,true,Theme.Ink,22,20); TextAt(note,"Ứng dụng kiểm tra checksum trước khi thay phiên bản.\nNếu còn file đang dùng hoặc chờ upload, cập nhật sẽ được hoãn.\nBản cũ được giữ để khôi phục khi phiên bản mới không khởi chạy được.",10,false,Theme.Muted,22,56);
        }
        private NasProfile NasValues() {
            string host=nasHost.Text.Trim(); int separator=host.LastIndexOf('·'); if(separator>=0) host=host.Substring(separator+1).Trim();
            return new NasProfile { Folder=nasFolder.Text.Trim(),Share=nasShare.Text.Trim(),User=nasUser.Text.Trim(),Host=host,RemoteShare=remoteShare.Text.Trim(),Letter=Convert.ToString(nasLetter.SelectedItem),ReadOnly=nasReadOnly.Checked,CreateUser=nasCreateUser.Checked };
        }
        private async Task RefreshNas() {
            var state=await nas.Status();
            nasNetwork.Text=state.Ready?"Đã kết nối Tailscale · IP máy này: "+state.Address+"\nMáy từ xa cần đăng nhập cùng mạng Tailscale.":state.State=="NotInstalled"?"Chưa cài Tailscale. Bấm Cài Tailscale để tải và cài tự động.":"Tailscale: "+state.State+" · Bấm Đăng nhập để kết nối.";
            string selected=nasHost.Text; nasHost.Items.Clear(); foreach(string peer in state.Peers) nasHost.Items.Add(peer); nasHost.Text=selected;
        }
        private void BuildNas() {
            var values=nas.Load(); nasFolder.Text=values.Folder; nasShare.Text=values.Share; nasUser.Text=values.User; nasPassword.UseSystemPasswordChar=true;
            nasHost.Text=values.Host; remoteShare.Text=values.RemoteShare; nasLetter.DropDownStyle=ComboBoxStyle.DropDownList; for(char c='D';c<='Z';c++) nasLetter.Items.Add(c.ToString()); nasLetter.SelectedItem=values.Letter;
            nasReadOnly.Text="Chỉ cho phép đọc file"; nasReadOnly.Checked=values.ReadOnly; nasCreateUser.Text="Tạo tài khoản Windows riêng cho NAS"; nasCreateUser.Checked=values.CreateUser;
            nasCreateUser.CheckedChanged+=(a,b)=>nasPassword.Enabled=nasCreateUser.Checked; nasPassword.Enabled=values.CreateUser;
            var page=Page(); var network=Card(page,203); TextAt(network,"Kết nối Internet riêng tư",15,true,Theme.Ink,22,18);
            nasNetwork=TextAt(network,nas.Installed?"Đã có Tailscale. Bấm Kiểm tra kết nối hoặc Đăng nhập.":"Chưa cài Tailscale. Tool sẽ tải từ nguồn chính thức khi bạn bấm Cài Tailscale.",10,false,Theme.Muted,22,62); nasNetwork.AutoSize=false; nasNetwork.Size=new Size(730,56); nasNetwork.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var networkButtons=Buttons(network,133);
            Action(networkButtons,"Cài Tailscale",true,async()=>{ nasNetwork.Text="Đang tải/cài Tailscale. Chấp nhận quyền Windows nếu xuất hiện…"; var result=await nas.Admin("Install",NasValues()); var details=(Dictionary<string,object>)result["details"]; if(details.ContainsKey("restart") && Convert.ToBoolean(details["restart"])) { nasNetwork.Text="Đã cài Tailscale. Khởi động lại Windows trước khi kết nối."; MessageBox.Show(this,nasNetwork.Text,"Cài Tailscale"); } else await RefreshNas(); });
            Action(networkButtons,"Đăng nhập",false,async()=>{ await nas.Login(text=>nasNetwork.Text=text); await RefreshNas(); });
            Action(networkButtons,"Kiểm tra kết nối",false,RefreshNas);
            var server=Card(page,490); TextAt(server,"Chia sẻ ổ trên máy này",15,true,Theme.Ink,22,18); TextAt(server,"Chọn thư mục hoặc ổ dữ liệu. Máy này phải bật để truy cập từ xa.",10,false,Theme.Muted,22,51);
            Field(server,"Ổ / thư mục",nasFolder,88);
            Action(Buttons(server,123),"Chọn thư mục / ổ",false,()=>{ using(var picker=new FolderBrowserDialog { Description="Chọn ổ dữ liệu hoặc thư mục cần chia sẻ",ShowNewFolderButton=false }) if(picker.ShowDialog(this)==DialogResult.OK) nasFolder.Text=picker.SelectedPath; return Task.FromResult(0); });
            Field(server,"Tên NAS",nasShare,183); Field(server,"Tài khoản NAS",nasUser,225); Field(server,"Mật khẩu mới",nasPassword,267);
            CheckAt(server,nasCreateUser,314); CheckAt(server,nasReadOnly,348);
            TextAt(server,"Mật khẩu mới: ≥12 ký tự, có chữ hoa, chữ thường, số và ký hiệu.",9,false,Theme.Muted,22,388);
            var serverButtons=Buttons(server,427);
            Action(serverButtons,"Tạo NAS",true,async()=>{
                var p=NasValues(); NasService.ValidateShare(p.Share); NasService.ValidateUser(p.User);
                nasServer.Text="Đang tạo NAS. Chấp nhận quyền Windows nếu xuất hiện…";
                Dictionary<string,object> result;
                try { result=await nas.Admin("Create",p,nasPassword.Text); } finally { nasPassword.Clear(); }
                var details=(Dictionary<string,object>)result["details"]; nasCreateUser.Checked=false; p.CreateUser=false; nas.Save(p);
                nasServer.Text="Đã bật NAS: "+details["path"]+"\nTài khoản truy cập: "+details["account"]+" · "+(p.ReadOnly?"Chỉ đọc":"Đọc và ghi")+"\nNhập địa chỉ và tài khoản này trên máy từ xa.";
            });
            Action(serverButtons,"Dừng chia sẻ",false,async()=>{ var result=await nas.Admin("Remove",NasValues()); nasServer.Text=Convert.ToString(((Dictionary<string,object>)result["details"])["message"]); });
            Action(serverButtons,"Kiểm tra NAS",false,async()=>{ var result=await nas.Admin("Inspect",NasValues()); var details=(Dictionary<string,object>)result["details"]; nasServer.Text=Convert.ToString(details["message"])+(details.ContainsKey("folder")?"\n"+details["folder"]:""); });
            var address=Card(page,126); nasServer=TextAt(address,"Chưa kiểm tra chia sẻ. Tạo NAS hoặc bấm Kiểm tra NAS.\nNAS dùng mật khẩu tài khoản Windows; mã PIN đăng nhập không dùng được.",10,false,Theme.Muted,22,20); nasServer.AutoSize=false; nasServer.Size=new Size(730,89); nasServer.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var client=Card(page,426); TextAt(client,"Gắn ổ từ máy khác",15,true,Theme.Ink,22,18); TextAt(client,"Chọn máy Tailscale hoặc nhập IP 100.x.x.x của máy chứa ổ.",10,false,Theme.Muted,22,51);
            Field(client,"Máy chứa NAS",nasHost,90); Field(client,"Tên NAS từ xa",remoteShare,132); Field(client,"Ký tự ổ",nasLetter,174);
            var clientButtons=Buttons(client,220);
            Action(clientButtons,"Gắn / kết nối lại",true,async()=>{ var p=NasValues(); NasService.RemotePath(p.Host,p.RemoteShare); NasService.ValidateLetter(p.Letter); var state=await nas.Status(); if(!state.Ready) throw new Exception("Đăng nhập Tailscale trước khi gắn NAS."); status.Text="Đang kiểm tra máy NAS…"; await NasService.Probe(p.Host); IntPtr owner=Handle; await Task.Run(()=>NasService.Map(owner,p.Host,p.RemoteShare,p.Letter)); nas.Save(p); nas.RememberConnection(p); status.Text="Đã gắn NAS vào ổ "+p.Letter+":"; Process.Start("explorer.exe",Core.Quote(p.Letter+@":\")); });
            Action(clientButtons,"Mở ổ NAS",false,async()=>{ var p=NasValues(); string expected=NasService.RemotePath(p.Host,p.RemoteShare); if(!string.Equals(await Task.Run(()=>NasService.MappedPath(p.Letter)),expected,StringComparison.OrdinalIgnoreCase)) throw new Exception("Gắn hoặc kết nối lại ổ NAS trước."); await NasService.Probe(p.Host); Process.Start("explorer.exe",Core.Quote(p.Letter+@":\")); });
            Action(clientButtons,"Ngắt ổ NAS",false,async()=>{ var p=NasValues(); await Task.Run(()=>NasService.Unmap(p.Host,p.RemoteShare,p.Letter)); status.Text="Đã ngắt ổ NAS"; });
            TextAt(client,"Windows hỏi tài khoản/mật khẩu; bạn có thể lưu trong hộp thoại Windows.",9,false,Theme.Muted,22,280);
            nasConnection=TextAt(client,"Tự kiểm tra và kết nối lại ổ NAS mỗi phút khi ứng dụng đang chạy.",10,false,Theme.Muted,22,310);
            nasConnection.AutoSize=false; nasConnection.Size=new Size(710,96); nasConnection.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            var note=Card(page,185); TextAt(note,"NAS hoạt động cùng Windows",12,true,Theme.Ink,22,18); TextAt(note,"Chia sẻ NAS vẫn hoạt động khi đóng tool. Không cần mở port trên router.\nGiữ máy chứa ổ bật; chế độ ngủ sẽ làm mất kết nối.\nỔ dữ liệu cần NTFS/ReFS. Google Drive tiếp tục hoạt động riêng.",10,false,Theme.Muted,22,50);
            Action(Buttons(note,122),"Chạy khi đăng xuất",false,async()=>{ await nas.Admin("Unattended",NasValues()); status.Text="Tailscale đã bật chạy khi đăng xuất Windows"; });
        }
        private void BuildAbout() {
            var page=Page(); var card=Card(page,210); TextAt(card,"CYAN NAS for Windows",18,true,Theme.Ink,22,22); TextAt(card,"v"+Core.Version+" · Bộ cài offline · Windows x64",10,false,Theme.Muted,22,61); dependencies=TextAt(card,"",10,false,Theme.Muted,22,101);
            var buttons=Buttons(card,148); Action(buttons,"Xem nhật ký",false,()=>{ if(!File.Exists(service.Log)) throw new Exception("Chưa có nhật ký."); Process.Start("notepad.exe",Core.Quote(service.Log)); return Task.FromResult(0); }); Action(buttons,"Thư mục dữ liệu",false,()=>{ Open(service.Home); return Task.FromResult(0); });
            Action(buttons,"Thoát an toàn",false,async()=>{ await service.Stop(); exiting=true; Close(); });
            var credits=Card(page,213); TextAt(credits,"Thành phần nguồn mở",14,true,Theme.Ink,22,20); TextAt(credits,"rclone · Copyright (C) Nick Craig-Wood · MIT\nWinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos",9,false,Theme.Muted,22,58);
            var link=new LinkLabel { Text="github.com/winfsp/winfsp", AutoSize=true, Location=new Point(22,106), LinkColor=Theme.Blue }; link.LinkClicked+=(a,b)=>Open("https://github.com/winfsp/winfsp"); credits.Controls.Add(link); Action(Buttons(credits,150),"Giấy phép & mã nguồn",false,()=>{ Open(Path.Combine(Application.StartupPath,"licenses")); return Task.FromResult(0); });
            var note=Card(page,112); TextAt(note,"Ổ đĩa đám mây trên máy của bạn",12,true,Theme.Ink,22,18); TextAt(note,"Tốc độ phụ thuộc kết nối mạng. Đóng file và chờ upload trước khi thoát.\nDữ liệu và tài khoản được lưu riêng theo người dùng Windows.",10,false,Theme.Muted,22,51);
        }
        private void Field(Control p,string name,Control c,int y) { TextAt(p,name,10,false,Theme.Ink,22,y+4); c.Location=new Point(238,y); c.Size=new Size(495,29); c.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; p.Controls.Add(c); }
        private void CheckAt(Control p,CheckBox c,int y) { c.Location=new Point(22,y); c.AutoSize=true; c.ForeColor=Theme.Ink; p.Controls.Add(c); }
        private void SelectPage(int index) { for(int i=0;i<pages.Count;i++) { pages[i].Visible=i==index; navigation[i].BackColor=i==index?Color.FromArgb(36,58,87):Theme.Navy; navigation[i].ForeColor=i==index?Color.White:Color.FromArgb(184,198,218); } pages[index].BringToFront(); pageTitle.Text=navigation[index].Text; }
        private async Task Run(Func<Task> action) {
            if(busy || polling) return; busy=true; foreach(var b in actionButtons) b.Enabled=false;
            try { await action(); } catch(Exception e) { status.Text=e.Message; if(tray!=null) tray.ShowBalloonTip(5000,"CYAN NAS",e.Message,ToolTipIcon.Warning); if(Visible) MessageBox.Show(this,e.Message,"CYAN NAS",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
            finally { busy=false; if(!IsDisposed) foreach(var b in actionButtons) b.Enabled=true; }
        }
        private void Save() {
            var s=new Settings { Remote=remote.Text.Trim(), Letter=Convert.ToString(letter.SelectedItem), Folder=folder.Text.Trim(), CacheGB=(int)cache.Value, AutoMount=autoMount.Checked, AutoUpdate=autoUpdate.Checked, Repository=Core.EffectiveRepository(settings.Repository) }; Core.Validate(s);
            if(service.Running && (s.Remote!=settings.Remote || s.Letter!=settings.Letter || s.Folder!=settings.Folder || s.CacheGB!=settings.CacheGB)) throw new Exception("Ngắt ổ đĩa trước khi đổi kết nối, ký tự ổ, thư mục hoặc cache.");
            service.Save(s); service.SetStartup(startup.Checked); settings=s; wantMount=s.AutoMount && File.Exists(service.Config); driveLabel.Text=settings.Letter+":";
        }
        private void DependencyStatus() { dependencies.Text="Ổ đĩa: "+(File.Exists(service.Rclone)?"sẵn sàng":"cần sửa bản cài")+"   •   Driver: "+(service.WinFspInstalled?"sẵn sàng":"chưa sẵn sàng"); }
        private async Task StartMount() {
            nextRetry=DateTime.UtcNow.AddSeconds(30); DependencyStatus(); service.Start(settings); status.Text="Đang kết nối ổ "+settings.Letter+": …";
            for(int i=0;i<20;i++) { await Task.Delay(1000); if(!service.Running) throw new Exception("Không gắn được ổ. Mở Thông tin → Xem nhật ký để kiểm tra."); if(Directory.Exists(settings.Letter+@":\")) { status.Text="Đã kết nối · File của bạn đã sẵn sàng"; return; } } status.Text="Đang chờ ổ đĩa sẵn sàng. Kiểm tra kết nối mạng.";
        }
        private async Task Tick() {
            if(busy || polling) return; polling=true;
            try {
                try { string connection=await nas.MaintainConnections(); if(connection!=null && nasConnection!=null) nasConnection.Text=connection; } catch(Exception e) { if(nasConnection!=null) nasConnection.Text="Kết nối NAS: "+e.Message; }
                if(service.Running) { var stats=await service.Rc("vfs/stats"); var d=(Dictionary<string,object>)stats["diskCache"]; queued.Text=Core.Number(d,"uploadsQueued").ToString(); uploading.Text=Core.Number(d,"uploadsInProgress").ToString(); cacheUsed.Text=(Core.Number(d,"bytesUsed")/1073741824.0).ToString("0.0")+" GB"; status.Text=Core.Number(d,"erroredFiles")>0?"Có file upload lỗi. Kiểm tra nhật ký trước khi ngắt.":"Đã kết nối · Thay đổi được đồng bộ sau khi lưu"; }
                else if(wantMount && DateTime.UtcNow>=nextRetry) await StartMount();
                if(pages[2].Visible && nas.Installed) await RefreshNas();
                if(settings.AutoUpdate && settings.Repository!="" && DateTime.UtcNow>=nextUpdate) { nextUpdate=DateTime.UtcNow.AddHours(6); await Update(true); }
            } catch(Exception ex) { status.Text=ex.Message; nextRetry=DateTime.UtcNow.AddSeconds(30); } finally { polling=false; }
        }
        private async Task Update(bool automatic) {
            status.Text="Đang kiểm tra bản cập nhật …"; var release=await Updater.Check(settings.Repository); if(release==null) { if(!automatic) MessageBox.Show(this,"Bạn đang dùng phiên bản mới nhất.","CYAN NAS"); status.Text="Bạn đang dùng phiên bản mới nhất."; return; }
            if(!automatic && MessageBox.Show(this,"Cài bản "+release.Tag+"? Đóng file trên ổ đĩa trước khi tiếp tục. Ứng dụng sẽ khởi động lại.","Cập nhật",MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
            status.Text="Đang tải và kiểm tra "+release.Tag; string stage=await Updater.Stage(release,service.Home); await service.Stop(); Updater.Launch(stage); exiting=true; Close();
        }
        public void PreviewPage(int index) { SelectPage(index); }
        public void ShowWindow() { ShowInTaskbar=true; Show(); WindowState=FormWindowState.Normal; BringToFront(); Activate(); }
        protected override void WndProc(ref Message message) {
            if(message.Msg==Program.ShowWindowMessage) { ShowWindow(); return; }
            base.WndProc(ref message);
        }
        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);
            Rectangle area=Screen.FromControl(this).WorkingArea;
            MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));
            Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
            foreach(FlowLayoutPanel page in pages) FitCards(page);
        }
        private static void Open(string path) { Process.Start(new ProcessStartInfo(path) { UseShellExecute=true }); }
        protected override void Dispose(bool disposing) { if(disposing) { timer.Dispose(); if(tray!=null) tray.Dispose(); if(Icon!=null) Icon.Dispose(); } base.Dispose(disposing); }
    }
}
