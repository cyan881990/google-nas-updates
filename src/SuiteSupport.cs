using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
namespace GoogleNas {
 public static class CapacityInfo {
  public static string Bytes(double value){string[] units={"B","KB","MB","GB","TB","PB"};int unit=0;while(value>=1024&&unit<units.Length-1){value/=1024;unit++;}return value.ToString("0.#")+" "+units[unit];}
  public static string FromQuota(Dictionary<string,object> quota){object total,used,free;bool t=quota.TryGetValue("total",out total),u=quota.TryGetValue("used",out used),f=quota.TryGetValue("free",out free);if(!t&&!u&&!f)return "Không cung cấp quota";return (u?Bytes(Convert.ToDouble(used))+" đã dùng":"Đã dùng: chưa rõ")+(t?" / "+Bytes(Convert.ToDouble(total)):" / không rõ tổng")+(f?" · Trống "+Bytes(Convert.ToDouble(free)):"");}
  public static double LocalRatio(string folder){try{var d=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder)));return d.TotalSize==0?-1:(double)(d.TotalSize-d.TotalFreeSpace)/d.TotalSize;}catch{return -1;}}
  public static string Local(string folder){try{var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder)));return Bytes(drive.TotalSize-drive.TotalFreeSpace)+" / "+Bytes(drive.TotalSize)+" · Trống "+Bytes(drive.AvailableFreeSpace);}catch{return "Chưa đọc dung lượng";}}
 }
 public static class SuiteStartup {
  public static bool Enabled(bool server,bool unified=false){using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return key!=null&&key.GetValue(unified||SuiteIdentity.Unified?"CyanNas":server?"CyanNasServer":"CyanNasClient")!=null;}
  public static void Set(bool server,bool enabled,bool unified=false){string id=unified||SuiteIdentity.Unified?"CyanNas":server?"CyanNasServer":"CyanNasClient";using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(enabled)key.SetValue(id,Core.Quote(Path.Combine(Application.StartupPath,id+".exe"))+" --tray");else key.DeleteValue(id,false);}}
 }
 public static class SuiteMigration {
  public static async Task Import(bool server,string home,StorageEngine engine){
   string marker=Path.Combine(home,"migration-v2.done");if(File.Exists(marker))return;string old=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GoogleNas");
   if(Directory.Exists(old)){
    foreach(string name in server?new[]{"nas-shares.json","nas.json","nas-passwords.dat"}:new[]{"nas-passwords.dat"}){string src=Path.Combine(old,name),dst=Path.Combine(home,name);if(File.Exists(src)&&!File.Exists(dst))File.Copy(src,dst);}
    if(!server&&engine.Store.Load().Count==0){var legacy=new NasService(old);foreach(var item in legacy.Connections()){try{var login=legacy.LoadRemoteLogin(item.Host,item.RemoteShare);if(login==null)continue;string username=login.User,domain="";int split=username.IndexOf('\\');if(split>=0){domain=username.Substring(0,split);username=username.Substring(split+1);}var p=new StorageConnection{Name=item.RemoteShare,Provider="smb",Path=item.RemoteShare,Letter=item.Letter,AutoConnect=item.AutoConnect,Verified=true};p.Options["host"]=item.Host;p.Options["user"]=username;p.Options["domain"]=domain;p.Options["pass"]=login.Password;engine.Store.Put(p);}catch{}}}
   }
   if(!server&&File.Exists(Path.Combine(old,"settings.json"))&&File.Exists(Path.Combine(old,"rclone.conf"))){try{var settings=Core.Json.Deserialize<Settings>(File.ReadAllText(Path.Combine(old,"settings.json")));if(!engine.Store.Load().Any(x=>x.Provider=="drive")){var options=await engine.ReadExternalConfig(Path.Combine(old,"rclone.conf"),settings.Remote);if(options!=null&&options.ContainsKey("type")&&options["type"]=="drive"){var connection=new StorageConnection{Name="Google Drive",Provider="drive",Letter=settings.Letter,Path=settings.Folder,AutoConnect=settings.AutoMount,Verified=true,Options=options};await engine.Configure(connection);engine.Store.Put(connection);}}}catch{File.WriteAllText(Path.Combine(home,"migration-note.txt"),"Google Drive cũ vẫn được giữ. Nếu chưa thấy trong Client, thêm Google Drive và cấp quyền lại.");}}
   if(File.Exists(Path.Combine(old,"settings.json"))&&!File.Exists(Path.Combine(home,"suite-preferences.json"))){try{SuiteUpdate.SetAutomatic(home,Core.Json.Deserialize<Settings>(File.ReadAllText(Path.Combine(old,"settings.json"))).AutoUpdate);}catch{}}
   File.WriteAllText(marker,"Original settings retained. "+DateTime.UtcNow.ToString("o"));
  }
 }
 public static class SuiteUpdate {
  public static bool Automatic(string home){try{string file=Path.Combine(home,"suite-preferences.json");return File.Exists(file)&&Core.Json.Deserialize<Dictionary<string,bool>>(File.ReadAllText(file))["AutoUpdate"];}catch{return false;}}
  public static void SetAutomatic(string home,bool enabled){string file=Path.Combine(home,"suite-preferences.json");File.WriteAllText(file+".new",Core.Json.Serialize(new Dictionary<string,bool>{{"AutoUpdate",enabled}}));if(File.Exists(file))File.Replace(file+".new",file,null);else File.Move(file+".new",file);}
  public static async Task<bool> Available(){return await CyanRelease.Check()!=null;}
  public static Task DownloadAndLaunch(IWin32Window owner,StorageEngine engine){return CyanRelease.Offer(owner);}
 }
 public static class SuiteProgram {
  [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern int RegisterWindowMessage(string name);
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool PostMessage(IntPtr w,int m,IntPtr p,IntPtr l);
  public static readonly int ShowMessage=RegisterWindowMessage("CYANNAS."+SuiteIdentity.Id+"."+Environment.UserName);
  [System.Runtime.InteropServices.DllImport("shell32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern int SetCurrentProcessExplicitAppUserModelID(string id);
  public static int Run(string[] args){SetCurrentProcessExplicitAppUserModelID("CYAN."+SuiteIdentity.Id);
   if(args.Contains("--health-check")){try{StorageProviders.Find("sftp");return File.Exists(Path.Combine(Application.StartupPath,"runtime","rclone.exe"))?0:1;}catch{return 1;}}
   if(args.Length==2&&args[0]=="--render-connection"){string output=Path.GetFullPath(args[1]);using(var form=new StorageDialog(new StorageEngine(Path.Combine(Path.GetDirectoryName(output),"preview-connections")))){form.PreviewProvider("sftp");form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-10000,-10000);form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(output);}}return 0;}
   if(args.Length==2&&args[0]=="--render-preview"){string output=Path.GetFullPath(args[1]);using(Form form=SuiteIdentity.Unified?(Form)new UnifiedSuiteForm(true,false,true,Path.Combine(Path.GetDirectoryName(output),"preview-unified")):new SuiteForm(SuiteIdentity.Server,false,true,Path.Combine(Path.GetDirectoryName(output),"preview-"+SuiteIdentity.Id))){form.ShowInTaskbar=false;form.Location=new Point(-10000,-10000);form.StartPosition=FormStartPosition.Manual;form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(output);}}return 0;}
   bool created;using(var mutex=new Mutex(true,"Local\\"+SuiteIdentity.Id+"-"+Environment.UserName,out created)){if(!created){if(!args.Contains("--tray"))PostMessage(new IntPtr(0xffff),ShowMessage,new IntPtr(args.Contains("--client")?2:args.Contains("--server")?1:0),IntPtr.Zero);return 0;}try{if(SuiteIdentity.Unified)Application.Run(new UnifiedSuiteForm(!args.Contains("--client"),args.Contains("--tray")));else Application.Run(new SuiteForm(SuiteIdentity.Server,args.Contains("--tray")));return 0;}catch(Exception e){SuiteNotice.Show(e.Message,SuiteIdentity.Name);return 1;}finally{mutex.ReleaseMutex();}}
  }
 }
}
