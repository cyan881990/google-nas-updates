using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace GoogleNas {
    public class NasProfile {
        public string Folder="", Share="GoogleNAS", User="nasuser", Host="", RemoteShare="GoogleNAS", Letter="N";
        public bool ReadOnly=false, CreateUser=true;
    }
    public class NasNetwork {
        public string State="", Address="", AuthUrl="";
        public List<string> Peers=new List<string>();
        public bool Ready { get { return State=="Running" && NasService.IsTailIp(Address); } }
    }
    public class NasService {
        private readonly string home;
        public NasService(string directory) { home=directory; }
        public string Cli { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Tailscale","tailscale.exe"); } }
        public bool Installed { get { return File.Exists(Cli); } }
        public NasProfile Load() { string path=Path.Combine(home,"nas.json"); return File.Exists(path)?Core.Json.Deserialize<NasProfile>(File.ReadAllText(path)):new NasProfile(); }
        public void Save(NasProfile profile) { string path=Path.Combine(home,"nas.json"); File.WriteAllText(path+".new",Core.Json.Serialize(profile)); if(File.Exists(path)) File.Replace(path+".new",path,null); else File.Move(path+".new",path); }
        public static bool IsTailIp(string value) {
            IPAddress ip; if(!Regex.IsMatch(value??"",@"^100\.[1-9][0-9]{1,2}\.(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})$") || !IPAddress.TryParse(value,out ip) || ip.AddressFamily!=AddressFamily.InterNetwork) return false;
            byte[] bytes=ip.GetAddressBytes(); return bytes[0]==100 && bytes[1]>=64 && bytes[1]<=127;
        }
        public static void ValidateShare(string share) { if(!Regex.IsMatch(share??"",@"^[A-Za-z][A-Za-z0-9_-]{0,47}$")) throw new Exception("Tên NAS bắt đầu bằng chữ, tối đa 48 ký tự chữ, số, - hoặc _."); }
        public static void ValidateUser(string user) { if(!Regex.IsMatch(user??"",@"^[A-Za-z][A-Za-z0-9_-]{2,19}$")) throw new Exception("Tài khoản NAS cần 3–20 ký tự chữ, số, - hoặc _."); }
        public static string RemotePath(string host,string share) { if(!IsTailIp(host)) throw new Exception("Nhập địa chỉ IPv4 Tailscale dạng 100.x.x.x của máy chứa ổ."); ValidateShare(share); return @"\\"+host+@"\"+share; }
        public static void ValidateLetter(string letter) { if(!Regex.IsMatch(letter??"",@"^[D-Z]$")) throw new Exception("Chọn ổ từ D đến Z."); }
        public static NasNetwork ParseNetwork(string json) {
            var data=Core.Json.Deserialize<Dictionary<string,object>>(json); var result=new NasNetwork(); object value;
            if(data.TryGetValue("BackendState",out value)) result.State=Convert.ToString(value);
            if(data.TryGetValue("AuthURL",out value)) result.AuthUrl=Convert.ToString(value);
            if(data.TryGetValue("TailscaleIPs",out value) && value is IEnumerable && !(value is string)) foreach(object ip in (IEnumerable)value) if(IsTailIp(Convert.ToString(ip))) { result.Address=Convert.ToString(ip); break; }
            if(data.TryGetValue("Peer",out value) && value is Dictionary<string,object>) foreach(var peer in ((Dictionary<string,object>)value).Values) {
                var p=peer as Dictionary<string,object>; if(p==null) continue; object addresses; if(!p.TryGetValue("TailscaleIPs",out addresses) || !(addresses is IEnumerable) || addresses is string) continue;
                foreach(object ip in (IEnumerable)addresses) if(IsTailIp(Convert.ToString(ip))) { object name; result.Peers.Add((p.TryGetValue("HostName",out name)?Convert.ToString(name):"Máy")+" · "+ip); }
            }
            return result;
        }
        private async Task<string> Command(string[] args,int timeout) {
            if(!Installed) throw new Exception("Bấm Cài Tailscale trước khi kết nối.");
            using(var p=Process.Start(new ProcessStartInfo(Cli,Core.Args(args)) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true })) {
                var output=p.StandardOutput.ReadToEndAsync(); var error=p.StandardError.ReadToEndAsync();
                if(!await Task.Run(()=>p.WaitForExit(timeout))) { p.Kill(); throw new Exception("Tailscale chưa trả lời. Kiểm tra dịch vụ Tailscale và thử lại."); }
                string text=await output, failure=await error;
                if(p.ExitCode!=0 && string.IsNullOrWhiteSpace(text)) throw new Exception(failure.Trim()); return text;
            }
        }
        public async Task<NasNetwork> Status() { return Installed?ParseNetwork(await Command(new[]{"status","--json"},10000)):new NasNetwork { State="NotInstalled" }; }
        public async Task Login(Action<string> status) {
            if(!Installed) throw new Exception("Cài Tailscale trước khi đăng nhập.");
            status("Đăng nhập Tailscale trong trình duyệt. Cả hai máy cần ở cùng mạng Tailscale.");
            using(var p=new Process { StartInfo=new ProcessStartInfo(Cli,Core.Args(new[]{"login","--timeout=90s"})) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true } }) {
                bool opened=false; object gate=new object(); var errors=new StringBuilder();
                DataReceivedEventHandler receive=(sender,e)=>{ if(e.Data==null) return; lock(gate) { errors.AppendLine(e.Data); var match=Regex.Match(e.Data,@"https://login\.tailscale\.com/[A-Za-z0-9/_?=.&%-]+"); if(match.Success && !opened) { opened=true; try { Process.Start(new ProcessStartInfo(match.Value) { UseShellExecute=true }); } catch(Win32Exception failure) { errors.AppendLine(failure.Message); } } } };
                p.OutputDataReceived+=receive; p.ErrorDataReceived+=receive; p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                if(!await Task.Run(()=>p.WaitForExit(100000))) { p.Kill(); throw new Exception("Chưa hoàn tất đăng nhập. Bấm Đăng nhập để thử lại."); }
                p.WaitForExit(); if(p.ExitCode!=0) throw new Exception("Chưa hoàn tất đăng nhập Tailscale. "+errors.ToString().Trim());
            }
        }
        public async Task<Dictionary<string,object>> Admin(string operation,NasProfile profile,string password="") {
            string requests=Path.Combine(home,"admin-requests"); Directory.CreateDirectory(requests);
            string request=Path.Combine(requests,Guid.NewGuid().ToString("N")+".request"), script=Path.Combine(requests,"nas-admin.ps1");
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("nas-admin.ps1")) using(var file=File.Create(script)) { if(stream==null) throw new Exception("Ứng dụng thiếu module NAS. Cài lại bản đầy đủ."); stream.CopyTo(file); }
            var data=new Dictionary<string,object> { {"Operation",operation},{"Profile",profile},{"Password",password},{"UserSid",WindowsIdentity.GetCurrent().User.Value} };
            byte[] clear=Encoding.UTF8.GetBytes(Core.Json.Serialize(data)); try { File.WriteAllBytes(request,ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser)); } finally { Array.Clear(clear,0,clear.Length); }
            bool completed=false;
            try {
                string ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
                using(var p=Process.Start(new ProcessStartInfo(ps,Core.Args(new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-Request",request})) { UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden })) {
                    if(!await Task.Run(()=>p.WaitForExit(240000))) throw new Exception("Tác vụ NAS vẫn đang chạy; không bấm chạy lại. Xem nhật ký trong thư mục dữ liệu/admin-requests."); completed=true;
                    if(!File.Exists(request+".result")) throw new Exception("Tác vụ NAS không trả kết quả. Kiểm tra quyền quản trị Windows.");
                    var result=Core.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(request+".result"));
                    if(!Convert.ToBoolean(result["ok"])) throw new Exception(Convert.ToString(result["error"])); return result;
                }
            } catch(Win32Exception e) { if(e.NativeErrorCode==1223) { completed=true; throw new Exception("Bạn đã hủy yêu cầu quyền quản trị Windows."); } throw; }
            finally { if(completed) { if(File.Exists(request)) File.Delete(request); if(File.Exists(request+".result")) File.Delete(request+".result"); } }
        }
        public static async Task Probe(string host) {
            if(!IsTailIp(host)) throw new Exception("Địa chỉ Tailscale không hợp lệ.");
            using(var client=new TcpClient()) { var connect=client.ConnectAsync(host,445); if(await Task.WhenAny(connect,Task.Delay(8000))!=connect) throw new Exception("Không tới được máy NAS. Kiểm tra hai máy đã đăng nhập Tailscale, máy chứa ổ đang bật và đã tạo NAS."); await connect; }
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct NetResource { public int Scope,Type,DisplayType,Usage; public string LocalName,RemoteName,Comment,Provider; }
        [DllImport("mpr.dll",CharSet=CharSet.Unicode)] private static extern int WNetAddConnection3(IntPtr owner,ref NetResource resource,string password,string username,int flags);
        [DllImport("mpr.dll",CharSet=CharSet.Unicode)] private static extern int WNetCancelConnection2(string name,int flags,bool force);
        [DllImport("mpr.dll",CharSet=CharSet.Unicode)] private static extern int WNetGetConnection(string localName,StringBuilder remoteName,ref int length);
        public static string MappedPath(string letter) { ValidateLetter(letter); var buffer=new StringBuilder(2048); int length=buffer.Capacity; return WNetGetConnection(letter+":",buffer,ref length)==0?buffer.ToString():null; }
        public static void Map(IntPtr owner,string host,string share,string letter) {
            string path=RemotePath(host,share); ValidateLetter(letter);
            if(DriveInfo.GetDrives().Any(d=>d.Name.StartsWith(letter+":",StringComparison.OrdinalIgnoreCase)) || MappedPath(letter)!=null) throw new Exception("Ký tự ổ "+letter+": đang được sử dụng. Chọn ký tự khác.");
            var resource=new NetResource { Type=1,LocalName=letter+":",RemoteName=path };
            int error=WNetAddConnection3(owner,ref resource,null,null,1|8|16);
            if(error==1223) throw new Exception("Đã hủy kết nối NAS.");
            if(error==1219) throw new Exception("Máy này đang kết nối NAS bằng tài khoản khác. Ngắt kết nối đó trong Windows trước khi dùng tài khoản mới.");
            if(error!=0) throw new Win32Exception(error,"Không gắn được NAS: "+new Win32Exception(error).Message);
        }
        public static void Unmap(string host,string share,string letter) {
            string expected=RemotePath(host,share),actual=MappedPath(letter);
            if(actual==null || !actual.Equals(expected,StringComparison.OrdinalIgnoreCase)) throw new Exception("Ổ này không trỏ tới NAS đã chọn; không ngắt ổ khác.");
            int error=WNetCancelConnection2(letter+":",1,false); if(error!=0) throw new Win32Exception(error,"Đóng file đang mở trên ổ NAS trước khi ngắt. "+new Win32Exception(error).Message);
        }
    }
}
