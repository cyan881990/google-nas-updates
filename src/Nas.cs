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
        public string Folder="", Share="CYANNAS", User="nasuser", Host="", RemoteShare="CYANNAS", Letter="N", RemoteUser="";
        public bool ReadOnly=false, CreateUser=true;
    }
    public class NasNetwork {
        public string State="", Address="", AuthUrl="";
        public List<string> Peers=new List<string>();
        public bool Ready { get { return State=="Running" && NasService.IsTailIp(Address); } }
    }
    public class NasLogin { public string User="", Password=""; }
    public class NasService {
        private readonly string home;
        private readonly NasConnectionMonitor monitor=new NasConnectionMonitor();
        public NasService(string directory) { home=directory; }
        private string PasswordFile { get { return Path.Combine(home,"nas-passwords.dat"); } }
        private Dictionary<string,string> Passwords() {
            if(!File.Exists(PasswordFile)) return new Dictionary<string,string>();
            byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(PasswordFile),null,DataProtectionScope.CurrentUser);
            try { return Core.Json.Deserialize<Dictionary<string,string>>(Encoding.UTF8.GetString(clear)); }
            finally { Array.Clear(clear,0,clear.Length); }
        }
        public void SavePassword(string user,string password) {
            ValidateUser(user); if(string.IsNullOrEmpty(password)) throw new Exception("Nhập mật khẩu NAS cần lưu.");
            var values=Passwords(); values[user.ToLowerInvariant()]=password;
            WritePasswords(values);
        }
        private void WritePasswords(Dictionary<string,string> values) {
            byte[] clear=Encoding.UTF8.GetBytes(Core.Json.Serialize(values));
            try {
                Directory.CreateDirectory(home);
                File.WriteAllBytes(PasswordFile+".new",ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser));
                if(File.Exists(PasswordFile)) File.Replace(PasswordFile+".new",PasswordFile,null); else File.Move(PasswordFile+".new",PasswordFile);
            } finally { Array.Clear(clear,0,clear.Length); }
        }
        public string LoadPassword(string user) { ValidateUser(user); string value; return Passwords().TryGetValue(user.ToLowerInvariant(),out value)?value:""; }
        public static void ValidateRemoteUser(string user) {
            if(string.IsNullOrWhiteSpace(user) || user.Length>513 || user.Any(char.IsControl)) throw new Exception("Nhập tài khoản NAS dạng TEN-MAY\\taikhoan hoặc taikhoan.");
        }
        public static string LocalAccount(string user) { ValidateUser(user); return Environment.MachineName+"\\"+user; }
        [DllImport("advapi32.dll",EntryPoint="LogonUserW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool LogonUser(string user,string domain,string password,int logonType,int provider,out IntPtr token);
        [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
        private static int CheckWindowsPassword(string user,string password) {
            IntPtr token=IntPtr.Zero;
            try { return LogonUser(user,Environment.MachineName,password,3,0,out token)?0:Marshal.GetLastWin32Error(); }
            finally { if(token!=IntPtr.Zero) CloseHandle(token); }
        }
        public static void CheckLocalPassword(string user,string password,Func<string,string,int> check=null) {
            ValidateUser(user); if(string.IsNullOrEmpty(password)) throw new Exception("Nhập mật khẩu Windows hiện tại của tài khoản NAS để kiểm tra.");
            int error=(check??CheckWindowsPassword)(user,password);
            if(error!=0) throw ConnectionError(error,LocalAccount(user));
        }
        public static Win32Exception ConnectionError(int error,string target) {
            string help;
            switch(error) {
                case 86: case 1326: help="Windows không chấp nhận tài khoản hoặc mật khẩu. Dùng tên tài khoản được sao chép từ máy chia sẻ và mật khẩu Windows hiện tại. Lưu mật khẩu trong tool không đổi mật khẩu Windows."; break;
                case 5: help="Windows từ chối truy cập. Kiểm tra quyền chia sẻ SMB, quyền thư mục NTFS và chính sách đăng nhập mạng của tài khoản trên máy chứa ổ; mã này không xác định riêng lỗi mật khẩu."; break;
                case 1219: help="Đã có kết nối đến máy NAS bằng tài khoản khác. Đóng file và ngắt đúng kết nối cũ trong Windows trước khi dùng tài khoản mới."; break;
                case 1331: help="Tài khoản Windows trên máy chứa ổ đang bị vô hiệu hóa."; break;
                case 1909: help="Tài khoản Windows đang bị khóa. Dừng thử mật khẩu và mở khóa tài khoản trên máy chứa ổ."; break;
                case 1330: help="Mật khẩu Windows đã hết hạn. Đổi mật khẩu trên máy chứa ổ rồi nhập lại trong tool."; break;
                case 1907: help="Windows yêu cầu đổi mật khẩu tài khoản trước khi đăng nhập qua mạng."; break;
                case 1385: help="Tài khoản không được phép đăng nhập qua mạng trên máy chứa ổ. Kiểm tra quyền Access this computer from the network và Deny access to this computer from the network."; break;
                case 1327: help="Windows chặn đăng nhập do giới hạn tài khoản. Kiểm tra mật khẩu không trống và chính sách tài khoản trên máy chứa ổ."; break;
                case 67: help="Không tìm thấy tên chia sẻ. Kiểm tra Tên NAS từ xa trên máy chứa ổ."; break;
                case 53: case 64: help="Không truy cập được máy NAS. Kiểm tra máy chứa ổ đang bật, Tailscale và kết nối SMB."; break;
                case 1223: help="Đã hủy kết nối NAS."; break;
                default: help="Windows không hoàn tất kết nối NAS. Kiểm tra chi tiết lỗi bên dưới."; break;
            }
            return new Win32Exception(error,"Mã Windows "+error+" · "+target+"\n"+help+"\n"+new Win32Exception(error).Message);
        }
        private static string LoginKey(string host,string share) { return "remote:"+RemotePath(host,share).ToLowerInvariant(); }
        public NasLogin LoadRemoteLogin(string host,string share) {
            string value; return Passwords().TryGetValue(LoginKey(host,share),out value)?Core.Json.Deserialize<NasLogin>(value):null;
        }
        public void SaveRemoteLogin(string host,string share,string user,string password) {
            ValidateRemoteUser(user); if(string.IsNullOrEmpty(password)) throw new Exception("Nhập mật khẩu NAS.");
            var values=Passwords(); values[LoginKey(host,share)]=Core.Json.Serialize(new NasLogin { User=user,Password=password }); WritePasswords(values);
        }
        public static NasLogin ResolveLogin(NasLogin saved,string user,string password) {
            if(!string.IsNullOrEmpty(password)) { ValidateRemoteUser(user); return new NasLogin { User=user,Password=password }; }
            if(saved!=null && (string.IsNullOrWhiteSpace(user) || string.Equals(saved.User,user,StringComparison.OrdinalIgnoreCase))) return saved;
            if(!string.IsNullOrWhiteSpace(user)) throw new Exception("Nhập mật khẩu cho tài khoản NAS mới. Mật khẩu đã lưu thuộc tài khoản khác.");
            return null; // Existing Windows Credential Manager entries remain usable without another dialog.
        }
        public static NasLogin ChooseLogin(NasLogin saved,string user,string password,bool promptAgain,Func<NasLogin> prompt) {
            if(!promptAgain && !string.IsNullOrEmpty(password)) { ValidateRemoteUser(user); return new NasLogin { User=user,Password=password }; }
            if(!promptAgain && saved!=null && (string.IsNullOrWhiteSpace(user) || string.Equals(saved.User,user,StringComparison.OrdinalIgnoreCase))) return saved;
            var login=prompt(); if(login!=null) { ValidateRemoteUser(login.User); if(string.IsNullOrEmpty(login.Password)) throw new Exception("Nhập mật khẩu Windows / NAS; không dùng mã PIN."); }
            return login;
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct CredUiInfo {
            public int Size; public IntPtr Owner; public string Message,Caption; public IntPtr Banner;
        }
        [DllImport("credui.dll",EntryPoint="CredUIPromptForCredentialsW",CharSet=CharSet.Unicode)] private static extern int PromptCredentials(ref CredUiInfo info,string target,IntPtr context,int error,StringBuilder user,int maxUser,StringBuilder password,int maxPassword,[MarshalAs(UnmanagedType.Bool)] ref bool save,int flags);
        public static NasLogin PromptLogin(IntPtr owner,string host,string share,string account) {
            string path=RemotePath(host,share);
            var info=new CredUiInfo { Size=Marshal.SizeOf(typeof(CredUiInfo)),Owner=owner,Caption="Đăng nhập CYAN NAS",Message="Kết nối "+path+"\nDùng tài khoản Windows của máy chứa ổ. Thông tin chỉ được lưu sau khi kết nối thành công." };
            var user=new StringBuilder(514); if(!string.IsNullOrWhiteSpace(account) && account.Length<514) user.Append(account);
            var password=new StringBuilder(257); bool save=false;
            try {
                // Let Windows collect credentials; persistence is handled after SMB succeeds.
                const int Generic=0x40000,AlwaysShow=0x80,DoNotPersist=0x2;
                int error=PromptCredentials(ref info,host,IntPtr.Zero,0,user,user.Capacity,password,password.Capacity,ref save,Generic|AlwaysShow|DoNotPersist);
                if(error==1223) return null;
                if(error!=0) throw ConnectionError(error,path);
                return new NasLogin { User=user.ToString(),Password=password.ToString() };
            } finally { for(int i=0;i<password.Length;i++) password[i]='\0'; password.Length=0; }
        }
        internal string MapAndSave(IntPtr owner,NasProfile profile,string user,string password,Action<string,string> connect=null,Action<string,string,string> saveWindows=null) {
            var login=ResolveLogin(string.IsNullOrEmpty(password)?LoadRemoteLogin(profile.Host,profile.RemoteShare):null,user,password);
            if(connect==null) Map(owner,profile.Host,profile.RemoteShare,profile.Letter,login==null?null:login.User,login==null?null:login.Password);
            else connect(login==null?null:login.User,login==null?null:login.Password);
            if(login==null) return "";
            profile.RemoteUser=login.User; string warning="";
            try { SaveRemoteLogin(profile.Host,profile.RemoteShare,login.User,login.Password); } catch { warning="Đã gắn ổ nhưng chưa lưu được mật khẩu trong tool. Nhập lại khi kết nối lần sau."; }
            try { if(saveWindows==null) SaveWindowsLogin(profile.Host,login.User,login.Password); else saveWindows(profile.Host,login.User,login.Password); } catch { warning+=" Windows chưa cho lưu thông tin đăng nhập; tự kết nối lại cần tool đang chạy."; }
            return warning.Trim();
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct Credential {
            public uint Flags,Type; public string TargetName,Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize; public IntPtr CredentialBlob; public uint Persist,AttributeCount;
            public IntPtr Attributes; public string TargetAlias,UserName;
        }
        [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential credential,uint flags);
        private static void SaveWindowsLogin(string host,string user,string password) {
            if(!IsTailIp(host)) throw new Exception("Địa chỉ NAS không hợp lệ."); ValidateRemoteUser(user);
            byte[] bytes=Encoding.Unicode.GetBytes(password); IntPtr blob=Marshal.AllocHGlobal(bytes.Length);
            try {
                Marshal.Copy(bytes,0,blob,bytes.Length);
                var credential=new Credential { Type=2,TargetName=host,UserName=user,CredentialBlobSize=(uint)bytes.Length,CredentialBlob=blob,Persist=2,Comment="CYAN NAS" };
                if(!CredWrite(ref credential,0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            } finally { for(int i=0;i<bytes.Length;i++) Marshal.WriteByte(blob,i,0); Marshal.FreeHGlobal(blob); Array.Clear(bytes,0,bytes.Length); }
        }
        public string Cli { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Tailscale","tailscale.exe"); } }
        public bool Installed { get { return File.Exists(Cli); } }
        public NasProfile Load() { string path=Path.Combine(home,"nas.json"); return File.Exists(path)?Core.Json.Deserialize<NasProfile>(File.ReadAllText(path)):new NasProfile(); }
        public void Save(NasProfile profile) { string path=Path.Combine(home,"nas.json"); File.WriteAllText(path+".new",Core.Json.Serialize(profile)); if(File.Exists(path)) File.Replace(path+".new",path,null); else File.Move(path+".new",path); }
        private List<NasProfile> Connections() {
            string file=Path.Combine(home,"nas-connections.json");
            return File.Exists(file)?Core.Json.Deserialize<List<NasProfile>>(File.ReadAllText(file)):new List<NasProfile> { Load() };
        }
        public void RememberConnection(NasProfile profile) {
            var connections=Connections(); connections.RemoveAll(p=>p.Letter==profile.Letter);
            connections.Add(new NasProfile { Host=profile.Host,RemoteShare=profile.RemoteShare,Letter=profile.Letter,RemoteUser=profile.RemoteUser });
            string file=Path.Combine(home,"nas-connections.json"); File.WriteAllText(file+".new",Core.Json.Serialize(connections));
            if(File.Exists(file)) File.Replace(file+".new",file,null); else File.Move(file+".new",file);
        }
        public static bool CanMaintain(string actual,string expected) { return actual!=null && string.Equals(actual,expected,StringComparison.OrdinalIgnoreCase); }
        public Task<string> MaintainConnections() { return monitor.Poll(async()=> {
            var messages=new List<string>();
            foreach(var p in Connections()) {
                if(!IsTailIp(p.Host)) continue;
                try {
                    string expected=RemotePath(p.Host,p.RemoteShare); ValidateLetter(p.Letter);
                    // Only mappings still owned by this profile are touched. A removed or reassigned drive stays untouched.
                    if(!CanMaintain(MappedPath(p.Letter),expected)) continue;
                    await Probe(p.Host).ConfigureAwait(false);
                    if(!Directory.Exists(p.Letter+@":\")) {
                        if(!CanMaintain(MappedPath(p.Letter),expected)) continue;
                        var resource=new NetResource { Type=1,LocalName=p.Letter+":",RemoteName=expected };
                        // Cached Windows credentials only: no background dialog, force-unmap or file closure.
                        var login=LoadRemoteLogin(p.Host,p.RemoteShare);
                        int error=WNetAddConnection3(IntPtr.Zero,ref resource,login==null?null:login.Password,login==null?null:login.User,0);
                        if(error!=0) throw ConnectionError(error,expected);
                        if(!Directory.Exists(p.Letter+@":\")) throw new IOException("Windows chưa truy cập được thư mục NAS.");
                    }
                    messages.Add(p.Letter+": đã kết nối");
                } catch(Exception e) { messages.Add(p.Letter+": mất kết nối — "+e.Message+" Sẽ thử lại tự động."); }
            }
            return messages.Count==0?"":string.Join("\n",messages);
        }); }
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
        public static string ConnectionPath(int error,string remoteName) {
            // Windows keeps the UNC name for persistent mappings that are offline (1201).
            return (error==0 || error==1201) && !string.IsNullOrWhiteSpace(remoteName)?remoteName:null;
        }
        public static string MappedPath(string letter) { ValidateLetter(letter); var buffer=new StringBuilder(2048); int length=buffer.Capacity; int error=WNetGetConnection(letter+":",buffer,ref length); return ConnectionPath(error,buffer.ToString()); }
        public static void ValidateMappingTarget(string actual,string expected,bool drivePresent,string letter) {
            if((actual!=null && !actual.Equals(expected,StringComparison.OrdinalIgnoreCase)) || (actual==null && drivePresent)) throw new Exception("Ký tự ổ "+letter+": đang được sử dụng bởi ổ khác. Chọn ký tự khác.");
        }
        public static void Map(IntPtr owner,string host,string share,string letter,string username=null,string password=null) {
            string path=RemotePath(host,share); ValidateLetter(letter);
            ValidateMappingTarget(MappedPath(letter),path,DriveInfo.GetDrives().Any(d=>d.Name.StartsWith(letter+":",StringComparison.OrdinalIgnoreCase)),letter);
            var resource=new NetResource { Type=1,LocalName=letter+":",RemoteName=path };
            int error=WNetAddConnection3(owner,ref resource,password,username,1);
            if(error!=0) throw ConnectionError(error,path);
        }
        public static void Unmap(string host,string share,string letter) {
            string expected=RemotePath(host,share),actual=MappedPath(letter);
            if(actual==null || !actual.Equals(expected,StringComparison.OrdinalIgnoreCase)) throw new Exception("Ổ này không trỏ tới NAS đã chọn; không ngắt ổ khác.");
            int error=WNetCancelConnection2(letter+":",1,false); if(error!=0) throw new Win32Exception(error,"Đóng file đang mở trên ổ NAS trước khi ngắt. "+new Win32Exception(error).Message);
        }
    }
    public class NasConnectionMonitor {
        private Task<string> pending;
        private DateTime next=DateTime.MinValue;
        // One worker maximum even when a Windows network call takes longer than expected.
        public async Task<string> Poll(Func<Task<string>> work) {
            if(pending==null) {
                if(DateTime.UtcNow<next) return null;
                pending=Task.Run(work);
            }
            if(await Task.WhenAny(pending,Task.Delay(100))!=pending) return null;
            try { return await pending; } finally { pending=null; next=DateTime.UtcNow.AddSeconds(60); }
        }
    }
}
