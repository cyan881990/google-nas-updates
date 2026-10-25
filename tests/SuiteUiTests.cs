using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using GoogleNas;
class SuiteUiTests {
 static List<Control> All(Control p){var list=new List<Control>();foreach(Control c in p.Controls){list.Add(c);list.AddRange(All(c));}return list;}
 static void CheckDialog(SuiteWindow dialog,string root,string name,int width){
  dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-10000,-10000);dialog.Show();dialog.Size=new Size(width,dialog.Height);Application.DoEvents();
  if(dialog.FormBorderStyle!=FormBorderStyle.None||dialog.ShowInTaskbar||dialog.Icon==null)throw new Exception("Dialog chrome differs from app: "+name);
  foreach(var button in All(dialog).OfType<Button>().Where(b=>b.Visible))if(button.Right>button.Parent.ClientSize.Width+1||button.Bottom>button.Parent.ClientSize.Height+1)throw new Exception("Dialog button clipped: "+name+" / "+button.Text);
  using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(Path.Combine(root,name+"-"+width+".png"));}
 }
 [STAThread]static int Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
 foreach(bool server in new[]{true,false})using(var form=new SuiteForm(server,false,true,Path.Combine(root,server?"server-data":"client-data"))){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-10000,-10000);form.Show();Application.DoEvents();form.MinimumSize=Size.Empty;form.MaximumSize=new Size(8000,8000);
  var drives=All(form).OfType<DataGridView>().Single();
  if(form.BackColor!=SuiteTheme.Background||drives.ColumnCount!=9||drives.Columns.Cast<DataGridViewColumn>().Where(c=>c.Index>=5).Any(c=>!c.Visible))throw new Exception("Drive list must expose four row actions in the dark shell");
  if(server){string before=Convert.ToString(drives.Rows[0].Cells[3].Value);drives.Rows[0].Cells[3].Value="Localized status text";if(!form.RowConnected(drives.Rows[0]))throw new Exception("Server action state must use actual share state, not message wording");drives.Rows[0].Cells[3].Value=before;}
  foreach(int width in new[]{1024,1366,1920}){
   form.Size=new Size(width,1000);Application.DoEvents();
   for(int page=0;page<5;page++){
    All(form).OfType<Button>().Single(b=>b.Name=="PageNav"+page).PerformClick();Application.DoEvents();
    if(form.ActivePage!=page)throw new Exception("Navigation did not change page");
    foreach(var c in All(form))if(c.Visible&&c is Button&&c.Parent is FlowLayoutPanel&&c.Right>c.Parent.ClientSize.Width+1)throw new Exception("Action clipped: "+c.Text+" page "+page+" width "+width+" bounds "+c.Bounds+" parent "+c.Parent.ClientSize);
    foreach(string name in new[]{"NetworkPage","SettingsPage","ActivityPage"}){
     var panel=All(form).Single(c=>c.Name==name);
     bool expected=name=="NetworkPage"?page==3:name=="SettingsPage"?page==4:!server&&page==2;
     if(panel.Visible!=expected)throw new Exception("Page content leaked: "+name+" page "+page);
    }
    if(server){
     var fields=All(form).OfType<TextBox>().ToList();
     if(fields.Single(t=>t.AccessibleName=="Tên chia sẻ").Visible!=(page==1))throw new Exception("Share form leaked across pages");
     if(fields.Single(t=>t.AccessibleName=="Mật khẩu NAS").Visible!=(page==2))throw new Exception("Account form leaked across pages");
    }
    if(width==1366||page==0)using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(root,(server?"server":"client")+(page==0?"":"-page"+page)+"-"+width+".png"));}
   }
  }
  if(server){
   form.NavigatePage(1);var input=All(form).OfType<TextBox>().Single(t=>t.AccessibleName=="Tên chia sẻ");
   input.Text="PAGE-DRAFT";form.NavigatePage(2);form.NavigatePage(4);form.NavigatePage(1);
   if(input.Text!="PAGE-DRAFT")throw new Exception("Navigation discarded unsaved share configuration");
   var busyField=typeof(SuiteForm).GetField("busy",BindingFlags.NonPublic|BindingFlags.Instance);busyField.SetValue(form,true);All(form).OfType<Button>().Single(b=>b.Name=="ToolbarAdd").PerformClick();if(input.Text!="PAGE-DRAFT")throw new Exception("Toolbar Add replaced a draft during an active operation");busyField.SetValue(form,false);
  }
  if(server){var grid=All(form).OfType<DataGridView>().Single();grid.ClearSelection();grid.Rows[0].Selected=true;Application.DoEvents();var enable=All(form).OfType<Button>().FirstOrDefault(b=>b.Text=="Đã bật chia sẻ");if(enable==null||enable.Enabled)throw new Exception("Enabled server share must not be enabled twice");}
 }
 var engine=new StorageEngine(Path.Combine(root,"dialog-data"));using(var dialog=new StorageDialog(engine)){dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-10000,-10000);dialog.Show();Application.DoEvents();foreach(var p in StorageProviders.All){dialog.PreviewProvider(p.Id);Application.DoEvents();foreach(var button in All(dialog).OfType<Button>().Where(b=>b.Visible&&b.Parent is FlowLayoutPanel)){if(button.Right>button.Parent.ClientSize.Width+1)throw new Exception("Dialog action clipped: "+button.Text);}}dialog.PreviewProvider("sftp");Application.DoEvents();using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(Path.Combine(root,"add-connection.png"));}}
 foreach(int width in new[]{480,640}){
  using(var notice=new SuiteNoticeWindow("Không thể kết nối máy chủ. Kiểm tra địa chỉ, tài khoản và mạng rồi thử lại.\r\n\r\nThông báo dài vẫn đọc được và không đẩy nút ra ngoài cửa sổ.","Kết nối chưa hoàn tất",MessageBoxButtons.OK,MessageBoxIcon.Warning)){CheckDialog(notice,root,"notice",width);notice.CanDismiss=()=>false;All(notice).OfType<Button>().Single(b=>b.Name=="DialogClose").PerformClick();if(notice.IsDisposed)throw new Exception("Busy dialog dismissed");notice.CanDismiss=()=>true;notice.Dismiss();if(notice.DialogResult!=DialogResult.Cancel)throw new Exception("Close must cancel");}
  using(var confirm=new SuiteNoticeWindow("Bỏ cấu hình đã chọn? File và cache vẫn được giữ.","Bỏ lưu kết nối",MessageBoxButtons.YesNo,MessageBoxIcon.Question)){CheckDialog(confirm,root,"confirmation",width);confirm.AcceptButton.PerformClick();if(confirm.DialogResult!=DialogResult.No)throw new Exception("Confirmation must default to cancel");}
  using(var prompt=new SuitePromptWindow("Cấp quyền · Google Drive","Chọn không gian lưu trữ sau khi đăng nhập trình duyệt.\r\n\r\nNội dung hướng dẫn có thể cuộn nếu tài khoản có nhiều lựa chọn.",new[]{"My Drive","Shared Drive"},"My Drive")){CheckDialog(prompt,root,"authorization",width);if(prompt.Choice.Text!="My Drive"||prompt.Choice.Items.Count!=2)throw new Exception("OAuth choices lost");prompt.Choice.Text="Shared Drive";prompt.AcceptButton.PerformClick();if(prompt.DialogResult!=DialogResult.OK||prompt.Choice.Text!="Shared Drive")throw new Exception("OAuth result lost");}
 }
 using(var picker=new StorageDialog(engine)){picker.StartPosition=FormStartPosition.Manual;picker.Location=new Point(-10000,-10000);picker.Show();Application.DoEvents();picker.PreviewProvider("sftp");var host=All(picker).OfType<TextBox>().Single(t=>t.AccessibleName=="Máy chủ");host.Text="draft.example";foreach(string group in new[]{"NAS","Cloud","Tất cả"}){All(picker).OfType<Button>().Single(b=>b.Text==group).PerformClick();Application.DoEvents();int count=All(picker).OfType<StorageProviderTile>().Count(t=>t.Visible);if(count!=(group=="NAS"?5:group=="Cloud"?8:13)||host.Text!="draft.example")throw new Exception("Provider filter loses draft or services: "+group);}}
 using(var unified=new UnifiedSuiteForm(true,false,true,Path.Combine(root,"unified-data"))){unified.StartPosition=FormStartPosition.Manual;unified.Location=new Point(-10000,-10000);unified.Show();Application.DoEvents();unified.MinimumSize=Size.Empty;unified.MaximumSize=new Size(8000,8000);
  if(unified.ServerView.TopLevel||unified.ClientView.TopLevel||unified.Text!="CYAN NAS")throw new Exception("Roles must be hosted inside one application window");
  foreach(int width in new[]{1024,1366,1920}){unified.Size=new Size(width,1000);Application.DoEvents();foreach(bool server in new[]{true,false}){All(unified).OfType<Button>().Single(b=>b.Name==(server?"ServerTab":"ClientTab")).PerformClick();Application.DoEvents();if(unified.ServerSelected!=server||unified.ServerView.Visible!=server||unified.ClientView.Visible==server)throw new Exception("Main role tab visibility incorrect");foreach(var button in All(unified).OfType<Button>().Where(b=>b.Visible))if(button.Parent is FlowLayoutPanel&&button.Right>button.Parent.ClientSize.Width+1)throw new Exception("Unified action clipped: "+button.Text);using(var bmp=new Bitmap(unified.Width,unified.Height)){unified.DrawToBitmap(bmp,new Rectangle(Point.Empty,unified.Size));bmp.Save(Path.Combine(root,"unified-"+(server?"server":"client")+"-"+width+".png"));}}}
  unified.SelectRole(true);unified.ServerView.NavigatePage(1);var draft=All(unified.ServerView).OfType<TextBox>().Single(t=>t.AccessibleName=="Tên chia sẻ");draft.Text="KEEP-SERVER-DRAFT";unified.SelectRole(false);var clientGrid=All(unified.ClientView).OfType<DataGridView>().Single();clientGrid.ClearSelection();clientGrid.Rows[2].Selected=true;unified.SelectRole(true);if(draft.Text!="KEEP-SERVER-DRAFT"||unified.ServerView.ActivePage!=1)throw new Exception("Role switching discarded server draft/page");unified.SelectRole(false);if(clientGrid.SelectedRows[0].Index!=2)throw new Exception("Role switching discarded client selection");
 }
 Console.WriteLine("Suite native UI: one CYAN NAS window, persistent SERVER/CLIENT tabs, role switching, drive actions, source filters and dialogs passed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
