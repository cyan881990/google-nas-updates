using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
namespace GoogleNas {
 public class CyanApiException : Exception {
  public readonly string Code; public readonly int RetryAfter;
  public CyanApiException(string code,int retry=0):base(MessageFor(code)){Code=code;RetryAfter=retry;}
  static string MessageFor(string code){switch(code){
   case "invalid_credentials":return "Email hoặc mật khẩu chưa đúng.";
   case "email_not_verified":return "Hãy xác nhận email trước khi đăng nhập.";
   case "no_license":return "Tài khoản chưa có quyền sử dụng CYAN NAS. Mở trang tài khoản CYANPRO để kiểm tra.";
   case "license_expired":return "License đã hết hạn. Mở CYANPRO để gia hạn.";
   case "device_limit_reached":return "Đã đủ số thiết bị. Gỡ một thiết bị cũ trên CYANPRO rồi thử lại.";
   case "device_revoked":return "Thiết bị này đã bị thu hồi. Vui lòng đăng nhập lại.";
   case "invalid_token":case "missing_token":return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
   case "rate_limited":return "Đang có quá nhiều yêu cầu. Vui lòng chờ trước khi thử lại.";
   case "invalid_api_key":return "Khóa kết nối CYANPRO không còn hợp lệ. Liên hệ nhà phát hành.";
   default:return "CYANPRO chưa chấp nhận yêu cầu ("+code+").";
  }}
 }
 public class CyanApi {
  public const string Origin="https://cyanpro.app";
  // This is the public client key. Administrative credentials never belong in the application.
  public const string ClientKey="cyk_live_50cd40e4e1e3e20463bd3cc4cb949577";
  internal Func<string,object,string,Task<Dictionary<string,object>>> Transport;
  static long pausedUntil;
  public async Task<Dictionary<string,object>> Send(string path,object body=null,string bearer=null){
   long remaining=Interlocked.Read(ref pausedUntil)-DateTime.UtcNow.Ticks;if(remaining>0)throw new CyanApiException("rate_limited",(int)Math.Ceiling(TimeSpan.FromTicks(remaining).TotalSeconds));
   if(Transport!=null)return await Transport(path,body,bearer);
   using(var c=Services.Client()){
    c.Encoding=Encoding.UTF8;c.Headers["X-Api-Key"]=ClientKey;if(!string.IsNullOrEmpty(bearer))c.Headers[HttpRequestHeader.Authorization]="Bearer "+bearer;
    if(body!=null)c.Headers[HttpRequestHeader.ContentType]="application/json";
    using(var timeout=new CancellationTokenSource(15000))using(timeout.Token.Register(()=>c.CancelAsync())){
     string raw;
     try{raw=body==null?await c.DownloadStringTaskAsync(Origin+path):await c.UploadStringTaskAsync(Origin+path,"POST",Core.Json.Serialize(body));}
     catch(WebException e){var response=e.Response as HttpWebResponse;if(response==null)throw;using(response){int retry;int.TryParse(response.Headers["Retry-After"],out retry);string code="http_"+(int)response.StatusCode;try{using(var reader=new StreamReader(response.GetResponseStream()))code=Text(Core.Json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd()),"error",code);}catch{}if((int)response.StatusCode==429)Interlocked.Exchange(ref pausedUntil,DateTime.UtcNow.AddSeconds(Math.Max(1,retry)).Ticks);throw new CyanApiException(code,retry);}}
     var result=Core.Json.Deserialize<Dictionary<string,object>>(raw);if(result==null||!Flag(result,"ok"))throw new CyanApiException(Text(result,"error","invalid_response"));return result;
    }
   }
  }
  internal static string Text(Dictionary<string,object> d,string key,string fallback=""){object v;return d!=null&&d.TryGetValue(key,out v)&&v!=null?Convert.ToString(v):fallback;}
  internal static long Number(Dictionary<string,object> d,string key,long fallback=0){long n;return long.TryParse(Text(d,key),out n)?n:fallback;}
  internal static bool Flag(Dictionary<string,object> d,string key){return Text(d,key).Equals("true",StringComparison.OrdinalIgnoreCase);}
  internal static Dictionary<string,object> Object(Dictionary<string,object> d,string key){object v;return d!=null&&d.TryGetValue(key,out v)?v as Dictionary<string,object>:null;}
  public Task<Dictionary<string,object>> Version(){return Send("/api/v1/version?current="+Uri.EscapeDataString(Core.Version)+"&platform=windows");}
 }
 public class CyanSessionState {
  public string Access="",Refresh="",Email="",Fingerprint="";public long AccessUntil,ServerTime,CheckedLocal,ObservedLocal;public bool Valid;
  public Dictionary<string,object> License=new Dictionary<string,object>();
 }
 public class CyanSession {
  public static CyanSession Current;public static bool Enforced,UpdateRequired;
  readonly string file,fingerprint;readonly CyanApi api;readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
  internal Func<long> Clock=()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds();
  internal CyanSessionState State=new CyanSessionState();
  public event Action Changed;public string Status {get;private set;}
  public bool Offline {get;private set;}
  public bool Allowed {get{long now=Clock();long elapsed=now-State.CheckedLocal;long expiry=CyanApi.Number(State.License,"expires_at");return State.Valid&&State.Fingerprint==fingerprint&&now>=State.ObservedLocal-120&&elapsed>=-120&&elapsed<=3*86400&&(expiry<=0||State.ServerTime+Math.Max(0,elapsed)<expiry);}}
  public string Email {get{return State.Email;}} public bool HasSession {get{return !string.IsNullOrEmpty(State.Refresh);}}
  public string Details {get{if(!Allowed)return State.Valid?"Cần kết nối CYANPRO để xác thực lại license.\n"+Status:Status;var l=State.License;long expiry=CyanApi.Number(l,"expires_at");string until=expiry>0?DateTimeOffset.FromUnixTimeSeconds(expiry).ToLocalTime().ToString("dd/MM/yyyy HH:mm"):"Không giới hạn thời hạn";return State.Email+"\nGói: "+CyanApi.Text(l,"plan","CYAN NAS")+(CyanApi.Flag(l,"is_trial")?" · Dùng thử, còn "+CyanApi.Text(l,"days_left")+" ngày":"")+"\nHạn dùng: "+until+"\nThiết bị: "+CyanApi.Text(l,"devices_in_use","?")+" / "+CyanApi.Text(l,"max_devices","?")+"\n"+Status;}}
  public CyanSession(string home,CyanApi client=null,string device=null){Status="Chưa đăng nhập CYANPRO";new Services(home);file=Path.Combine(home,"cyanpro-session.dat");api=client??new CyanApi();fingerprint=device??DeviceFingerprint();try{State=Core.Json.Deserialize<CyanSessionState>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser)));if(State==null||State.Fingerprint!=fingerprint)State=new CyanSessionState();}catch{State=new CyanSessionState();}if(HasSession)Status="Đang chờ kiểm tra license";}
  static string DeviceFingerprint(){string machine;using(var k=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))machine=k==null?null:Convert.ToString(k.GetValue("MachineGuid"));if(string.IsNullOrWhiteSpace(machine))throw new Exception("Không đọc được định danh máy Windows.");using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes("CYAN-NAS|"+machine))).Replace("-","").ToLowerInvariant();}
  void Save(){State.ObservedLocal=Math.Max(State.ObservedLocal,Clock());byte[] clear=Encoding.UTF8.GetBytes(Core.Json.Serialize(State));try{File.WriteAllBytes(file+".new",ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser));if(File.Exists(file))File.Replace(file+".new",file,null);else File.Move(file+".new",file);}finally{Array.Clear(clear,0,clear.Length);}}
  void Notify(){if(Changed!=null)Changed();}
  void Tokens(Dictionary<string,object> r){string access=CyanApi.Text(r,"access_token"),refresh=CyanApi.Text(r,"refresh_token");if(access=="")throw new CyanApiException("invalid_response");State.Access=access;if(refresh!="")State.Refresh=refresh;State.AccessUntil=Clock()+Math.Max(30,CyanApi.Number(r,"expires_in",3600))-30;Save();}
  void Accept(Dictionary<string,object> r){var license=CyanApi.Object(r,"license");long server=CyanApi.Number(r,"server_time");if(license==null||server<=0)throw new CyanApiException("invalid_response");State.License=license;State.ServerTime=server;State.CheckedLocal=Clock();State.ObservedLocal=Clock();State.Valid=true;Offline=false;Status="License đã xác thực với CYANPRO";Save();}
  async Task Refresh(){if(!HasSession)throw new CyanApiException("invalid_token");Tokens(await api.Send("/api/v1/auth/refresh",new{refresh_token=State.Refresh}));}
  async Task<Dictionary<string,object>> Authorized(string path,object body){if(State.AccessUntil<=Clock())await Refresh();try{return await api.Send(path,body,State.Access);}catch(CyanApiException e){if(e.Code!="invalid_token"&&e.Code!="missing_token")throw;}await Refresh();return await api.Send(path,body,State.Access);}
  void Deny(CyanApiException e){State.Valid=false;Status=e.Message;if(e.Code=="invalid_token"||e.Code=="device_revoked"){State.Access="";State.Refresh="";}Save();Notify();}
  public async Task Login(string email,string password){await gate.WaitAsync();bool claimed=false;try{var result=await api.Send("/api/v1/auth/login",new{email=email.Trim(),password=password,fingerprint=fingerprint,device_name=Environment.MachineName,platform="Windows",app_version=Core.Version});claimed=true;State=new CyanSessionState{Email=email.Trim(),Fingerprint=fingerprint};Tokens(result);var account=CyanApi.Object(result,"account");State.Email=CyanApi.Text(account,"email",State.Email);var verified=await Authorized("/api/v1/license/verify",new{fingerprint=fingerprint,app_version=Core.Version});if(!CyanApi.Flag(verified,"valid"))throw new CyanApiException(CyanApi.Text(verified,"reason","license_expired"));Accept(verified);Notify();}catch(CyanApiException e){if(claimed)Deny(e);else{Status=e.Message;Notify();}throw;}finally{gate.Release();}}
  public async Task Verify(){await gate.WaitAsync();try{if(!HasSession){Status="Đăng nhập CYANPRO để kích hoạt trên máy này";Notify();return;}var result=await Authorized("/api/v1/license/verify",new{fingerprint=fingerprint,app_version=Core.Version});if(!CyanApi.Flag(result,"valid"))throw new CyanApiException(CyanApi.Text(result,"reason","license_expired"));Accept(result);Notify();}catch(CyanApiException e){if(e.Code=="rate_limited"||e.Code.StartsWith("http_5")){Offline=true;Status=Allowed?"CYANPRO tạm bận · đang dùng license đã lưu":"Chờ CYANPRO phản hồi để xác thực license";Save();Notify();}else Deny(e);}catch(WebException){Offline=true;Status=Allowed?"Mất mạng · dùng license đã xác thực, tối đa 72 giờ":"Cần Internet để xác thực lại license";Save();Notify();}catch(OperationCanceledException){Offline=true;Status=Allowed?"CYANPRO chưa phản hồi · đang dùng license đã lưu":"Cần kết nối CYANPRO để xác thực";Save();Notify();}finally{gate.Release();}}
  public async Task Logout(){await gate.WaitAsync();try{if(HasSession)await Authorized("/api/v1/auth/logout",new{});State=new CyanSessionState();if(File.Exists(file))File.Delete(file);Status="Đã đăng xuất và giải phóng thiết bị";Offline=false;Notify();}finally{gate.Release();}}
  public static void Require(){if(Enforced&&UpdateRequired)throw new Exception("Cần cập nhật phiên bản CYAN NAS bắt buộc. Mở Cài đặt → Kiểm tra cập nhật.");if(Enforced&&(Current==null||!Current.Allowed))throw new Exception("Cần đăng nhập và license hợp lệ. Mở nút Tài khoản CYANPRO trên thanh tiêu đề.");}
 }
}
