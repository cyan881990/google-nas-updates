using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace GoogleNas {
    internal class NasListEntry {
        public NasProfile Profile; public NasItemState Status; public bool Local;
        public override string ToString() { return (Local?Profile.Share:Profile.Letter+": · "+Profile.RemoteShare)+" · "+Status.Message; }
    }
    internal class NasListBox : ListBox {
        private readonly Func<float> factor;
        public NasListBox(Func<float> scale) { factor=scale; DrawMode=DrawMode.OwnerDrawFixed; BorderStyle=BorderStyle.None; IntegralHeight=false; HorizontalScrollbar=false; BackColor=Color.White; }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if(factor!=null) ItemHeight=Math.Min(255,Math.Max(32,(int)(64*factor()))); }
        protected override void OnDrawItem(DrawItemEventArgs e) {
            if(e.Index<0 || e.Index>=Items.Count) return; var item=(NasListEntry)Items[e.Index]; bool selected=(e.State&DrawItemState.Selected)!=0; float s=factor();
            using(var brush=new SolidBrush(selected?Color.FromArgb(231,241,255):Color.White)) e.Graphics.FillRectangle(brush,e.Bounds);
            string title=item.Local?item.Profile.Share:item.Profile.Letter+":   "+item.Profile.RemoteShare;
            string path=item.Local?item.Profile.Folder:NasService.RemotePath(item.Profile.Host,item.Profile.RemoteShare);
            int x=e.Bounds.X+(int)(10*s),w=e.Bounds.Width-(int)(20*s),y=e.Bounds.Y+(int)(4*s);
            var flags=TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine;
            using(var bold=new Font(Font,FontStyle.Bold)) TextRenderer.DrawText(e.Graphics,title,bold,new Rectangle(x,y,w,(int)(20*s)),Theme.Ink,flags);
            TextRenderer.DrawText(e.Graphics,path,Font,new Rectangle(x,y+(int)(20*s),w,(int)(18*s)),Theme.Muted,flags);
            Color status=item.Status.State=="Connected" || item.Status.State=="Enabled"?Color.FromArgb(32,123,72):new[]{"Offline","Conflict","AuthRequired","Foreign"}.Contains(item.Status.State)?Color.FromArgb(173,55,46):item.Status.State=="Connecting"?Theme.Blue:Theme.Muted;
            TextRenderer.DrawText(e.Graphics,item.Status.Message,Font,new Rectangle(x,y+(int)(38*s),w,(int)(20*s)),status,flags);
            if((e.State&DrawItemState.Focus)!=0) e.DrawFocusRectangle();
        }
    }
    internal class NasListSection : Card {
        public NasListBox List; public FlowLayoutPanel Toolbar; public Label Empty;
        private readonly Func<float> factor; private bool arranging;
        public NasListSection(Func<float> scale) { factor=scale; Height=320; }
        protected override void OnLayout(LayoutEventArgs e) {
            base.OnLayout(e); if(arranging || factor==null || List==null || Toolbar==null || Theme.LayoutScaling) return; arranging=true;
            try { float s=factor(); int pad=(int)(22*s),width=Math.Max(1,Width-pad*2),h=(int)((List.Items.Count==0?28:Math.Min(3,List.Items.Count)*64)*s); List.SetBounds(pad,(int)(84*s),width,h); if(Empty!=null) Empty.Bounds=List.Bounds; var actions=(ActionBar)Toolbar; int toolbarHeight=actions.RequiredHeight(width); Toolbar.SetBounds(pad,List.Bottom+(int)(12*s),width,toolbarHeight); Height=Toolbar.Bottom+(int)(10*s); } finally { arranging=false; }
        }
    }
    public partial class MainForm {
        private NasListBox localNasList,remoteNasList;
        private Button createNasButton,remoteMountButton,remoteOpenButton,remoteStopButton;
        private Button listStopButton;
        private readonly Dictionary<string,NasItemState> localShareStates=new Dictionary<string,NasItemState>(StringComparer.OrdinalIgnoreCase);
        private bool rebindingNas,catalogRefreshing,catalogRefreshAgain,nasPreview;
        private readonly Timer catalogTimer=new Timer { Interval=250 };
        private NasListSection BuildNasList(DashboardColumns grid,bool local) {
            var card=new NasListSection(()=>UiScale) { Name=local?"LocalNasList":"RemoteNasList",Width=940 };
            grid.AddFull(card);
            TextAt(card,local?"Ổ đang chia sẻ trên máy này":"Ổ NAS đã lưu",14,true,Theme.Ink,22,18);
            Paragraph(card,local?"Chọn một NAS để xem trạng thái và thao tác. Thêm ổ mới bằng Chia sẻ mới.":"Các ổ đã gắn thành công sẽ tự kết nối khi mở tool. Tạm ngắt để dừng tự gắn.",22,49,32);
            var list=new NasListBox(()=>UiScale) { Name=local?"LocalNasItems":"RemoteNasItems",AccessibleName=local?"Danh sách NAS đang chia sẻ":"Danh sách ổ NAS đã lưu",Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            card.List=list; card.Controls.Add(list); var row=Buttons(card,274); ((ActionBar)row).StackWhenNarrow=true; card.Toolbar=row;
            card.Empty=Paragraph(card,"Chưa có ổ NAS trong danh sách.",22,84,28);
            if(local) {
                localNasList=list; Action(row,"Chia sẻ mới",true,()=>{ NewLocalShare(); return Task.FromResult(0); },"NewLocalNas");
                listStopButton=Action(row,"Dừng NAS",false,StopLocalShare,"StopSelectedNas",true);
                Action(row,"Làm mới",false,ImportLocalShares,"RefreshLocalNas");
                list.SelectedIndexChanged+=(a,b)=>{ if(rebindingNas || list.SelectedItem==null) return; var p=((NasListEntry)list.SelectedItem).Profile; managedShare=p.Share; nasFolder.Text=p.Folder; nasShare.Text=p.Share; nasUser.Text=p.User; nasReadOnly.Checked=p.ReadOnly; nasCreateUser.Checked=false; nasPassword.Clear(); UpdateNasActions(); nasServer.Text=p.Share+" · "+ShareState(p.Share).Message+"\n"+p.Folder+(string.IsNullOrWhiteSpace(p.User)?"\nChọn tài khoản NAS.":"\nTài khoản: "+NasService.LocalAccount(p.User)); };
            } else {
                remoteNasList=list; Action(row,"Thêm ổ NAS",true,()=>{ list.ClearSelected(); ClearRemotePassword(); nasHost.Text=""; remoteShare.Text="CYANNAS"; remoteUser.Text=""; var used=new HashSet<string>(nas.Connections().Select(p=>p.Letter).Concat(Environment.GetLogicalDrives().Select(p=>p.Substring(0,1))).Concat(new[]{settings.Letter}),StringComparer.OrdinalIgnoreCase); foreach(char letter in "NOPQRSTUVWXYZDEFGHIJKLM") if(!used.Contains(letter.ToString())) { nasLetter.SelectedItem=letter.ToString(); break; } nasConnection.Text="Nhập IP, tên NAS và tài khoản ở phần bên dưới, rồi bấm Lưu đăng nhập hoặc Gắn ổ."; UpdateNasActions(); return Task.FromResult(0); },"NewRemoteNas");
                remoteStopButton=Action(row,"Tạm ngắt",false,PauseRemoteShare,"DisconnectRemote",true);
                Action(row,"Làm mới",false,async()=>{ await nas.MaintainConnections(); await RefreshNasCatalogs(false); },"RefreshRemoteNas");
                list.SelectedIndexChanged+=(a,b)=>{ if(rebindingNas || list.SelectedItem==null) return; var p=((NasListEntry)list.SelectedItem).Profile; nasHost.Text=p.Host; remoteShare.Text=p.RemoteShare; nasLetter.SelectedItem=p.Letter; remoteUser.Text=p.RemoteUser; ClearRemotePassword(); nasConnection.Text=p.Letter+": · "+nas.ConnectionState(p.Letter).Message; UpdateNasActions(); };
            }
            card.PerformLayout(); return card;
        }
        private NasItemState ShareState(string name) { NasItemState state; return localShareStates.TryGetValue(name??"",out state)?state:new NasItemState { Key=name,State="Unknown",Message="Đang kiểm tra trạng thái" }; }
        private void FillNasList(NasListBox list,List<NasProfile> profiles,bool local) {
            if(list==null) return; var selected=list.SelectedItem as NasListEntry; string key=selected==null?null:(local?selected.Profile.Share:selected.Profile.Letter);
            rebindingNas=true; list.BeginUpdate();
            try {
                list.Items.Clear(); foreach(var p in profiles) { var current=local?ShareState(p.Share):nas.ConnectionState(p.Letter); var state=local || p.AutoConnect || current.State=="Paused" || current.State=="AuthRequired"?current:new NasItemState { Key=p.Letter,State="Saved",Message="Đã lưu · bấm Gắn ổ NAS để bật tự kết nối" }; var item=new NasListEntry { Local=local,Profile=p,Status=state }; list.Items.Add(item); if((local?p.Share:p.Letter)==key) list.SelectedItem=item; }
                var card=(NasListSection)list.Parent; list.Visible=profiles.Count>0; card.Empty.Visible=profiles.Count==0; card.PerformLayout();
                if(list.Items.Count==0) list.AccessibleDescription=local?"Chưa có NAS. Chọn Chia sẻ mới.":"Chưa có cấu hình NAS được lưu.";
            } finally { list.EndUpdate(); rebindingNas=false; }
        }
        private async Task RefreshNasCatalogs(bool probeLocal=true) {
            if(catalogRefreshing) { catalogRefreshAgain=true; return; } catalogRefreshing=true;
            try {
                do {
                    catalogRefreshAgain=false; var local=nas.LocalShares();
                    if(probeLocal) {
                        var names=local.Select(p=>p.Share).Concat(new[]{nasShare.Text.Trim()}).Distinct(StringComparer.OrdinalIgnoreCase).Where(n=>{ try { NasService.ValidateShare(n); return true; } catch { return false; } }).ToArray();
                        var states=nasPreview?names.Select(n=>localShareStates.ContainsKey(n)?localShareStates[n]:new NasItemState { Key=n,State="Stopped",Message="Đã dừng / chưa bật" }).ToArray():await Task.Run(()=>names.Select(NasService.ReadShareState).ToArray());
                        if(IsDisposed) return; foreach(var state in states) if(state.State!="Unknown" || !localShareStates.ContainsKey(state.Key)) localShareStates[state.Key]=state;
                    }
                    FillNasList(localNasList,local,true); var connections=nas.Connections(); FillNasList(remoteNasList,connections,false);
                    var selected=remoteNasList.SelectedItem as NasListEntry;
                    if(selected!=null) nasConnection.Text=selected.Profile.Letter+": · "+selected.Status.Message;
                    else if(connections.Count>0) nasConnection.Text=connections.Count+" ổ NAS đã lưu · "+connections.Count(p=>nas.ConnectionState(p.Letter).State=="Connected")+" đã kết nối. Chọn một ổ để xem chi tiết.";
                    UpdateNasActions();
                } while(catalogRefreshAgain && !IsDisposed);
            } finally { catalogRefreshing=false; }
        }
        private void QueueNasCatalogRefresh() { if(IsDisposed || Disposing) return; catalogTimer.Stop(); catalogTimer.Start(); }
        private async void NasCatalogRefreshTick(object sender,EventArgs e) {
            catalogTimer.Stop(); if(IsDisposed || Disposing) return;
            try { await RefreshNasCatalogs(); } catch(Exception error) { if(!IsDisposed) status.Text="Kiểm tra NAS: "+error.Message; }
        }
        private void UpdateNasActions() {
            if(createNasButton==null || busy) return;
            string name=nasShare.Text.Trim(); var state=ShareState(name);
            createNasButton.Enabled=state.State=="Stopped"; createNasButton.Text=state.State=="Enabled"?"Đã bật chia sẻ":"Bật chia sẻ NAS";
            bool sharing=state.State=="Enabled"; nasFolder.Enabled=!sharing; nasShare.Enabled=!sharing; nasReadOnly.Enabled=!sharing; nasUser.Enabled=!sharing; nasCreateUser.Enabled=!sharing;
            listStopButton.Enabled=localNasList.SelectedItem!=null && ShareState(managedShare).State=="Enabled";
            if(remoteMountButton!=null) {
                var p=NasValues(); var connection=nas.ConnectionState(p.Letter??""); bool same=nas.Connections().Any(x=>x.Letter==p.Letter && x.Host==p.Host && x.RemoteShare==p.RemoteShare);
                bool connected=same && connection.State=="Connected";
                remoteMountButton.Enabled=!connected; remoteMountButton.Text=connected?"Đã kết nối":"Gắn ổ NAS"; remoteOpenButton.Enabled=connected; remoteStopButton.Enabled=same;
            }
        }
        private void NewLocalShare() {
            localNasList.ClearSelected(); var used=new HashSet<string>(nas.LocalShares().Select(p=>p.Share).Concat(localShareStates.Where(x=>x.Value.State=="Enabled" || x.Value.State=="Foreign").Select(x=>x.Key)),StringComparer.OrdinalIgnoreCase); string name="CYANNAS"; int i=2; while(used.Contains(name)) name="CYANNAS"+(i++);
            nasFolder.Clear(); nasShare.Text=name; nasPassword.Clear(); if(used.Count>0) nasCreateUser.Checked=false; nasServer.Text="Chia sẻ mới · chọn thư mục và tài khoản, sau đó bấm Bật chia sẻ NAS."; QueueNasCatalogRefresh();
        }
        private async Task SaveRemoteLoginConfiguration() {
            var p=NasValues(); nas.ValidateSavedTarget(p); string password=remotePassword.Text;
            if(password.Length==0) { var saved=nas.LoadRemoteLogin(p.Host,p.RemoteShare); if(saved!=null && string.Equals(saved.User,p.RemoteUser,StringComparison.OrdinalIgnoreCase)) password=saved.Password; }
            bool first=nas.Connections().Count==0; nas.SaveRemoteLogin(p.Host,p.RemoteShare,p.RemoteUser,password); p.AutoConnect=false; nas.RememberConnection(p); nas.MarkConfigurationSaved(p.Letter); if(first && !nasPreview) { service.SetStartup(true); startup.Checked=true; } ClearRemotePassword();
            nasConnection.Text="Đã lưu đăng nhập mã hóa. Bấm Gắn ổ NAS để xác thực và bật tự kết nối."; status.Text=nasConnection.Text; await RefreshNasCatalogs(false);
        }
        private async Task ImportLocalShares() {
            if(!nasPreview) {
                var result=await nas.Admin("List",NasValues()); var details=(Dictionary<string,object>)result["details"];
                var imported=Core.Json.Deserialize<List<NasProfile>>(Core.Json.Serialize(details["shares"])); foreach(var p in imported) { nas.RememberShare(p); localShareStates[p.Share]=new NasItemState { Key=p.Share,State="Enabled",Message="Đang chia sẻ" }; }
            }
            await RefreshNasCatalogs();
        }
        private async Task StopLocalShare() {
            var result=await nas.Admin("Remove",ManagedNasValues()); localShareStates[managedShare]=new NasItemState { Key=managedShare,State="Stopped",Message="Đã dừng" }; nasServer.Text=managedShare+" · "+Convert.ToString(((Dictionary<string,object>)result["details"])["message"]); await RefreshNasCatalogs();
        }
        private async Task PauseRemoteShare() {
            var p=NasValues(); await nas.DisconnectSaved(p); await RefreshNasCatalogs(false); nasConnection.Text="Đã tạm ngắt "+p.Letter+":. Bấm Gắn ổ NAS để bật tự kết nối lại."; status.Text=nasConnection.Text;
        }
    }
}
