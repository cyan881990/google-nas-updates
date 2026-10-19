using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
namespace GoogleNas {
    public class NasItemState {
        public string Key="",State="Waiting",Message="Chờ kiểm tra";
    }
    internal class NasRestoreOperations {
        internal Func<string,string> Mapping;
        internal Func<string,bool> Occupied,Ready;
        internal Func<string,Task> Probe;
        internal Action<NasProfile,NasLogin> Connect;
        internal Action<NasProfile> Disconnect;
    }
    public partial class NasService {
        private readonly NasRestoreOperations restore;
        private static NasRestoreOperations NativeRestoreOperations() {
            return new NasRestoreOperations {
                Mapping=MappedPath,Occupied=letter=>DriveInfo.GetDrives().Any(d=>d.Name.StartsWith(letter+":",StringComparison.OrdinalIgnoreCase)),Ready=path=>Directory.Exists(path),Probe=Probe,Disconnect=p=>Unmap(p.Host,p.RemoteShare,p.Letter),
                Connect=(p,login)=>{ var resource=new NetResource { Type=1,LocalName=p.Letter+":",RemoteName=RemotePath(p.Host,p.RemoteShare) }; int error=WNetAddConnection3(IntPtr.Zero,ref resource,login==null?null:login.Password,login==null?null:login.User,1); if(error!=0) throw ConnectionError(error,resource.RemoteName); }
            };
        }
        private readonly object catalogGate=new object();
        private readonly Dictionary<string,NasConnectionMonitor> workers=new Dictionary<string,NasConnectionMonitor>();
        private readonly Dictionary<string,NasItemState> connectionStates=new Dictionary<string,NasItemState>();
        private List<NasProfile> ReadProfiles(string name) { string file=Path.Combine(home,name); return File.Exists(file)?Core.Json.Deserialize<List<NasProfile>>(File.ReadAllText(file)):new List<NasProfile>(); }
        private void WriteProfiles(string name,List<NasProfile> profiles) {
            Directory.CreateDirectory(home); string file=Path.Combine(home,name); File.WriteAllText(file+".new",Core.Json.Serialize(profiles));
            if(File.Exists(file)) File.Replace(file+".new",file,null); else File.Move(file+".new",file);
        }
        public List<NasProfile> LocalShares() {
            lock(catalogGate) {
                var profiles=ReadProfiles("nas-shares.json");
                if(!File.Exists(Path.Combine(home,"nas-shares.json"))) { var legacy=Load(); if(!string.IsNullOrWhiteSpace(legacy.Folder)) profiles.Add(legacy); }
                return profiles;
            }
        }
        public void RememberShare(NasProfile profile) {
            ValidateShare(profile.Share); if(!string.IsNullOrWhiteSpace(profile.User)) ValidateUser(profile.User);
            lock(catalogGate) { var profiles=LocalShares(); profiles.RemoveAll(p=>string.Equals(p.Share,profile.Share,StringComparison.OrdinalIgnoreCase)); profiles.Add(Core.Json.Deserialize<NasProfile>(Core.Json.Serialize(profile))); WriteProfiles("nas-shares.json",profiles); }
        }
        public void ForgetShare(string name) { lock(catalogGate) { var profiles=LocalShares(); profiles.RemoveAll(p=>string.Equals(p.Share,name,StringComparison.OrdinalIgnoreCase)); WriteProfiles("nas-shares.json",profiles); } }
        public List<NasProfile> Connections() {
            lock(catalogGate) {
                var profiles=ReadProfiles("nas-connections.json");
                if(!File.Exists(Path.Combine(home,"nas-connections.json"))) { var legacy=Load(); if(IsTailIp(legacy.Host)) { legacy.AutoConnect=false; profiles.Add(legacy); } }
                return profiles;
            }
        }
        public void RememberConnection(NasProfile profile) {
            RemotePath(profile.Host,profile.RemoteShare); ValidateLetter(profile.Letter);
            lock(catalogGate) {
                ValidateSavedTarget(profile);
                var profiles=Connections(); profiles.RemoveAll(p=>p.Letter==profile.Letter);
                profiles.Add(new NasProfile { Host=profile.Host,RemoteShare=profile.RemoteShare,Letter=profile.Letter,RemoteUser=profile.RemoteUser,AutoConnect=profile.AutoConnect }); WriteProfiles("nas-connections.json",profiles);
            }
        }
        public void ValidateSavedTarget(NasProfile profile) {
            RemotePath(profile.Host,profile.RemoteShare); ValidateLetter(profile.Letter);
            lock(catalogGate) { if(!File.Exists(Path.Combine(home,"nas-connections.json"))) return; var existing=Connections().FirstOrDefault(p=>p.Letter==profile.Letter); if(existing!=null && !string.Equals(RemotePath(existing.Host,existing.RemoteShare),RemotePath(profile.Host,profile.RemoteShare),StringComparison.OrdinalIgnoreCase)) throw new Exception("Ký tự ổ "+profile.Letter+": đã có cấu hình NAS khác. Chọn ký tự khác hoặc bỏ cấu hình cũ trước."); }
        }
        public void PauseConnection(string letter) {
            lock(catalogGate) { var profiles=Connections(); foreach(var p in profiles) if(p.Letter==letter) p.AutoConnect=false; WriteProfiles("nas-connections.json",profiles); SetConnectionState(letter,"Paused","Tạm ngắt · không tự gắn lại"); }
        }
        public void ForgetConnection(string letter) { lock(catalogGate) { var profiles=Connections(); profiles.RemoveAll(p=>p.Letter==letter); WriteProfiles("nas-connections.json",profiles); connectionStates.Remove(letter); } }
        public static bool CanMaintain(string actual,string expected) { return actual!=null && string.Equals(actual,expected,StringComparison.OrdinalIgnoreCase); }
        public static bool CanRestore(string actual,string expected,bool drivePresent) { return CanMaintain(actual,expected) || (actual==null && !drivePresent); }
        private bool StillAutomatic(NasProfile p) { return Connections().Any(x=>x.Letter==p.Letter && x.AutoConnect && string.Equals(RemotePath(x.Host,x.RemoteShare),RemotePath(p.Host,p.RemoteShare),StringComparison.OrdinalIgnoreCase)); }
        public NasItemState ConnectionState(string letter) {
            lock(catalogGate) { NasItemState state; return connectionStates.TryGetValue(letter,out state)?new NasItemState { Key=state.Key,State=state.State,Message=state.Message }:new NasItemState { Key=letter }; }
        }
        private void SetConnectionState(string letter,string state,string message) { lock(catalogGate) connectionStates[letter]=new NasItemState { Key=letter,State=state,Message=message }; }
        public void MarkConnected(string letter) { SetConnectionState(letter,"Connected","Đã kết nối · tự gắn khi mở tool"); }
        public void MarkConfigurationSaved(string letter) { SetConnectionState(letter,"Saved","Đã lưu · bấm Gắn ổ NAS để xác thực"); }
        // UI-thread wake-up preserves a single Windows worker per drive.
        public void NetworkChanged() {
            foreach(var p in Connections().Where(x=>x.AutoConnect)) {
                lock(catalogGate) { NasConnectionMonitor worker; if(workers.TryGetValue(p.Letter,out worker)) worker.Wake(); }
                SetConnectionState(p.Letter,"Connecting","Mạng vừa thay đổi · đang chờ NAS kết nối lại");
            }
        }
        private void RestoreConnection(NasProfile p,NasLogin login) {
            try { restore.Connect(p,login); }
            catch(Win32Exception e) {
                // Windows may retain the letter of a dead SMB mapping after a Wi-Fi switch.
                // Detach only our exact mapping without forcing open files closed.
                string expected=RemotePath(p.Host,p.RemoteShare);
                if(e.NativeErrorCode!=85 || restore.Disconnect==null || !StillAutomatic(p) || !CanMaintain(restore.Mapping(p.Letter),expected) || restore.Ready(p.Letter+@":\")) throw;
                restore.Disconnect(p);
                if(!StillAutomatic(p) || !CanRestore(restore.Mapping(p.Letter),expected,restore.Occupied(p.Letter))) return;
                restore.Connect(p,login);
            }
        }
        public async Task<string> MaintainConnections() {
            var polls=new List<Task<string>>();
            foreach(var item in Connections()) {
                var p=item; if(!IsTailIp(p.Host)) continue;
                if(!p.AutoConnect) { if(ConnectionState(p.Letter).State=="Waiting") SetConnectionState(p.Letter,"Paused","Tự kết nối đang tắt · bấm Gắn ổ NAS để bật"); continue; }
                NasConnectionMonitor worker;
                lock(catalogGate) { if(!workers.TryGetValue(p.Letter,out worker)) workers[p.Letter]=worker=new NasConnectionMonitor(); }
                polls.Add(worker.Poll(async()=> {
                    try {
                        string expected=RemotePath(p.Host,p.RemoteShare); ValidateLetter(p.Letter);
                        string actual=restore.Mapping(p.Letter); bool occupied=restore.Occupied(p.Letter);
                        if(!CanRestore(actual,expected,occupied)) { SetConnectionState(p.Letter,"Conflict","Ký tự ổ đang được dùng bởi ổ khác"); return ""; }
                        SetConnectionState(p.Letter,"Connecting","Đang kiểm tra / tự kết nối");
                        await restore.Probe(p.Host).ConfigureAwait(false);
                        if(!StillAutomatic(p)) return "";
                        if(actual==null || !restore.Ready(p.Letter+@":\")) {
                            actual=restore.Mapping(p.Letter); occupied=restore.Occupied(p.Letter);
                            if(!StillAutomatic(p) || !CanRestore(actual,expected,occupied)) return "";
                            var login=LoadRemoteLogin(p.Host,p.RemoteShare); RestoreConnection(p,login);
                        }
                        if(!StillAutomatic(p)) return "";
                        if(!restore.Ready(p.Letter+@":\")) throw new IOException("Windows chưa truy cập được thư mục NAS.");
                        MarkConnected(p.Letter);
                    } catch(Exception e) {
                        if(StillAutomatic(p)) {
                            var windows=e as Win32Exception;
                            if(windows!=null && new[]{86,1326,1909,1331,1330,1907,1385,1219}.Contains(windows.NativeErrorCode)) { PauseConnection(p.Letter); SetConnectionState(p.Letter,"AuthRequired","Cần sửa đăng nhập · "+e.Message+" · Đã tắt tự thử để tránh khóa tài khoản"); }
                            else SetConnectionState(p.Letter,"Offline",e.Message+" · Tự thử lại sau 10 giây khi mạng / Tailscale sẵn sàng");
                        }
                    }
                    return "";
                },()=>ConnectionState(p.Letter).State=="Offline"?10:60));
            }
            await Task.WhenAll(polls);
            return string.Join("\n",Connections().Select(p=>p.Letter+": "+ConnectionState(p.Letter).Message));
        }
        public async Task DisconnectSaved(NasProfile p,bool forget=false) {
            ValidateSavedTarget(p);
            if(!Connections().Any(x=>x.Letter==p.Letter && string.Equals(RemotePath(x.Host,x.RemoteShare),RemotePath(p.Host,p.RemoteShare),StringComparison.OrdinalIgnoreCase))) throw new Exception("Chọn đúng NAS trong danh sách đã lưu trước khi tạm ngắt.");
            PauseConnection(p.Letter); NasConnectionMonitor worker;
            lock(catalogGate) workers.TryGetValue(p.Letter,out worker);
            if(worker!=null && !await worker.WaitIdle()) throw new Exception("Đã tắt tự kết nối. Windows còn kiểm tra ổ này; chờ tác vụ xong rồi bấm Tạm ngắt lại.");
            await Task.Run(()=>{ var actual=MappedPath(p.Letter); if(actual!=null) Unmap(p.Host,p.RemoteShare,p.Letter); });
            if(forget) ForgetConnection(p.Letter);
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct ShareInfo1 { public string Name; public uint Type; public string Remark; }
        [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)] private static extern int NetShareGetInfo(string server,string name,int level,out IntPtr buffer);
        [DllImport("Netapi32.dll")] private static extern int NetApiBufferFree(IntPtr buffer);
        public static NasItemState ReadShareState(string name) {
            ValidateShare(name); IntPtr buffer=IntPtr.Zero;
            try {
                int error=NetShareGetInfo(null,name,1,out buffer);
                if(error==2310) return new NasItemState { Key=name,State="Stopped",Message="Đã dừng / chưa bật" };
                if(error!=0) return new NasItemState { Key=name,State="Unknown",Message="Chưa xác minh · Windows "+error };
                var info=(ShareInfo1)Marshal.PtrToStructure(buffer,typeof(ShareInfo1));
                bool owned=info.Remark=="GoogleNas:"+WindowsIdentity.GetCurrent().User.Value;
                return new NasItemState { Key=name,State=owned?"Enabled":"Foreign",Message=owned?"Đang chia sẻ":"Tên đã dùng bởi chia sẻ khác" };
            } finally { if(buffer!=IntPtr.Zero) NetApiBufferFree(buffer); }
        }
    }
}
