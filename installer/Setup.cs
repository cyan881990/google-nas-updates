using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using GoogleNas;

[assembly: AssemblyTitle("CYAN NAS Setup")]
[assembly: AssemblyProduct("CYAN NAS Setup")]
namespace GoogleNasInstaller {
    static class Entry {
        [STAThread] static int Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try {
                if(args.Length==2 && args[0]=="--verify-payload") { SetupEngine.Extract(Path.GetFullPath(args[1])); return 0; }
                if(args.Length==2 && args[0]=="--test-install-files") { string target=Path.GetFullPath(args[1]); string stage=target+"-stage"; SetupEngine.Extract(stage); SetupEngine.InstallFiles(stage,target); return 0; }
                if(args.Length==2 && args[0]=="--render-preview") {
                    using(var form=new SetupForm()) { form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-10000,-10000); form.Show(); Application.DoEvents(); using(var b=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(b,new Rectangle(0,0,form.Width,form.Height)); b.Save(args[1]); } } return 0;
                }
                if(args.Length==1 && args[0]=="--uninstall") { SetupEngine.Uninstall(); return 0; }
                using(var form=new SetupForm()) Application.Run(form); return 0;
            } catch(Exception e) { if(args.Length>1) File.WriteAllText(args[1]+".error.txt",e.ToString()); else MessageBox.Show(e.Message,"CYAN NAS Setup",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
        }
    }
    public static class SetupEngine {
        public class PendingInstallException : Exception { public PendingInstallException(string message) : base(message) { } }
        public static readonly string Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","GoogleNas");
        public static bool Reboot;
        public static string ShortcutWarning;
        public static bool Active() { return Process.GetProcessesByName("GoogleNas").Any(p=>p.SessionId==Process.GetCurrentProcess().SessionId); }
        public static int FrameworkRelease() {
            using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32)) using(var key=root.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")) return key==null?0:Convert.ToInt32(key.GetValue("Release",0));
        }
        public static bool WinFspPresent() {
            using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32)) using(var key=root.OpenSubKey(@"SOFTWARE\WinFsp")) {
                string path=key==null?null:key.GetValue("InstallDir") as string;
                return path!=null && File.Exists(Path.Combine(path,"bin","winfsp-x64.dll"));
            }
        }
        public static string SafePath(string root,string relative) {
            string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception("Đường dẫn trong bộ cài không hợp lệ."); return full;
        }
        public static void Extract(string destination) {
            Directory.CreateDirectory(destination);
            using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")) {
                if(resource==null) throw new Exception("Bộ cài thiếu dữ liệu tích hợp.");
                using(var zip=new ZipArchive(resource,ZipArchiveMode.Read)) foreach(var entry in zip.Entries) {
                    if(entry.FullName.EndsWith("/")) continue;
                    string path=SafePath(destination,entry.FullName); Directory.CreateDirectory(Path.GetDirectoryName(path)); entry.ExtractToFile(path,true);
                }
            }
            var manifest=Core.Json.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(destination,"payload.json")));
            foreach(var pair in manifest) if(!Core.Hash(SafePath(destination,pair.Key)).Equals(pair.Value,StringComparison.OrdinalIgnoreCase)) throw new Exception("Dữ liệu bộ cài sai checksum: "+pair.Key);
            foreach(string required in new[]{"app/GoogleNas.exe","app/runtime/rclone.exe","app/Uninstall.exe","prerequisites/winfsp.msi","prerequisites/dotnet48.exe"}) if(!manifest.ContainsKey(required)) throw new Exception("Bộ cài thiếu "+required);
        }
        public static void InstallFiles(string stage,string target) {
            string app=Path.Combine(stage,"app"); Directory.CreateDirectory(target);
            var changed=new List<KeyValuePair<string,string>>();
            string current="", transaction=Guid.NewGuid().ToString("N");
            try {
                foreach(string source in Directory.GetFiles(app,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase)) {
                    string relative=source.Substring(app.Length+1), destination=SafePath(target,relative); Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    current=destination;
                    if(File.Exists(destination) && Core.Hash(source)==Core.Hash(destination)) continue;
                    string temp=destination+".install-new-"+transaction, backup=destination+".install-backup-"+transaction;
                    File.Copy(source,temp,false);
                    if(Core.Hash(source)!=Core.Hash(temp)) throw new Exception("Sao chép file lỗi: "+relative);
                    try {
                        if(File.Exists(destination)) {
                            // Rename preserves the old file and avoids ReplaceFile's metadata/delete errors.
                            MoveWithRetry(destination,backup);
                            try { MoveWithRetry(temp,destination); }
                            catch { MoveWithRetry(backup,destination); throw; }
                            changed.Add(new KeyValuePair<string,string>(destination,backup));
                        } else { MoveWithRetry(temp,destination); changed.Add(new KeyValuePair<string,string>(destination,null)); }
                    } finally { try { if(File.Exists(temp)) File.Delete(temp); } catch(IOException) {} catch(UnauthorizedAccessException) {} }
                }
            } catch(Exception error) {
                var rollbackErrors=new List<string>();
                for(int i=changed.Count-1;i>=0;i--) {
                    var pair=changed[i];
                    try {
                        if(File.Exists(pair.Key)) DeleteInstalledFile(pair.Key);
                        if(pair.Value!=null && File.Exists(pair.Value)) MoveWithRetry(pair.Value,pair.Key);
                    } catch(Exception restoreError) { rollbackErrors.Add(pair.Key+": "+restoreError.Message); }
                }
                throw new IOException("Không thể thay tệp: "+current+"\nĐóng CYAN NAS và chương trình đang dùng tệp này, rồi chạy lại bộ cài.\nChi tiết: "+error.Message+(rollbackErrors.Count>0?"\nMột số tệp cần khôi phục từ .install-backup: "+string.Join("; ",rollbackErrors):""),error);
            }
            foreach(var pair in changed) if(pair.Value!=null && File.Exists(pair.Value)) { try { DeleteInstalledFile(pair.Value); } catch(IOException) {} catch(UnauthorizedAccessException) {} }
        }
        private static void MoveWithRetry(string source,string destination) {
            for(int attempt=0;;attempt++) {
                try { File.Move(source,destination); return; }
                catch(IOException) { if(attempt>=3) throw; System.Threading.Thread.Sleep(200); }
            }
        }
        private static void DeleteInstalledFile(string path) {
            File.SetAttributes(path,File.GetAttributes(path)&~FileAttributes.ReadOnly);
            File.Delete(path);
        }
        public static string InstallerArgs(string[] args) {
            // msiexec uses its own parser: quoted switches can open an invisible help dialog.
            return string.Join(" ",args.Select(a=>a.Length>0 && a.IndexOfAny(new[]{' ','\t','\r','\n','"'})<0 ? a : Core.Quote(a)));
        }
        public static bool WaitForInstaller(Process p,int timeoutMs,Action<int> heartbeat) {
            var clock=Stopwatch.StartNew();
            while(!p.WaitForExit(Math.Min(1000,Math.Max(1,timeoutMs-(int)clock.ElapsedMilliseconds)))) {
                heartbeat((int)clock.Elapsed.TotalSeconds);
                if(clock.ElapsedMilliseconds>=timeoutMs) return false;
            }
            return true;
        }
        private static void Elevated(string executable,string[] args,Action<string,int> progress,string name,int percent,int timeoutMs) {
            using(var p=Process.Start(new ProcessStartInfo(executable,InstallerArgs(args)) { UseShellExecute=true, Verb="runas", WindowStyle=ProcessWindowStyle.Normal })) {
                if(!WaitForInstaller(p,timeoutMs,seconds=>progress("Đang cài "+name+" · "+seconds+" giây. Chấp nhận yêu cầu quyền Windows nếu xuất hiện.",percent)))
                    throw new PendingInstallException("Windows Installer chưa hoàn tất sau "+(timeoutMs/60000)+" phút (PID "+p.Id+"). Bộ cài đã dừng chờ và giữ file cài tạm. Kiểm tra cửa sổ Windows Installer hoặc nhật ký; không chạy thêm bộ cài khi tiến trình này còn hoạt động.");
                if(p.ExitCode==3010 || p.ExitCode==1641) { Reboot=true; return; }
                if(p.ExitCode!=0) throw new Exception("Cài thành phần hệ thống thất bại (mã "+p.ExitCode+"). Bạn có thể chạy lại bộ cài. Nhật ký nằm trong %LOCALAPPDATA%\\GoogleNasSetup\\logs.");
            }
        }
        public static void Install(Action<string,int> progress,bool startup) {
            if(!Environment.Is64BitOperatingSystem || Environment.OSVersion.Version.Major<10 || Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64") throw new Exception("Bộ cài này dành cho Windows 10/11 x64.");
            if(Active()) throw new Exception("CYAN NAS đang chạy. Chọn Thông tin → Thoát an toàn, hoặc bấm ^ gần đồng hồ → nhấp phải CYAN NAS → Thoát an toàn, rồi bấm Cài đặt lại. Bấm X chỉ thu nhỏ, chưa thoát.");
            string stage=Path.Combine(Path.GetTempPath(),"GoogleNas-setup-"+Guid.NewGuid().ToString("N"));
            bool keepStage=false;
            try {
                progress("Giải nén và kiểm tra dữ liệu tích hợp…",10); Extract(stage);
                string logs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GoogleNasSetup","logs"); Directory.CreateDirectory(logs);
                if(FrameworkRelease()<528040) { progress("Đang cài .NET Framework offline. Chấp nhận yêu cầu quyền Windows nếu xuất hiện.",35); Elevated(Path.Combine(stage,"prerequisites","dotnet48.exe"),new[]{"/q","/norestart","/log",Path.Combine(logs,"dotnet48.log")},progress,".NET Framework",35,1200000); if(!Reboot && FrameworkRelease()<528040) throw new Exception(".NET Framework chưa sẵn sàng. Khởi động lại và chạy lại bộ cài."); }
                if(!WinFspPresent()) { progress("Đang cài WinFsp. Chấp nhận yêu cầu quyền Windows nếu xuất hiện.",60); Elevated(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe"),new[]{"/i",Path.Combine(stage,"prerequisites","winfsp.msi"),"/qn","/norestart","INSTALLLEVEL=100","/l*v",Path.Combine(logs,"winfsp.log")},progress,"WinFsp",60,180000); if(!Reboot && !WinFspPresent()) throw new Exception("Driver chưa được đăng ký. Kiểm tra nhật ký cài đặt."); }
                progress("Cài ứng dụng và các thành phần ổ đĩa…",80); InstallFiles(stage,Target); Register(startup);
                if(!string.IsNullOrEmpty(ShortcutWarning)) File.WriteAllText(Path.Combine(logs,"shortcuts.log"),ShortcutWarning);
                progress("Cài đặt hoàn tất",100);
            } catch(PendingInstallException) { keepStage=true; throw; }
            finally { if(!keepStage && Directory.Exists(stage)) Directory.Delete(stage,true); }
        }
        private static void Shortcut(string path,string executable,string arguments) {
            object shell=null,link=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcutShell=shell;
                link=shortcutShell.CreateShortcut(path); dynamic shortcut=link;
                shortcut.TargetPath=executable; shortcut.Arguments=arguments; shortcut.WorkingDirectory=Path.GetDirectoryName(executable); shortcut.Save();
            } finally {
                if(link!=null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
                if(shell!=null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
        public static string CreateShortcuts(string menu,string target) {
            try {
                Directory.CreateDirectory(menu);
                // WScript.Shell replaces unsupported characters with '?' on some Windows locales.
                Shortcut(Path.Combine(menu,"CYAN NAS.lnk"),Path.Combine(target,"GoogleNas.exe"),"");
                Shortcut(Path.Combine(menu,"Uninstall CYAN NAS.lnk"),Path.Combine(target,"Uninstall.exe"),"--uninstall");
                return null;
            } catch(System.Runtime.InteropServices.COMException e) { return e.Message; }
              catch(IOException e) { return e.Message; }
              catch(UnauthorizedAccessException e) { return e.Message; }
        }
        private static void Register(bool startup) {
            using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\GoogleNas")) {
                key.SetValue("DisplayName","CYAN NAS"); key.SetValue("DisplayVersion",Core.Version); key.SetValue("Publisher","CYAN NAS"); key.SetValue("InstallLocation",Target); key.SetValue("DisplayIcon",Path.Combine(Target,"GoogleNas.exe")); key.SetValue("UninstallString",Core.Quote(Path.Combine(Target,"Uninstall.exe"))+" --uninstall"); key.SetValue("NoModify",1,RegistryValueKind.DWord); key.SetValue("NoRepair",1,RegistryValueKind.DWord);
            }
            using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) { if(startup) key.SetValue("GoogleNas",Core.Quote(Path.Combine(Target,"GoogleNas.exe"))+" --tray"); else key.DeleteValue("GoogleNas",false); }
            ShortcutWarning=CreateShortcuts(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"CYAN NAS"),Target);
            if(string.IsNullOrEmpty(ShortcutWarning)) {
                // Remove only the two shortcuts owned by the previous product name.
                string oldMenu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Google NAS");
                foreach(string name in new[]{"Google NAS.lnk","Uninstall Google NAS.lnk"}) {
                    try { File.Delete(Path.Combine(oldMenu,name)); } catch {}
                }
            }
        }
        public static void Uninstall() {
            if(!Path.GetFullPath(Application.StartupPath).Equals(Path.GetFullPath(Target),StringComparison.OrdinalIgnoreCase)) throw new Exception("Chạy trình gỡ từ thư mục ứng dụng đã cài.");
            if(Active()) throw new Exception("Thoát an toàn CYAN NAS từ khay hệ thống trước khi gỡ.");
            if(MessageBox.Show("Gỡ CYAN NAS? Tài khoản, dữ liệu cache và driver dùng chung sẽ được giữ lại.","CYAN NAS",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
            string ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
            Process.Start(new ProcessStartInfo(ps,Core.Args(new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(Target,"uninstall.ps1"),"-WaitProcessId",Process.GetCurrentProcess().Id.ToString()})) { UseShellExecute=false,CreateNoWindow=true });
        }
    }
    public class SetupForm : Form {
        private Button install, finish; private CheckBox startup, launch; private ProgressBar progress; private Label state; private bool working, done;
        public SetupForm() {
            SuspendLayout(); AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
            Text="CYAN NAS · Cài đặt"; Icon=Theme.Icon(); Font=new Font("Segoe UI",10); BackColor=Theme.Background; ClientSize=new Size(760,535); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
            var banner=new Panel { Dock=DockStyle.Top,Height=143,BackColor=Theme.Navy }; Controls.Add(banner); Put(banner,"CYAN NAS",28,true,Color.White,30,25); Put(banner,"Ổ đĩa Google Drive cho Windows",12,false,Color.FromArgb(173,194,221),33,78);
            var body=new Card { Location=new Point(26,163),Size=new Size(708,277) }; Controls.Add(body); Put(body,"Một bộ cài. Sẵn sàng làm việc.",17,true,Theme.Ink,22,19);
            Put(body,"Google Drive: rclone, WinFsp và .NET Framework tích hợp offline.\nNAS từ xa: tool tự tải/cài Tailscale khi bạn bật tính năng.",10,false,Theme.Muted,22,61);
            startup=new CheckBox { Text="Khởi chạy CYAN NAS khi đăng nhập Windows",Checked=true,AutoSize=true,Location=new Point(22,111) }; body.Controls.Add(startup);
            launch=new CheckBox { Text="Mở ứng dụng sau khi cài xong",Checked=true,AutoSize=true,Location=new Point(22,145) }; body.Controls.Add(launch);
            state=Put(body,"Sẵn sàng cài đặt · Driver có thể yêu cầu quyền quản trị Windows.",9,false,Theme.Muted,22,185); state.AutoSize=false; state.Size=new Size(660,40);
            progress=new ProgressBar { Location=new Point(22,234),Size=new Size(660,8),Style=ProgressBarStyle.Continuous }; body.Controls.Add(progress);
            Put(this,"WinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos",8,false,Theme.Muted,28,451);
            var credits=new LinkLabel { Text="Giấy phép & thành phần tích hợp",AutoSize=true,Location=new Point(28,475),LinkColor=Theme.Blue }; credits.LinkClicked+=(a,b)=>MessageBox.Show("WinFsp: GPLv3, phân phối MSI nguyên bản.\nhttps://github.com/winfsp/winfsp\nrclone: MIT. .NET Framework: Microsoft.\nBộ cài cài các thành phần này theo giấy phép nhà cung cấp. Giấy phép và mã nguồn WinFsp được lưu trong thư mục licenses của ứng dụng.","Thành phần tích hợp"); Controls.Add(credits);
            install=Theme.Button("Cài đặt",true); install.Location=new Point(592,467); install.Size=new Size(142,42); Controls.Add(install);
            finish=Theme.Button("Hoàn tất",true); finish.Location=install.Location; finish.Size=install.Size; finish.Visible=false; Controls.Add(finish);
            install.Click+=async(a,b)=>await BeginInstall(); finish.Click+=(a,b)=>{ if(launch.Checked && !SetupEngine.Reboot) Process.Start(Path.Combine(SetupEngine.Target,"GoogleNas.exe")); Close(); };
            FormClosing+=(a,b)=>{ if(working) b.Cancel=true; };
            ResumeLayout(true);
        }
        private Label Put(Control p,string text,float size,bool bold,Color color,int x,int y) { var l=Theme.Label(text,size,bold,color); l.Location=new Point(x,y); p.Controls.Add(l); return l; }
        private async Task BeginInstall() {
            if(working || done) return; working=true; install.Enabled=false; startup.Enabled=false; launch.Enabled=false; state.ForeColor=Theme.Muted;
            try {
                bool auto=startup.Checked;
                await Task.Run(()=>SetupEngine.Install((message,value)=>BeginInvoke(new Action(()=>{ state.Text=message; progress.Value=value; })),auto));
                done=true; install.Visible=false; finish.Visible=true; launch.Enabled=true;
                if(SetupEngine.Reboot) { state.Text="Đã cài xong. Khởi động lại Windows để hoàn tất driver/runtime, rồi mở CYAN NAS."; launch.Checked=false; launch.Enabled=false; }
                else state.Text="Đã cài xong. Mở ứng dụng và kết nối tài khoản Google để bắt đầu.";
                if(!string.IsNullOrEmpty(SetupEngine.ShortcutWarning)) state.Text="Đã cài xong; chưa tạo được shortcut. Mở GoogleNas.exe trong thư mục cài. Chi tiết: %LOCALAPPDATA%\\GoogleNasSetup\\logs\\shortcuts.log";
            } catch(Exception e) {
                try { string logs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GoogleNasSetup","logs"); Directory.CreateDirectory(logs); File.WriteAllText(Path.Combine(logs,"install-error.log"),e.ToString()); } catch {}
                state.Text=e.Message; state.ForeColor=Color.FromArgb(173,55,46); MessageBox.Show(this,e.Message,"Chưa hoàn tất cài đặt",MessageBoxButtons.OK,MessageBoxIcon.Warning); install.Enabled=!(e is SetupEngine.PendingInstallException); startup.Enabled=true; launch.Enabled=true;
            }
            finally { working=false; }
        }
    }
}
