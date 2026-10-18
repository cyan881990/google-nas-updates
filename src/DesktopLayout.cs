using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace GoogleNas {
    public partial class MainForm {
        private readonly ToolTip tips=new ToolTip();
        private Label subtitle, breadcrumb, activity;
        private Button driveMount, driveOpen, driveStop, driveLogin, serverReveal, clientReveal;
        private DateTime nextNasRefresh=DateTime.MinValue;
        private System.Action resizeSidebar;
        private Image brandImage;
        private string[] pageNames={"Google Drive","Ổ cứng thành NAS","Kết nối NAS","Mạng Tailscale","Cài đặt","Trợ giúp"};
        private string[] pageDescriptions={"Kết nối tài khoản Google và làm việc với file ngay trong Windows Explorer.","Chia sẻ ổ cứng hoặc thư mục trên máy này cho các thiết bị của bạn.","Gắn ổ được chia sẻ từ máy khác vào Windows Explorer trên máy này.","Thiết lập mạng riêng để truy cập NAS từ xa qua Internet.","Quản lý khởi động cùng Windows và các bản cập nhật của CYAN NAS.","Thông tin ứng dụng, thành phần tích hợp và công cụ hỗ trợ."};
        private float UiScale { get { return desktopScale; } }
        private void BuildDesktop() {
            var shell=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,232)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var frame=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty }; frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); frame.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); frame.RowStyles.Add(new RowStyle(SizeType.Percent,100)); Controls.Add(frame); frame.Controls.Add(CreateWindowBar(),0,0); frame.Controls.Add(shell,0,1);
            var side=new Panel { Dock=DockStyle.Fill,BackColor=Theme.Sidebar,Margin=Padding.Empty }; shell.Controls.Add(side,0,0);
            var navArea=new Panel { Dock=DockStyle.Fill,BackColor=Theme.Sidebar,AutoScroll=true,Name="NavigationScroll" }; side.Controls.Add(navArea);
            var brandArea=new Panel { Dock=DockStyle.Top,Height=100,BackColor=Theme.Sidebar }; side.Controls.Add(brandArea);
            brandImage=IconArtwork.Render(128);
            var brandIcon=new PictureBox { Name="BrandIcon",Image=brandImage,SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(40,40),Location=new Point(18,21) }; brandArea.Controls.Add(brandIcon);
            var brand=TextAt(brandArea,"CYAN NAS",18,true,Theme.Ink,70,26); brand.Name="Brand";
            var tagline=TextAt(brandArea,"Ổ đĩa của bạn. Kết nối mọi nơi.",8.5f,false,Theme.Muted,26,70);
            var mainCaption=TextAt(navArea,"TÍNH NĂNG CHÍNH",8,true,Theme.Muted,26,19);
            for(int i=0;i<pageNames.Length;i++) {
                int index=i; int top=i<2?46+i*56:226+(i-2)*52;
                var nav=new MacButton { Name="Nav"+i,Text=pageNames[i],TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(16,0,0,0),FlatStyle=FlatStyle.Flat,ForeColor=Theme.Ink,BackColor=Theme.Sidebar,Size=new Size(208,46),Location=new Point(12,top),Cursor=Cursors.Hand,TabIndex=i };
                nav.FlatAppearance.BorderSize=0; nav.Click+=(a,b)=>SelectPage(index); navArea.Controls.Add(nav); navigation.Add(nav);
            }
            var appCaption=TextAt(navArea,"KẾT NỐI & ỨNG DỤNG",8,true,Theme.Muted,26,194);
            var version=Theme.Label("CYAN NAS  "+Core.Version+"\nWindows x64",9,false,Theme.Muted); version.Dock=DockStyle.Bottom; version.AutoSize=false; version.Height=78; version.Padding=new Padding(26,15,0,0); side.Controls.Add(version);
            var main=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty,Padding=new Padding(28,22,28,12) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute,106)); main.RowStyles.Add(new RowStyle(SizeType.Percent,100)); main.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); shell.Controls.Add(main,1,0);
            var header=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute,23)); header.RowStyles.Add(new RowStyle(SizeType.Absolute,43)); header.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            breadcrumb=HeaderLabel("KHÔNG GIAN LÀM VIỆC",8.5f,true,Theme.Muted); pageTitle=HeaderLabel("Google Drive",23,true,Theme.Ink); subtitle=HeaderLabel("",10,false,Theme.Muted);
            breadcrumb.Name="Breadcrumb"; pageTitle.Name="PageTitle"; subtitle.Name="PageDescription";
            header.Controls.Add(breadcrumb,0,0); header.Controls.Add(pageTitle,0,1); header.Controls.Add(subtitle,0,2); main.Controls.Add(header,0,0);
            pageHost=new Panel { Dock=DockStyle.Fill,Margin=Padding.Empty }; main.Controls.Add(pageHost,0,1);
            activity=HeaderLabel("Sẵn sàng · Chọn một chức năng ở thanh bên để bắt đầu.",9,false,Theme.Muted); activity.Name="Activity"; activity.Padding=new Padding(0,12,0,0); main.Controls.Add(activity,0,2);
            BuildDrivePage(); BuildLocalPage(); BuildRemotePage(); BuildNetworkPage(); BuildApplicationPage(); BuildHelpPage();
            status.TextChanged+=(a,b)=>{ if(!polling) { activity.Text=status.Text; tips.SetToolTip(activity,status.Text); } };
            Deactivate+=(a,b)=>HidePasswords();
            bool resizingSidebar=false;
            resizeSidebar=()=>{
                if(resizingSidebar || IsDisposed || Theme.LayoutScaling) return;
                resizingSidebar=true;
                try {
                    float scale=UiScale; bool compact=ClientSize.Width/scale<1000;
                    shell.ColumnStyles[0].Width=(compact?160:232)*scale;
                    shell.PerformLayout(); side.PerformLayout();
                    brandArea.Height=(int)((compact?84:100)*scale); tagline.Visible=!compact;
                    brandIcon.Bounds=new Rectangle((int)(16*scale),(int)(21*scale),(int)(40*scale),(int)(40*scale));
                    brand.Location=new Point((int)((compact?62:70)*scale),(int)((compact?30:26)*scale));
                    float fontSize=(compact?11.5f:18)*Font.Size/10f;
                    if(Math.Abs(brand.Font.Size-fontSize)>0.01) brand.Font=new Font("Segoe UI",fontSize,FontStyle.Bold);
                    mainCaption.Text=compact?"CHÍNH":"TÍNH NĂNG CHÍNH"; appCaption.Text=compact?"ỨNG DỤNG":"KẾT NỐI & ỨNG DỤNG";
                    mainCaption.Left=appCaption.Left=(int)((compact?14:26)*scale);
                    string[] compactNames={"Google Drive","Ổ cứng NAS","Kết nối NAS","Tailscale","Cài đặt","Trợ giúp"};
                    for(int i=0;i<navigation.Count;i++) { var nav=navigation[i]; nav.Text=compact?compactNames[i]:pageNames[i]; nav.Left=(int)((compact?8:12)*scale); nav.Width=Math.Max(1,navArea.ClientSize.Width-nav.Left*2); }
                    navArea.PerformLayout();
                } finally { resizingSidebar=false; }
            };
            SizeChanged+=(a,b)=>resizeSidebar(); navArea.SizeChanged+=(a,b)=>resizeSidebar(); resizeSidebar();
        }
        private Label HeaderLabel(string text,float size,bool bold,Color color) { var l=Theme.Label(text,size,bold,color); l.AutoSize=false; l.Dock=DockStyle.Fill; l.AutoEllipsis=true; l.Margin=Padding.Empty; return l; }
        private Panel Page() {
            var page=new Panel { Name="Page"+pages.Count,Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty,Visible=false,AutoScrollMargin=new Size(0,16) };
            bool queuedLayout=false;
            pageHost.Controls.Add(page); pages.Add(page); page.SizeChanged+=(a,b)=>{ FitCards(page); if(!queuedLayout && page.IsHandleCreated) { queuedLayout=true; page.BeginInvoke((MethodInvoker)(()=>{ queuedLayout=false; if(page.IsDisposed) return; FitCards(page); page.PerformLayout(); })); } }; page.Layout+=(a,b)=>FitCards(page); return page;
        }
        private DashboardColumns Columns(Panel page) { var c=new DashboardColumns(()=>UiScale) { Width=940,Margin=Padding.Empty }; page.Controls.Add(c); return c; }
        private void FitCards(Panel page) { if(Theme.LayoutScaling) return; foreach(Control c in page.Controls) c.Width=Math.Max(1,page.ClientSize.Width-2); }
        private Card Section(DashboardColumns grid,bool right,string title,string description,int height) {
            var card=new Card { Width=430,Height=height,Name=title }; grid.Add(card,right);
            TextAt(card,title,14,true,Theme.Ink,22,18); Paragraph(card,description,22,49,42); return card;
        }
        private Label TextAt(Control p,string text,float size,bool bold,Color color,int x,int y) { var l=Theme.Label(text,size,bold,color); l.Location=new Point(x,y); p.Controls.Add(l); return l; }
        private Label Paragraph(Control parent,string text,int x,int y,int height) { var l=TextAt(parent,text,9.5f,false,Theme.Muted,x,y); l.AutoSize=false; l.Size=new Size(parent.Width-x-22,height); l.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; l.AutoEllipsis=true; tips.SetToolTip(l,text); l.TextChanged+=(a,b)=>tips.SetToolTip(l,l.Text); return l; }
        private FlowLayoutPanel Buttons(Control parent,int y) { var row=new FlowLayoutPanel { Location=new Point(22,y),Size=new Size(parent.Width-44,48),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,WrapContents=false,Margin=Padding.Empty }; parent.Controls.Add(row); return row; }
        private Button Action(FlowLayoutPanel row,string text,bool primary,Func<Task> action,string name=null,bool danger=false) {
            var button=Theme.Button(text,primary); button.Name=name??text; if(danger) { button.ForeColor=Color.FromArgb(174,48,61); button.FlatAppearance.BorderColor=Color.FromArgb(235,207,211); }
            button.Click+=async(a,b)=>await Run(action); actionButtons.Add(button); row.Controls.Add(button); return button;
        }
        private Label Field(Control card,string label,Control input,int y) {
            var caption=TextAt(card,label,9.5f,true,Theme.Ink,22,y); input.AccessibleName=label; var surface=new InputSurface(input) { Location=new Point(22,y+25),Size=new Size(card.Width-44,34),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right }; card.Controls.Add(surface); return caption;
        }
        private Button InlineField(Control card,string label,Control input,int y,string action,Func<Task> click,string name) {
            TextAt(card,label,9.5f,true,Theme.Ink,22,y);
            var row=new TableLayoutPanel { Location=new Point(22,y+25),Size=new Size(card.Width-44,34),ColumnCount=2,RowCount=1,Margin=Padding.Empty,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,84));
            row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            input.AccessibleName=label; var surface=new InputSurface(input) { Dock=DockStyle.Fill,Margin=new Padding(0,0,8,0) }; row.Controls.Add(surface,0,0);
            var button=Theme.Button(action,false); button.Name=name; button.MinimumSize=Size.Empty; button.AutoSize=false; button.Padding=Padding.Empty; button.Dock=DockStyle.Fill; button.Margin=Padding.Empty;
            button.Click+=async(a,b)=>await Run(click); row.Controls.Add(button,1,0); actionButtons.Add(button); card.Controls.Add(row); return button;
        }
        private void CheckAt(Control parent,CheckBox box,int y) { box.Location=new Point(22,y); box.AutoSize=true; box.ForeColor=Theme.Ink; parent.Controls.Add(box); }
        private void SelectPage(int index) {
            if(index<0 || index>=pages.Count) throw new ArgumentOutOfRangeException("index");
            if(resizeSidebar!=null) resizeSidebar();
            for(int i=0;i<pages.Count;i++) { pages[i].Visible=i==index; navigation[i].BackColor=i==index?Theme.Blue:Theme.Sidebar; navigation[i].ForeColor=i==index?Color.White:Theme.Ink; }
            pages[index].BringToFront(); pageTitle.Text=pageNames[index]; subtitle.Text=pageDescriptions[index]; breadcrumb.Text=index<2?"KHÔNG GIAN LÀM VIỆC":"KẾT NỐI & ỨNG DỤNG";
            FitCards(pages[index]); pages[index].PerformLayout(); HidePasswords();
            tips.SetToolTip(subtitle,subtitle.Text);
        }
        private void HidePasswords() { nasPassword.UseSystemPasswordChar=true; remotePassword.UseSystemPasswordChar=true; if(serverReveal!=null) serverReveal.Text="Hiện"; if(clientReveal!=null) clientReveal.Text="Hiện"; }
        private void BuildDrivePage() {
            var grid=Columns(Page()); var drive=Section(grid,false,"Ổ đĩa Google Drive","Tài khoản Google của bạn, truy cập như một ổ đĩa trên PC.",274);
            driveLabel=TextAt(drive,settings.Letter+":",36,true,Theme.Blue,22,91);
            status=Paragraph(drive,File.Exists(service.Config)?"Đã có cấu hình Google.\nSẵn sàng gắn ổ đĩa.":"Chưa kết nối tài khoản.\nBắt đầu bằng Kết nối Google.",128,99,58); status.ForeColor=Theme.Ink;
            var connect=Buttons(drive,173);
            driveLogin=Action(connect,"Kết nối Google",!File.Exists(service.Config),async()=>{ Save(); await service.Connect(settings); status.Text="Đã kết nối Google. Bấm Gắn ổ đĩa để bắt đầu."; },"GoogleLogin");
            driveMount=Action(connect,"Gắn ổ đĩa",File.Exists(service.Config),async()=>{ Save(); wantMount=true; await StartMount(); },"GoogleMount");
            var manage=Buttons(drive,222); driveOpen=Action(manage,"Mở Explorer",false,()=>{ Process.Start("explorer.exe",Core.Quote(settings.Letter+@":\")); return Task.FromResult(0); },"GoogleOpen");
            driveStop=Action(manage,"Ngắt ổ",false,async()=>{ await service.Stop(); wantMount=false; status.Text="Đã ngắt Google Drive."; },"GoogleStop",true);
            var metrics=new Card { Name="DriveMetrics",Height=111,Width=430,Padding=new Padding(10) }; grid.Add(metrics,false);
            var metricGrid=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(10,8,6,4) }; for(int i=0;i<3;i++) metricGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3)); metrics.Controls.Add(metricGrid);
            metricGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            queued=Metric(metricGrid,0,"CHỜ TẢI LÊN"); uploading=Metric(metricGrid,1,"ĐANG TẢI LÊN"); cacheUsed=Metric(metricGrid,2,"CACHE ĐÃ DÙNG");
            var note=Section(grid,false,"Làm việc như ổ đĩa thường","File được tải về khi mở. Sau khi lưu, thay đổi sẽ được tải lên Google Drive.",112);
            var config=Section(grid,true,"Vị trí & bộ nhớ đệm","Tùy chỉnh cách ổ Google Drive xuất hiện trên máy này.",448);
            Field(config,"Ký tự ổ đĩa",letter,94); Field(config,"Thư mục (trống = toàn bộ Drive)",folder,162); Field(config,"Bộ nhớ đệm tối đa (GB)",cache,230);
            var remoteCaption=Field(config,"Tên cấu hình Google",remote,350); remoteCaption.Visible=false; remote.Visible=false; remote.Parent.Visible=false;
            tips.SetToolTip(folder,"Để trống để dùng toàn bộ My Drive."); tips.SetToolTip(remote,"Giữ tên hiện có nếu bạn không sử dụng cấu hình Google tùy chỉnh.");
            CheckAt(config,autoMount,347); var saveRow=Buttons(config,390); Action(saveRow,"Lưu cấu hình",false,()=>{ Save(); status.Text="Đã lưu cấu hình Google Drive."; return Task.FromResult(0); },"SaveGoogle");
            Button advanced=null; advanced=Action(Buttons(config,296),"+  Tùy chọn nâng cao",false,()=>{ bool expand=!remote.Parent.Visible; remote.Visible=expand; remote.Parent.Visible=expand; remoteCaption.Visible=expand; advanced.Text=expand?"−  Thu gọn nâng cao":"+  Tùy chọn nâng cao"; autoMount.Top=(int)Math.Round((expand?423:347)*UiScale); saveRow.Top=(int)Math.Round((expand?466:390)*UiScale); config.Height=(int)Math.Round((expand?524:448)*UiScale); return Task.FromResult(0); },"GoogleAdvanced");
        }
        private Label Metric(TableLayoutPanel grid,int col,string name) { var p=new Panel { Dock=DockStyle.Fill,Margin=Padding.Empty }; grid.Controls.Add(p,col,0); TextAt(p,name,7.5f,true,Theme.Muted,0,0); return TextAt(p,"—",20,true,Theme.Ink,0,25); }
        private void BuildLocalPage() {
            var grid=Columns(Page()); var source=Section(grid,false,"1. Chọn dữ liệu chia sẻ","Chọn ổ dữ liệu hoặc thư mục trên chính máy này.",314);
            InlineField(source,"Ổ / thư mục cần chia sẻ",nasFolder,94,"Chọn…",()=>{ using(var picker=new FolderBrowserDialog { Description="Chọn ổ dữ liệu hoặc thư mục cần chia sẻ",ShowNewFolderButton=false }) if(picker.ShowDialog(this)==DialogResult.OK) nasFolder.Text=picker.SelectedPath; return Task.FromResult(0); },"BrowseNas");
            Field(source,"Tên NAS hiển thị trên mạng",nasShare,166); CheckAt(source,nasReadOnly,238); Paragraph(source,"Bật chỉ đọc nếu bạn không muốn thiết bị khác sửa hoặc xóa file.",22,270,40);
            var address=Section(grid,false,"Trạng thái chia sẻ","Sao chép tài khoản Windows chính xác để đăng nhập từ máy khác.",282);
            nasServer=Paragraph(address,"Chưa kiểm tra chia sẻ. Bấm Kiểm tra NAS để xem địa chỉ và trạng thái hiện tại.",22,91,70); nasServer.ForeColor=Theme.Ink;
            Action(Buttons(address,172),"Kiểm tra",false,async()=>{ var result=await nas.Admin("Inspect",NasValues()); var details=(Dictionary<string,object>)result["details"]; nasServer.Text=Convert.ToString(details["message"])+(details.ContainsKey("folder")?"\n"+details["folder"]:"")+"\nTài khoản cấu hình: "+NasService.LocalAccount(nasUser.Text.Trim()); },"InspectNas");
            Action((FlowLayoutPanel)address.Controls[address.Controls.Count-1],"Dừng NAS",false,async()=>{ var result=await nas.Admin("Remove",NasValues()); nasServer.Text=Convert.ToString(((Dictionary<string,object>)result["details"])["message"]); },"StopNas",true);
            Action(Buttons(address,224),"Sao chép tài khoản",false,()=>{ string account=NasService.LocalAccount(nasUser.Text.Trim()); Clipboard.SetText(account); status.Text="Đã sao chép tài khoản Windows: "+account; return Task.FromResult(0); },"CopyNasAccount");
            var access=Section(grid,true,"2. Tài khoản truy cập","Kiểm tra mật khẩu Windows của tài khoản trên chính máy này.",516);
            Field(access,"Tài khoản Windows / NAS",nasUser,94);
            serverReveal=InlineField(access,"Mật khẩu NAS",nasPassword,166,"Hiện",()=>{ if(nasPassword.UseSystemPasswordChar) { if(nasPassword.Text.Length==0) nasPassword.Text=nas.LoadPassword(nasUser.Text.Trim()); if(nasPassword.Text.Length==0) throw new Exception("Chưa lưu mật khẩu. Nhập mật khẩu hiện tại rồi bấm Lưu mật khẩu."); nasPassword.UseSystemPasswordChar=false; } else nasPassword.UseSystemPasswordChar=true; serverReveal.Text=nasPassword.UseSystemPasswordChar?"Hiện":"Ẩn"; return Task.FromResult(0); },"ServerReveal");
            Action(Buttons(access,237),"Lưu mật khẩu",false,()=>{ nas.SavePassword(nasUser.Text.Trim(),nasPassword.Text); HidePasswords(); status.Text="Đã lưu mật khẩu mã hóa. Mật khẩu Windows không bị thay đổi."; return Task.FromResult(0); },"ServerSavePassword");
            Action(Buttons(access,291),"Kiểm tra mật khẩu",false,async()=>{ string user=nasUser.Text.Trim(); string password=nasPassword.Text; if(password.Length==0) password=nas.LoadPassword(user); HidePasswords(); await Task.Run(()=>NasService.CheckLocalPassword(user,password)); status.Text="Windows chấp nhận mật khẩu cho "+NasService.LocalAccount(user)+". Nếu máy khác vẫn lỗi, kiểm tra quyền SMB/NTFS và dùng tài khoản được sao chép."; },"TestNasPassword");
            CheckAt(access,nasCreateUser,353); Paragraph(access,"Lưu mật khẩu không đổi mật khẩu Windows. Tài khoản có sẵn dùng mật khẩu hiện tại. Tài khoản mới cần ít nhất 12 ký tự, đủ hoa, thường, số, ký hiệu.",22,390,54);
            Action(Buttons(access,456),"Bật chia sẻ NAS",true,CreateLocalNas,"CreateNas");
            nasUser.TextChanged+=(a,b)=>{ nasPassword.Clear(); HidePasswords(); };
            var help=Section(grid,true,"NAS qua Internet","Cài và đăng nhập Tailscale trên cả hai máy. Máy chứa ổ cần bật và không ở chế độ ngủ.",171);
            Action(Buttons(help,111),"Thiết lập Tailscale",false,()=>{ SelectPage(3); return Task.FromResult(0); },"LocalNetwork");
        }
        private async Task CreateLocalNas() {
            var p=NasValues(); NasService.ValidateShare(p.Share); NasService.ValidateUser(p.User); nasServer.Text="Đang tạo NAS. Chấp nhận quyền Windows nếu xuất hiện…";
            Dictionary<string,object> result; string enteredPassword=nasPassword.Text; bool createdUser=p.CreateUser; string warning="";
            try { result=await nas.Admin("Create",p,enteredPassword); if(createdUser) { try { nas.SavePassword(p.User,enteredPassword); } catch { warning="\nChưa lưu được mật khẩu; hãy nhập lại và bấm Lưu mật khẩu."; } } } finally { nasPassword.Clear(); HidePasswords(); }
            var details=(Dictionary<string,object>)result["details"]; nasCreateUser.Checked=false; p.CreateUser=false; nas.Save(p);
            nasServer.Text=details["path"]+"\nTài khoản: "+details["account"]+"\n"+(p.ReadOnly?"Chỉ đọc":"Đọc và ghi")+warning; status.Text="Đã tạo NAS. Dùng địa chỉ và tài khoản ở mục Trạng thái chia sẻ.";
        }
        private void BuildRemotePage() {
            var grid=Columns(Page()); var target=Section(grid,false,"1. Chọn NAS từ máy khác","Nhập IP Tailscale và tên NAS được chia sẻ từ máy chứa ổ.",371);
            Field(target,"Máy chứa NAS · IP Tailscale",nasHost,94); Field(target,"Tên NAS từ xa",remoteShare,166); Field(target,"Ký tự ổ trên máy này",nasLetter,238);
            Action(Buttons(target,310),"Gắn ổ NAS",true,MountRemoteNas,"MountRemote"); Action((FlowLayoutPanel)target.Controls[target.Controls.Count-1],"Mở ổ NAS",false,async()=>{ var p=NasValues(); string expected=NasService.RemotePath(p.Host,p.RemoteShare); if(!string.Equals(await Task.Run(()=>NasService.MappedPath(p.Letter)),expected,StringComparison.OrdinalIgnoreCase)) throw new Exception("Gắn hoặc kết nối lại ổ NAS trước."); await NasService.Probe(p.Host); Process.Start("explorer.exe",Core.Quote(p.Letter+@":\")); },"OpenRemote");
            var health=Section(grid,false,"Theo dõi kết nối","Tự kiểm tra mỗi phút khi CYAN NAS đang chạy.",218);
            nasConnection=Paragraph(health,"Thông tin kết nối sẽ hiển thị sau khi gắn ổ NAS.",22,91,63); nasConnection.ForeColor=Theme.Ink;
            Action(Buttons(health,162),"Ngắt ổ NAS",false,async()=>{ var p=NasValues(); await Task.Run(()=>NasService.Unmap(p.Host,p.RemoteShare,p.Letter)); nasConnection.Text="Đã ngắt ổ NAS "+p.Letter+":"; status.Text=nasConnection.Text; },"DisconnectRemote",true);
            var credentials=Section(grid,true,"2. Đăng nhập NAS","Dùng tài khoản của máy chứa ổ, ví dụ TEN-MAY\\taikhoan.",354);
            Field(credentials,"Tài khoản máy chứa NAS",remoteUser,94);
            clientReveal=InlineField(credentials,"Mật khẩu NAS từ xa",remotePassword,166,"Hiện",()=>{ if(!remotePassword.UseSystemPasswordChar) { remotePassword.UseSystemPasswordChar=true; clientReveal.Text="Hiện"; return Task.FromResult(0); } var p=NasValues(); if(remotePassword.Text.Length==0) { var login=nas.LoadRemoteLogin(p.Host,p.RemoteShare); if(login==null || (p.RemoteUser.Length>0 && !string.Equals(login.User,p.RemoteUser,StringComparison.OrdinalIgnoreCase))) throw new Exception("Chưa lưu đăng nhập cho NAS này. Nhập thông tin rồi bấm Gắn / kết nối lại."); remoteUser.Text=login.User; remotePassword.Text=login.Password; } remotePassword.UseSystemPasswordChar=false; clientReveal.Text="Ẩn"; return Task.FromResult(0); },"ClientReveal");
            Paragraph(credentials,"Chỉ cần nhập lần đầu. Khi gắn thành công, thông tin đăng nhập được tự lưu có mã hóa để kết nối lại.",22,245,66);
            nasHost.TextChanged+=(a,b)=>{ if(!updatingHosts) ClearRemotePassword(); }; remoteShare.TextChanged+=(a,b)=>ClearRemotePassword(); remoteUser.TextChanged+=(a,b)=>ClearRemotePassword();
            var help=Section(grid,true,"Lưu ý khi đăng nhập","Hai máy cần đăng nhập cùng mạng Tailscale. Dùng mật khẩu Windows / NAS, không dùng mã PIN đăng nhập.",176);
            Action(Buttons(help,116),"Kiểm tra Tailscale",false,()=>{ SelectPage(3); return Task.FromResult(0); },"RemoteNetwork");
        }
        private void ClearRemotePassword() { remotePassword.Clear(); remotePassword.UseSystemPasswordChar=true; if(clientReveal!=null) clientReveal.Text="Hiện"; }
        private async Task MountRemoteNas() {
            var p=NasValues(); string password=remotePassword.Text; NasService.RemotePath(p.Host,p.RemoteShare); NasService.ValidateLetter(p.Letter);
            if(!(await nas.Status()).Ready) throw new Exception("Vào Mạng Tailscale và đăng nhập trước khi gắn NAS.");
            status.Text="Đang kiểm tra máy NAS…"; await NasService.Probe(p.Host); IntPtr owner=Handle; string warning;
            try { warning=await Task.Run(()=>nas.MapAndSave(owner,p,p.RemoteUser,password)); } finally { ClearRemotePassword(); }
            remoteUser.Text=p.RemoteUser; nas.Save(p); nas.RememberConnection(p); nasConnection.Text="Đã gắn "+p.Letter+": → "+NasService.RemotePath(p.Host,p.RemoteShare);
            status.Text=warning.Length==0?"Đã gắn ổ NAS "+p.Letter+": · Đã ghi nhớ đăng nhập.":warning; Process.Start("explorer.exe",Core.Quote(p.Letter+@":\"));
        }
        private void BuildNetworkPage() {
            var grid=Columns(Page()); var network=Section(grid,false,"Kết nối Tailscale","Mạng riêng giữa các thiết bị, dùng cho NAS từ xa.",351);
            nasNetwork=Paragraph(network,nas.Installed?"Đã cài Tailscale. Kiểm tra trạng thái hoặc đăng nhập để bắt đầu.":"Chưa cài Tailscale trên máy này. Bắt đầu với bước Cài Tailscale.",22,94,78); nasNetwork.ForeColor=Theme.Ink;
            var row=Buttons(network,184); Action(row,"Cài Tailscale",true,async()=>{ nasNetwork.Text="Đang tải và cài Tailscale…"; var result=await nas.Admin("Install",NasValues()); var details=(Dictionary<string,object>)result["details"]; if(details.ContainsKey("restart") && Convert.ToBoolean(details["restart"])) nasNetwork.Text="Đã cài Tailscale. Khởi động lại Windows trước khi kết nối."; else await RefreshNas(); },"InstallTailscale");
            Action(row,"Đăng nhập",false,async()=>{ await nas.Login(text=>nasNetwork.Text=text); await RefreshNas(); },"LoginTailscale");
            Action(Buttons(network,237),"Kiểm tra kết nối",false,RefreshNas,"RefreshTailscale");
            Action(Buttons(network,290),"Chạy khi đăng xuất",false,async()=>{ await nas.Admin("Unattended",NasValues()); status.Text="Đã bật Tailscale chạy khi đăng xuất Windows."; },"Unattended");
            var steps=Section(grid,true,"Thiết lập trên hai máy","Thực hiện trên máy chia sẻ ổ và máy cần truy cập.",290);
            Paragraph(steps,"1   Cài Tailscale trên mỗi máy.\n\n2   Đăng nhập cùng mạng Tailscale.\n\n3   Tạo NAS ở máy chứa ổ, sau đó gắn ổ ở máy còn lại.",22,94,160);
            var note=Section(grid,true,"Giữ máy chứa ổ bật","Giữ máy bật và kết nối Internet. Có thể bật Chạy khi đăng xuất để NAS tiếp tục truy cập được sau khi đăng xuất Windows.",141);
        }
        private void BuildApplicationPage() {
            var grid=Columns(Page()); var general=Section(grid,false,"Khởi động & chạy nền","Tùy chọn áp dụng cho ứng dụng trên máy này.",254);
            CheckAt(general,startup,99); CheckAt(general,autoUpdate,143);
            Action(Buttons(general,195),"Lưu tùy chọn",true,()=>{ Save(); status.Text="Đã lưu cài đặt ứng dụng."; return Task.FromResult(0); },"SaveApplication");
            var update=Section(grid,true,"Phiên bản ứng dụng","Cài các cải tiến và bản sửa lỗi mới nhất.",254);
            TextAt(update,"v"+Core.Version,28,true,Theme.Blue,22,91); Paragraph(update,"Nguồn cập nhật chính thức đã được cấu hình sẵn.",22,151,30);
            Action(Buttons(update,195),"Kiểm tra cập nhật",true,async()=>{ Save(); await Update(false); },"CheckUpdates");
            Section(grid,false,"Chạy nền","Đóng cửa sổ sẽ thu nhỏ ứng dụng. Mở lại từ taskbar, shortcut hoặc biểu tượng khay hệ thống.",143);
            Section(grid,true,"Cập nhật an toàn","Tool kiểm tra checksum trước khi thay phiên bản. Hãy đóng file và chờ tải lên hoàn tất trước khi cập nhật hoặc thoát an toàn.",143);
        }
        private void BuildHelpPage() {
            var grid=Columns(Page()); var support=Section(grid,false,"Hỗ trợ & chẩn đoán","Nhật ký và dữ liệu của CYAN NAS trên máy này.",278);
            dependencies=Paragraph(support,"",22,94,48);
            var row=Buttons(support,153); Action(row,"Xem nhật ký",false,()=>{ if(!File.Exists(service.Log)) throw new Exception("Chưa có nhật ký."); Process.Start("notepad.exe",Core.Quote(service.Log)); return Task.FromResult(0); },"OpenLog");
            Action(row,"Dữ liệu",false,()=>{ Open(service.Home); return Task.FromResult(0); },"OpenData");
            Action(Buttons(support,213),"Thoát an toàn",false,async()=>{ await service.Stop(); exiting=true; Close(); },"SafeExit");
            var credits=Section(grid,true,"CYAN NAS cho Windows","Hai cách kết nối dữ liệu: Google Drive và ổ cứng của bạn.",282);
            Paragraph(credits,"rclone · Nick Craig-Wood · MIT\n\nWinFsp · Bill Zissimopoulos\n\nNAS từ xa qua mạng Tailscale.",22,94,118);
            Action(Buttons(credits,220),"Giấy phép & mã nguồn",false,()=>{ Open(Path.Combine(Application.StartupPath,"licenses")); return Task.FromResult(0); },"Licenses");
        }
        private void UpdateDriveActions() {
            if(driveMount==null || busy) return; bool running=service.Running;
            driveMount.Enabled=!running && File.Exists(service.Config); driveLogin.Enabled=!running; driveOpen.Enabled=running; driveStop.Enabled=running;
        }
    }
    public class DashboardColumns : Panel {
        private readonly List<Control> left=new List<Control>(),right=new List<Control>();
        private readonly Func<float> scale;
        private bool laying;
        public bool IsWide { get; private set; }
        public DashboardColumns(Func<float> factor) { scale=factor; BackColor=Theme.Background; DoubleBuffered=true; }
        public void Add(Control card,bool rightColumn) { (rightColumn?right:left).Add(card); Controls.Add(card); card.TabIndex=rightColumn?right.Count*2-1:left.Count*2-2; card.SizeChanged+=(a,b)=>Reflow(); Reflow(); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Reflow(); }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Reflow(); }
        private int Arrange(List<Control> cards,int x,int y,int width,int gap) { int start=y; foreach(var card in cards) { card.SetBounds(x,y,Math.Max(1,width),card.Height); y+=card.Height+gap; } return cards.Count==0?0:y-start-gap; }
        private void Reflow() {
            if(laying || scale==null || Theme.LayoutScaling) return; laying=true;
            try { int gap=(int)Math.Round(18*scale()); IsWide=Width>=840*scale();
                int w=IsWide?(Width-gap)/2:Width; int lh=Arrange(left,0,0,w,gap); int rh=Arrange(right,IsWide?w+gap:0,IsWide?0:lh+gap,IsWide?Width-w-gap:Width,gap);
                Height=IsWide?Math.Max(lh,rh):lh+(right.Count>0?gap+rh:0);
            } finally { laying=false; }
        }
    }
}
