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
 [STAThread]static int Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
 foreach(bool server in new[]{true,false})using(var form=new SuiteForm(server,false,true,Path.Combine(root,server?"server-data":"client-data"))){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-10000,-10000);form.Show();Application.DoEvents();form.MinimumSize=Size.Empty;form.MaximumSize=new Size(8000,8000);
  foreach(int width in new[]{1024,1366,1920}){form.Size=new Size(width,1000);Application.DoEvents();foreach(var c in All(form)){if(c.Visible&&c is Button&&c.Parent is FlowLayoutPanel){if(c.Right>c.Parent.ClientSize.Width+1)throw new Exception("Action clipped: "+c.Text+" width "+width);}}
   using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(root,(server?"server":"client")+"-"+width+".png"));}
  }
  if(server){var grid=All(form).OfType<DataGridView>().Single();grid.ClearSelection();grid.Rows[0].Selected=true;Application.DoEvents();var enable=All(form).OfType<Button>().FirstOrDefault(b=>b.Text=="Đã bật chia sẻ");if(enable==null||enable.Enabled)throw new Exception("Enabled server share must not be enabled twice");}
 }
 var engine=new StorageEngine(Path.Combine(root,"dialog-data"));using(var dialog=new StorageDialog(engine)){dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-10000,-10000);dialog.Show();Application.DoEvents();foreach(var p in StorageProviders.All){dialog.PreviewProvider(p.Id);Application.DoEvents();foreach(var button in All(dialog).OfType<Button>().Where(b=>b.Visible&&b.Parent is FlowLayoutPanel)){if(button.Right>button.Parent.ClientSize.Width+1)throw new Exception("Dialog action clipped: "+button.Text);}}dialog.PreviewProvider("sftp");Application.DoEvents();using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(Path.Combine(root,"add-connection.png"));}}
 Console.WriteLine("Suite native UI: both role dashboards at 1024/1366/1920, enabled-share guard, all 13 configuration forms and action bounds passed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
