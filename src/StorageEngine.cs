using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
namespace GoogleNas {
 public class StorageStore {
  public readonly string Home;private readonly object gate=new object();
  public StorageStore(string home) { Home=home;new Services(home); }
  private byte[] Protect(byte[] data) { return ProtectedData.Protect(data,null,DataProtectionScope.CurrentUser); }
  public string Key {
   get { lock(gate) { string file=Path.Combine(Home,"config-key.dat");if(!File.Exists(file)){var bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);File.WriteAllBytes(file,Protect(Encoding.UTF8.GetBytes(Convert.ToBase64String(bytes))));} return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser)); } }
  }
  public List<StorageConnection> Load() { lock(gate) { string file=Path.Combine(Home,"connections.dat");if(!File.Exists(file))return new List<StorageConnection>();var clear=ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser);try{return Core.Json.Deserialize<List<StorageConnection>>(Encoding.UTF8.GetString(clear));}finally{Array.Clear(clear,0,clear.Length);} } }
  public void Save(List<StorageConnection> values) { lock(gate) { var bytes=Encoding.UTF8.GetBytes(Core.Json.Serialize(values));try{string file=Path.Combine(Home,"connections.dat");File.WriteAllBytes(file+".new",Protect(bytes));if(File.Exists(file))File.Replace(file+".new",file,null);else File.Move(file+".new",file);}finally{Array.Clear(bytes,0,bytes.Length);} } }
  public void Put(StorageConnection p) { lock(gate){StorageProviders.Validate(p);var all=Load();if(all.Any(x=>x.Id!=p.Id&&x.Letter==p.Letter))throw new Exception("Ký tự ổ đã được dùng trong kết nối khác.");all.RemoveAll(x=>x.Id==p.Id);all.Add(p);Save(all); }}
 }
 public class StorageRuntime { public Services Service;public string State="Đã lưu",Message="";public long? TotalBytes,UsedBytes,FreeBytes;public bool Native,PendingPause;public string Capacity="Chưa đọc quota";public DateTime NextHealth=DateTime.MinValue;public DateTime NextQuota=DateTime.MinValue;public Task QuotaJob;public Task Job;public DateTime Next=DateTime.MinValue; }
 public class StorageQuotaUnsupportedException : Exception {}
 public class StorageAuthenticationException : Exception { public StorageAuthenticationException(string message):base(message){} }
 public class StorageEngine {
  private bool stopping;
  private static readonly object inputEncodingGate=new object();
  private Process StartCommand(ProcessStartInfo info){lock(inputEncodingGate){Encoding previous=null;try{try{previous=Console.InputEncoding;Console.InputEncoding=new UTF8Encoding(false);}catch(IOException){}return Process.Start(info);}finally{if(previous!=null){try{Console.InputEncoding=previous;}catch(IOException){}}}}}
  internal Action<string> Diagnostics {get;set;}
  public readonly StorageStore Store;public readonly string Rclone;private readonly NasService bridge;
  private readonly Dictionary<string,StorageRuntime> live=new Dictionary<string,StorageRuntime>();
  public StorageEngine(string home,string rclone=null) { Store=new StorageStore(home);bridge=new NasService(home);Rclone=rclone??new Services(home).Rclone; }
  public StorageRuntime Runtime(StorageConnection p) { StorageRuntime r;if(!live.TryGetValue(p.Id,out r))live[p.Id]=r=new StorageRuntime();return r; }
  public string DirectoryFor(StorageConnection p) { StorageProviders.Validate(p);return Path.Combine(Store.Home,"mounts",p.Id); }
  public string ConfigFor(StorageConnection p) { return Path.Combine(DirectoryFor(p),"rclone.conf"); }
  private ProcessStartInfo Info(IEnumerable<string> args,string config=null) {
   if(!File.Exists(Rclone))throw new Exception("Thiếu rclone tích hợp. Chạy lại bộ cài để sửa.");
   var list=args.ToList();if(config!=null){list.AddRange(new[]{"--config",config,"--ask-password=false"});}
   var info=new ProcessStartInfo(Rclone,Core.Args(list)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)};info.EnvironmentVariables["RCLONE_CONFIG_PASS"]=Store.Key;return info;
  }
  private async Task<string> Run(IEnumerable<string> args,string config=null,string input=null,int timeout=60000) {
   using(var p=StartCommand(Info(args,config))) {
    var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();if(input!=null){byte[] bytes=new UTF8Encoding(false).GetBytes(input+"\n");try{await p.StandardInput.BaseStream.WriteAsync(bytes,0,bytes.Length);await p.StandardInput.BaseStream.FlushAsync();}finally{Array.Clear(bytes,0,bytes.Length);}}p.StandardInput.Close();
    var wait=Task.Run(()=>p.WaitForExit());if(await Task.WhenAny(wait,Task.Delay(timeout))!=wait){try{p.Kill();}catch{}await wait;await stdout;await stderr;throw new Exception("Hết thời gian chờ kết nối. Kiểm tra máy chủ và mạng rồi thử lại.");}
    string output=await stdout;string error=await stderr;if(p.ExitCode!=0&&Diagnostics!=null)Diagnostics("rclone command failure: "+string.Join(" ",args.Take(2))+" · "+error);if(p.ExitCode!=0&&(error.IndexOf("doesn't support about",StringComparison.OrdinalIgnoreCase)>=0||error.IndexOf("does not support about",StringComparison.OrdinalIgnoreCase)>=0))throw new StorageQuotaUnsupportedException();if(p.ExitCode!=0&&new[]{"login incorrect","authentication failed","authorization failed","invalid_grant","access denied","401 unauthorized","invalid credentials","logon failure","wrong password"}.Any(x=>error.IndexOf(x,StringComparison.OrdinalIgnoreCase)>=0))throw new StorageAuthenticationException("Đăng nhập bị từ chối. Tự kết nối được tắt để tránh khóa tài khoản; hãy kiểm tra cấu hình.");if(p.ExitCode!=0)throw new Exception("Không hoàn tất kết nối (rclone "+p.ExitCode+"). Kiểm tra địa chỉ, tài khoản, quyền truy cập và TLS/SSH. Không có mật khẩu được ghi ra thông báo.");return output;
   }
  }
  public async Task<Dictionary<string,string>> ReadExternalConfig(string config,string remote){var text=await Run(new[]{"config","dump"},config);var data=Core.Json.Deserialize<Dictionary<string,Dictionary<string,string>>>(text);return data.ContainsKey(remote)?data[remote]:null;}
  public void ResetAuthorization(StorageConnection p){StorageProviders.Validate(p);if(!StorageProviders.Find(p.Provider).OAuth)throw new Exception("Kết nối này không dùng OAuth.");string file=ConfigFor(p);if(File.Exists(file))File.Delete(file);var old=Store.Load().FirstOrDefault(x=>x.Id==p.Id);if(old!=null){old.AutoConnect=false;old.Verified=false;foreach(string key in new[]{"token","drive_id","drive_type","service_account_credentials"})old.Options.Remove(key);Store.Put(old);}}
  public async Task Configure(StorageConnection p,Func<Dictionary<string,object>,Task<string>> ask=null) {
   StorageProviders.Validate(p);string directory=DirectoryFor(p);var acl=new Services(directory);string cfg=ConfigFor(p);var data=new Dictionary<string,string>();
   if(File.Exists(cfg)) {
    string json=await Run(new[]{"config","dump"},cfg);var dump=Core.Json.Deserialize<Dictionary<string,Dictionary<string,string>>>(json);if(dump.ContainsKey("remote"))data=dump["remote"];
   }
   foreach(var pair in StorageProviders.Options(p)) {
    string value=pair.Value;if(new[]{"pass","key_file_pass"}.Contains(pair.Key)&&value!="")value=(await Run(new[]{"obscure","-"},null,value)).Trim();data[pair.Key]=value;
   }
   if(data.ContainsKey("service_account_file")&&data["service_account_file"]!="") { var json=File.ReadAllText(data["service_account_file"]);Core.Json.DeserializeObject(json);data["service_account_credentials"]=Core.Json.Serialize(Core.Json.DeserializeObject(json));data.Remove("service_account_file"); }
   string temp=cfg+".prepare-"+Guid.NewGuid().ToString("N");
   try {
    var text=new StringBuilder("[remote]\n");foreach(var pair in data) { if(pair.Value.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new Exception("Cấu hình không hợp lệ.");text.Append(pair.Key).Append(" = ").Append(pair.Value).Append('\n'); }
    File.WriteAllText(temp,text.ToString(),new UTF8Encoding(false));string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
    string passwordCommand=Core.Args(new[]{shell,"-NoLogo","-NoProfile","-NonInteractive","-WindowStyle","Hidden","-Command","[Console]::Out.Write($env:RCLONE_CONFIG_PASS)"});
    await Run(new[]{"config","encryption","set","--password-command",passwordCommand},temp);
    if(File.Exists(cfg))File.Replace(temp,cfg,null);else File.Move(temp,cfg);
   } finally { if(File.Exists(temp))File.Delete(temp); }
   bool needsOAuth=StorageProviders.Find(p.Provider).OAuth && !data.ContainsKey("token") && !data.ContainsKey("service_account_credentials");
   if(needsOAuth) {
    if(ask==null)throw new Exception("Cần đăng nhập qua cửa sổ cấu hình trước khi tự kết nối.");
    string result=await Run(new[]{"config","update","remote","config_is_local","true","--non-interactive"},cfg,null,300000);
    for(int i=0;i<40;i++) {
     if(string.IsNullOrWhiteSpace(result))break;var question=Core.Json.Deserialize<Dictionary<string,object>>(result);object state;if(!question.TryGetValue("State",out state)||string.IsNullOrEmpty(Convert.ToString(state)))break;
     var option=question.ContainsKey("Option")?question["Option"] as Dictionary<string,object>:null;
     if(option==null)throw new Exception("Cấu hình OAuth yêu cầu thông tin chưa được hỗ trợ.");
     if(option.ContainsKey("IsPassword")&&Convert.ToBoolean(option["IsPassword"]))throw new Exception("Không đưa mật khẩu vào câu trả lời OAuth. Dùng trình duyệt để cấp quyền.");
     string answer=await ask(question);if(answer==null)throw new OperationCanceledException("Đã hủy đăng nhập.");
     result=await Run(new[]{"config","update","remote","--continue","--state",Convert.ToString(state),"--result",answer,"--non-interactive"},cfg,null,300000);
     if(i==39)throw new Exception("Luồng đăng nhập chưa kết thúc. Hãy thử lại.");
    }
   }
  }
  public async Task Test(StorageConnection p) { StorageProviders.Validate(p);await Run(new[]{"lsf","remote:"+p.Path.Trim('/'),"--max-depth","1","--contimeout","8s","--timeout","15s","--retries","1","--low-level-retries","1"},ConfigFor(p),null,45000); }
  public async Task Connect(StorageConnection p) {
   if(stopping)return;CyanSession.Require();var r=Runtime(p);r.PendingPause=false;if(r.Service!=null && r.Service.Running){await Test(p);r.State="Đã kết nối";r.Message="";return;}
   r.State="Đang kết nối";r.Message="";
   try {
    if(!File.Exists(ConfigFor(p)))await Configure(p);
    await Test(p);if(r.PendingPause)return;
    if(p.Provider=="smb"&&NasService.IsTailIp(p.Get("host"))&&System.Text.RegularExpressions.Regex.IsMatch(p.Path.Trim('/'),@"^[A-Za-z][A-Za-z0-9_-]{0,47}$")) {
     var native=NativeProfile(p);var normalized=StorageProviders.Options(p);string account=normalized["domain"]==""?normalized["user"]:normalized["domain"]+@"\"+normalized["user"];
     await Task.Run(()=>bridge.MapAndSave(IntPtr.Zero,native,account,p.Get("pass")));bridge.RememberConnection(native);bridge.MarkConnected(p.Letter);r.Native=true;r.State="Đã kết nối";r.QuotaJob=ReadQuota(p,r);r.NextQuota=DateTime.UtcNow.AddMinutes(5);if(!r.PendingPause){p.Verified=true;Store.Put(p);}return;
    }
    if(DriveInfo.GetDrives().Any(d=>d.Name.StartsWith(p.Letter+":",StringComparison.OrdinalIgnoreCase)))throw new Exception("Ký tự "+p.Letter+": đang được dùng. Tool không thay thế ổ khác.");
    var service=new Services(DirectoryFor(p)){ConfigPassword=Store.Key,VolumeName=p.Name,ExecutableOverride=Rclone};
    var settings=new Settings{Remote="remote",Folder=p.Path.Trim('/'),Letter=p.Letter,CacheGB=20};
    await Task.Run(()=>service.Start(settings,p.ReadOnly));r.Service=service;if(r.PendingPause){await service.Stop();return;}
    var probe=new DriveProbe();for(int i=0;i<15;i++){await Task.Delay(500);if(!service.Running)throw new Exception("Tiến trình gắn ổ đã dừng. Kiểm tra cấu hình, driver và quyền truy cập.");if(await probe.Ready(p.Letter+@":\",500)){r.State="Đã kết nối";r.QuotaJob=ReadQuota(p,r);r.NextQuota=DateTime.UtcNow.AddMinutes(5);if(!r.PendingPause){p.Verified=true;Store.Put(p);}return;}}
    r.State="Đang chuẩn bị ổ";
   }catch(Exception e){var win=e as System.ComponentModel.Win32Exception;if(e is StorageAuthenticationException||(win!=null&&new[]{86,1326,1909,1331,1330,1907,1385,1219}.Contains(win.NativeErrorCode))){p.AutoConnect=false;Store.Put(p);}r.State="Mất kết nối";r.Message=e.Message;r.Next=DateTime.UtcNow.AddSeconds(15);throw;}
  }
  public async Task Stop(StorageConnection p,bool pause=true) {
   var r=Runtime(p);r.PendingPause=true;if(pause){p.AutoConnect=false;Store.Put(p);}if(r.Job!=null&&!r.Job.IsCompleted)await r.Job;
   if(r.Native){await bridge.DisconnectSaved(NativeProfile(p));r.Native=false;}if(r.Service!=null)await r.Service.Stop();r.State="Tạm ngắt";r.Message="";
  }
  private static NasProfile NativeProfile(StorageConnection p){return new NasProfile{Host=p.Get("host"),RemoteShare=p.Path.Trim('/'),Letter=p.Letter,RemoteUser=p.Get("user"),AutoConnect=p.AutoConnect};}
  public async Task StopAll() { stopping=true;try{try{await Task.WhenAll(live.Values.SelectMany(r=>new[]{r.Job,r.QuotaJob}).Where(t=>t!=null));}catch{}foreach(var pair in live.ToArray())if(pair.Value.Service!=null)await pair.Value.Service.Stop();}finally{stopping=false;} }
  public void NetworkChanged() { bridge.NetworkChanged();foreach(var r in live.Values){r.Next=DateTime.MinValue;r.NextHealth=DateTime.MinValue;} }
  public async Task Poll() {
   if(stopping)return;foreach(var p in Store.Load().Where(x=>x.AutoConnect&&x.Verified)) {
    var r=Runtime(p);if((r.QuotaJob==null||r.QuotaJob.IsCompleted)&&DateTime.UtcNow>=r.NextQuota){r.NextQuota=DateTime.UtcNow.AddMinutes(5);r.QuotaJob=ReadQuota(p,r);}if(r.Job!=null&&!r.Job.IsCompleted)continue;if(DateTime.UtcNow<r.Next)continue;r.Next=DateTime.UtcNow.AddSeconds(15);
    r.Job=Maintain(p,r);await Task.WhenAny(r.Job,Task.Delay(40));
   }
  }
  public async Task ReadQuota(StorageConnection p,StorageRuntime r) {
   try{if(!File.Exists(ConfigFor(p)))return;string json=await Run(new[]{"about","remote:"+p.Path.Trim('/'),"--json","--contimeout","5s","--timeout","10s","--retries","1"},ConfigFor(p),null,16000);var quota=Core.Json.Deserialize<Dictionary<string,object>>(json);r.Capacity=CapacityInfo.FromQuota(quota);object number;r.TotalBytes=quota.TryGetValue("total",out number)&&number!=null?(long?)Convert.ToInt64(number):null;r.UsedBytes=quota.TryGetValue("used",out number)&&number!=null?(long?)Convert.ToInt64(number):null;r.FreeBytes=quota.TryGetValue("free",out number)&&number!=null?(long?)Convert.ToInt64(number):null;}
   catch(StorageQuotaUnsupportedException){r.Capacity="Không cung cấp quota";}catch{r.Capacity="Chưa đọc được quota";}
  }
  private async Task Maintain(StorageConnection p,StorageRuntime r) {
   try {
    if(r.Native){await bridge.MaintainConnections();var state=bridge.ConnectionState(p.Letter);r.State=state.State=="Connected"?"Đã kết nối":state.State=="AuthRequired"?"Cần sửa đăng nhập":"Đang nối lại";r.Message=state.Message;return;}
    if(r.Service!=null&&r.Service.Running) {
     var stats=await r.Service.Rc("vfs/stats");if(DateTime.UtcNow>=r.NextHealth){r.NextHealth=DateTime.UtcNow.AddSeconds(30);await Run(new[]{"lsjson","remote:"+p.Path.Trim('/'),"--stat","--contimeout","5s","--timeout","8s","--retries","1","--low-level-retries","1"},ConfigFor(p),null,14000);}r.State="Đã kết nối";r.Message="";
     return; // rclone keeps its live VFS and retries transport; never discard queued writes on Wi-Fi changes.
    }
    if(!Store.Load().Any(x=>x.Id==p.Id&&x.AutoConnect))return;
    await Connect(p);
   }catch(Exception e){r.State=r.Service!=null&&r.Service.Running?"Đang nối lại":"Mất kết nối";r.Message=e.Message;}
  }
 }
}
