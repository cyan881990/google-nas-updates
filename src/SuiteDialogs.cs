using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace GoogleNas {
 public class StorageProviderTile : MacButton {
  private readonly StorageProvider provider;
  public StorageProviderTile(StorageProvider p){provider=p;Text=p.Name;AccessibleName=p.Name;AutoSize=false;MinimumSize=Size.Empty;Size=new Size(130,84);Cursor=Cursors.Hand;BackColor=SuiteTheme.Surface;}
  protected override void OnPaint(PaintEventArgs e){float s=DeviceDpi/96f;e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.Clear(Parent==null?SuiteTheme.Background:Parent.BackColor);bool selected=BackColor!=SuiteTheme.Surface;using(var path=Round(new RectangleF(.5f,.5f,Width-1,Height-1),5*s)){using(var fill=new SolidBrush(selected?Color.FromArgb(49,68,92):SuiteTheme.Surface))e.Graphics.FillPath(fill,path);using(var pen=new Pen(selected?SuiteTheme.Blue:SuiteTheme.Border,selected?1.4f:1))e.Graphics.DrawPath(pen,path);}using(var icon=SuiteGlyph.Provider(provider.Id,(int)(32*s)))e.Graphics.DrawImage(icon,(int)(9*s),(int)(5*s));using(var font=new Font("Segoe UI",8))TextRenderer.DrawText(e.Graphics,provider.Name,font,new Rectangle((int)(10*s),(int)(42*s),Width-(int)(18*s),Height-(int)(44*s)),selected?SuiteTheme.Blue:SuiteTheme.Ink,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
 }
 public class StorageDialog : SuiteWindow {
  readonly StorageEngine engine;readonly StorageConnection original;readonly string draftId;readonly FlowLayoutPanel fields=new FlowLayoutPanel();readonly FlowLayoutPanel providers=new FlowLayoutPanel();
  readonly Dictionary<string,Control> inputs=new Dictionary<string,Control>();readonly TextBox name=new TextBox(),path=new TextBox();readonly ComboBox letter=new ComboBox();readonly CheckBox automatic=new SuiteCheckBox(),readOnly=new SuiteCheckBox();
  readonly Label hint=new Label(),state=new Label();readonly Button test,save,offline;string provider="smb";bool working;
  public StorageConnection Result;
  public StorageDialog(StorageEngine e,StorageConnection existing=null) {
   engine=e;original=existing;draftId=existing==null?Guid.NewGuid().ToString("N"):existing.Id;provider=existing==null?"smb":existing.Provider;Text=existing==null?"Thêm kết nối":"Chỉnh sửa kết nối";Icon=SuiteGlyph.Icon(false);Font=new Font("Segoe UI",9.5f);BackColor=SuiteTheme.Background;StartPosition=FormStartPosition.CenterParent;Size=new Size(1060,850);MinimumSize=new Size(900,680);FormBorderStyle=FormBorderStyle.None;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
   var outer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(22)};outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,310));outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));outer.RowStyles.Add(new RowStyle(SizeType.Absolute,68));outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));outer.RowStyles.Add(new RowStyle(SizeType.Absolute,85));Body.Padding=Padding.Empty;Body.Controls.Add(outer);CanDismiss=()=>!working;
   var header=new Panel{Dock=DockStyle.Fill};var heading=SuiteForm.Label(Text,20,true);heading.Location=new Point(0,8);header.Controls.Add(heading);
   var filters=new FlowLayoutPanel{Width=354,Height=38,Anchor=AnchorStyles.Top|AnchorStyles.Right,WrapContents=false};
   foreach(string group in new[]{"Tất cả","Cloud","NAS"}){string category=group;var filter=SuiteForm.Button(group);filter.Width=108;filter.Click+=(a,b)=>{foreach(Button item in providers.Controls){string id=(string)item.Tag;bool network=new[]{"smb","ftp","ftps","sftp","webdav"}.Contains(id);item.Visible=category=="Tất cả"||(category=="NAS"?network:!network);}foreach(Button item in filters.Controls){item.BackColor=item.Text==category?SuiteTheme.Blue:SuiteTheme.Surface;item.ForeColor=item.Text==category?Color.White:SuiteTheme.Ink;}};filters.Controls.Add(filter);}
   filters.Controls[0].BackColor=SuiteTheme.Blue;filters.Controls[0].ForeColor=Color.White;header.Controls.Add(filters);header.SizeChanged+=(a,b)=>filters.Location=new Point(Math.Max(300,header.Width-filters.Width),8);
   outer.Controls.Add(header,0,0);outer.SetColumnSpan(header,2);
   providers.Dock=DockStyle.Fill;providers.Margin=Padding.Empty;providers.AutoScroll=true;providers.Padding=new Padding(0,0,14,0);outer.Controls.Add(providers,0,1);
   foreach(var p in StorageProviders.All) { var b=new StorageProviderTile(p);b.Margin=new Padding(0,0,8,10);b.Tag=p.Id;b.Enabled=existing==null||existing.Provider==p.Id;b.Click+=(a,z)=>{provider=p.Id;Render();};providers.Controls.Add(b); }
   fields.Dock=DockStyle.Fill;fields.FlowDirection=FlowDirection.TopDown;fields.WrapContents=false;fields.AutoScroll=true;fields.BackColor=SuiteTheme.Surface;fields.Padding=new Padding(18);outer.Controls.Add(fields,1,1);fields.SizeChanged+=(a,b)=>{foreach(Control c in fields.Controls)c.Width=Math.Max(80,fields.ClientSize.Width-50);};
   var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,458));outer.Controls.Add(footer,0,2);outer.SetColumnSpan(footer,2);state.Dock=DockStyle.Fill;state.ForeColor=SuiteTheme.Muted;state.Text="Cấu hình được mã hóa theo tài khoản Windows. Kiểm tra thật trước khi lưu.";footer.Controls.Add(state,0,0);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(0,18,0,0),WrapContents=false};test=SuiteForm.Button("Kiểm tra",false);save=SuiteForm.Button("Lưu & gắn ổ",true);test.Click+=async(a,b)=>await Execute(false);save.Click+=async(a,b)=>await Execute(true);offline=SuiteForm.Button("Lưu cấu hình",false);offline.Click+=(a,b)=>{try{var p=Values();p.Verified=false;p.AutoConnect=false;engine.Store.Put(p);Result=p;DialogResult=DialogResult.OK;Close();}catch(Exception error){SuiteNotice.Show(this,error.Message);}};actions.Controls.Add(offline);actions.Controls.Add(test);actions.Controls.Add(save);footer.Controls.Add(actions,1,0);
   for(char c='D';c<='Z';c++)letter.Items.Add(c.ToString());letter.DropDownStyle=ComboBoxStyle.DropDownList;letter.SelectedItem=existing==null?FreeLetter():existing.Letter;
   name.Text=existing==null?"":existing.Name;path.Text=existing==null?"":existing.Path;automatic.Text="Tự kết nối khi mở Client / cùng Windows";automatic.Checked=existing==null||existing.AutoConnect;readOnly.Text="Chỉ đọc";readOnly.Checked=existing!=null&&existing.ReadOnly;Render();FormClosing+=(a,b)=>{if(working)b.Cancel=true;};
  }
  public void PreviewProvider(string id){provider=id;Render();}
  private string FreeLetter() { var used=engine.Store.Load().Select(x=>x.Letter).Concat(Environment.GetLogicalDrives().Select(x=>x.Substring(0,1))).ToArray();foreach(char c in "NOPQRSTUVWXYZGHIJKLMDEF")if(!used.Contains(c.ToString()))return c.ToString();return "N"; }
  private void Field(string caption,Control input,bool file=false) {
   float scale=IsHandleCreated?DeviceDpi/96f:1;var panel=new Panel{Width=fields.ClientSize.Width-50,Height=(int)(68*scale),Margin=new Padding(0,0,0,(int)(5*scale))};var label=SuiteForm.Label(caption,9,true);label.SetBounds(0,0,panel.Width,(int)(20*scale));panel.Controls.Add(label);
   bool secret=input is TextBox&&((TextBox)input).UseSystemPasswordChar;
   input.AccessibleName=caption;var surface=new SuiteInputSurface(input){Location=new Point(0,(int)(25*scale)),Size=new Size(panel.Width-(int)((file||secret?78:0)*scale),(int)(34*scale)),Padding=new Padding((int)(10*scale),(int)(5*scale),(int)(10*scale),(int)(5*scale)),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};panel.Controls.Add(surface);
   if(file){var browse=SuiteForm.Button("Chọn…",false);browse.SetBounds(panel.Width-(int)(70*scale),(int)(25*scale),(int)(70*scale),(int)(34*scale));browse.Anchor=AnchorStyles.Top|AnchorStyles.Right;browse.Click+=(a,b)=>{using(var picker=new OpenFileDialog())if(picker.ShowDialog(this)==DialogResult.OK)input.Text=picker.FileName;};panel.Controls.Add(browse);}
   if(secret){var reveal=SuiteForm.Button("Hiện",false);reveal.SetBounds(panel.Width-(int)(70*scale),(int)(25*scale),(int)(70*scale),(int)(34*scale));reveal.Anchor=AnchorStyles.Top|AnchorStyles.Right;reveal.Click+=(a,b)=>{var box=(TextBox)input;box.UseSystemPasswordChar=!box.UseSystemPasswordChar;reveal.Text=box.UseSystemPasswordChar?"Hiện":"Ẩn";};panel.Controls.Add(reveal);}
   fields.Controls.Add(panel);
  }
  private void Render() {
   fields.SuspendLayout();fields.Controls.Clear();inputs.Clear();var p=StorageProviders.Find(provider);if(original==null&&(string.IsNullOrWhiteSpace(name.Text)||StorageProviders.All.Any(x=>x.Name==name.Text)))name.Text=p.Name;foreach(Button b in providers.Controls)b.BackColor=(string)b.Tag==provider?Color.FromArgb(49,68,92):SuiteTheme.Surface;
   var title=SuiteForm.Label(p.Name,17,true);title.Height=(int)(36*(IsHandleCreated?DeviceDpi/96f:1));fields.Controls.Add(title);hint.AutoSize=false;hint.Text=p.Hint;hint.ForeColor=SuiteTheme.Muted;hint.Height=(int)(62*(IsHandleCreated?DeviceDpi/96f:1));hint.Margin=new Padding(0,0,0,10);fields.Controls.Add(hint);
   Field("Tên hiển thị",name);Field("Ký tự ổ Windows",letter);Field("Thư mục gốc / Share / Bucket",path);
   foreach(var f in p.Fields){Control control;if(f.Key=="tls_mode"){var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};combo.Items.AddRange(new object[]{"Explicit TLS","Implicit TLS"});combo.SelectedItem=original==null?f.Default:original.Get(f.Key);if(combo.SelectedIndex<0)combo.SelectedIndex=0;control=combo;}
    else {var box=new TextBox{UseSystemPasswordChar=f.Secret};box.Text=original==null?f.Default:original.Get(f.Key);control=box;}
    inputs[f.Key]=control;Field(f.Label,control,f.File);
   }
   if(provider=="ftps"){var mode=(ComboBox)inputs["tls_mode"];mode.SelectedIndexChanged+=(a,b)=>{if(mode.Text=="Implicit TLS"&&inputs["port"].Text=="21")inputs["port"].Text="990";else if(mode.Text=="Explicit TLS"&&inputs["port"].Text=="990")inputs["port"].Text="21";};}
   if(p.OAuth){var login=SuiteForm.Button("Đăng nhập lại",false);login.Click+=(a,b)=>{if(SuiteNotice.Show(this,"Bỏ phiên cấp quyền của kết nối này và đăng nhập lại? Không xóa dữ liệu cloud.","Đăng nhập lại",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;try{engine.ResetAuthorization(Values());state.Text="Đã bỏ phiên cũ. Bấm Kiểm tra hoặc Lưu & gắn ổ để cấp quyền lại.";}catch(Exception error){SuiteNotice.Show(this,error.Message,"Chưa thể đăng nhập lại");}};fields.Controls.Add(login);}
   automatic.AutoSize=true;readOnly.AutoSize=true;fields.Controls.Add(automatic);fields.Controls.Add(readOnly);fields.ResumeLayout(true);foreach(Control c in fields.Controls)c.Width=Math.Max(80,fields.ClientSize.Width-50);
  }
  private StorageConnection Values() { var p=new StorageConnection{Id=draftId,Provider=provider,Name=name.Text.Trim(),Letter=Convert.ToString(letter.SelectedItem),Path=path.Text.Trim(),AutoConnect=automatic.Checked,ReadOnly=readOnly.Checked};foreach(var item in inputs)p.Options[item.Key]=item.Value.Text;return p; }
  private async Task Execute(bool mount) {
   if(working)return;working=true;test.Enabled=save.Enabled=offline.Enabled=false;providers.Enabled=fields.Enabled=false;
   try {var p=Values();StorageProviders.Validate(p);state.Text="Đang cấu hình và kiểm tra kết nối thật… Nếu cần, hãy cấp quyền trong trình duyệt.";await engine.Configure(p,Ask);await engine.Test(p);state.Text="Kết nối thành công. Đọc được danh sách tại thư mục gốc.";
    if(mount){p.Verified=true;engine.Store.Put(p);await engine.Connect(p);Result=p;DialogResult=DialogResult.OK;working=false;Close();}
   }catch(Exception e){state.Text=e.Message;SuiteNotice.Show(this,e.Message,"Kết nối chưa hoàn tất",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{working=false;test.Enabled=save.Enabled=offline.Enabled=true;providers.Enabled=fields.Enabled=true;}
  }
  private Task<string> Ask(Dictionary<string,object> question) {
   var option=(Dictionary<string,object>)question["Option"];string answer;
   var choices=new List<string>();object examples;
   if(option.TryGetValue("Examples",out examples)&&examples is IEnumerable)foreach(var x in (IEnumerable)examples){var d=x as Dictionary<string,object>;if(d!=null&&d.ContainsKey("Value"))choices.Add(Convert.ToString(d["Value"]));}
   object value;string selected=option.TryGetValue("Default",out value)?Convert.ToString(value):"";
   using(var dialog=new SuitePromptWindow("Cấp quyền · "+StorageProviders.Find(provider).Name,Convert.ToString(option.ContainsKey("Help")?option["Help"]:"Chọn thông tin đăng nhập."),choices,selected)){
    answer=dialog.ShowDialog(this)==DialogResult.OK?dialog.Choice.Text:null;
   }return Task.FromResult(answer);
  }
 }
}
