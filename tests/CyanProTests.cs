using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GoogleNas;
class CyanProTests {
 static int count;static void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
 static Dictionary<string,object> Data(string json){return Core.Json.Deserialize<Dictionary<string,object>>(json);}
 static void Reject(Action work,string name){bool rejected=false;try{work();}catch{rejected=true;}Check(rejected,name);}
 static long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();static int verifies,refreshes,logouts;static string mode="valid";
 static Task<Dictionary<string,object>> Server(string path,object body,string token){
  if(mode=="network")throw new WebException("fixture offline");
  if(path.EndsWith("/login")){if(mode=="badlogin")throw new CyanApiException("invalid_credentials");Check(Core.Json.Serialize(body).Contains("test-pass"),"Password sent to login only");return Task.FromResult(Data("{\"ok\":true,\"access_token\":\"fixture-access\",\"refresh_token\":\"fixture-refresh\",\"expires_in\":3600}"));}
  if(path.EndsWith("/refresh")){refreshes++;return Task.FromResult(Data("{\"ok\":true,\"access_token\":\"rotated-access\",\"refresh_token\":\"rotated-refresh\",\"expires_in\":3600}"));}
  if(path.EndsWith("/logout")){Check(!string.IsNullOrEmpty(token),"Logout authenticated");logouts++;return Task.FromResult(Data("{\"ok\":true}"));}
  if(path.EndsWith("/verify")){verifies++;Check(!string.IsNullOrEmpty(token),"Verify authenticated");if(mode=="revoked")throw new CyanApiException("device_revoked");if(mode=="expired")return Task.FromResult(Data("{\"ok\":true,\"valid\":false,\"reason\":\"license_expired\"}"));return Task.FromResult(Data("{\"ok\":true,\"valid\":true,\"server_time\":"+now+",\"license\":{\"status\":\"active\",\"plan\":\"Free\",\"max_devices\":5,\"devices_in_use\":1,\"expires_at\":"+(now+10*86400)+"}}"));}
  throw new Exception("Unexpected API path");
 }
 static async Task Run(string home){
  var api=new CyanApi{Transport=Server};var session=new CyanSession(home,api,"fixture-device"){Clock=()=>now};Check(!session.Allowed&&!session.HasSession,"Cold start requires sign in");await session.Login("sample@example.test","test-pass");Check(session.Allowed&&verifies==1,"Login followed by authoritative verification");Check(session.Details.Contains("Free")&&session.Details.Contains("1 / 5"),"Plan and device count");
  string file=Path.Combine(home,"cyanpro-session.dat");byte[] raw=File.ReadAllBytes(file);string text=Encoding.UTF8.GetString(raw);Check(!text.Contains("fixture-refresh")&&!text.Contains("test-pass")&&!text.Contains("sample@example"),"DPAPI storage protects tokens and account");string clear=Encoding.UTF8.GetString(ProtectedData.Unprotect(raw,null,DataProtectionScope.CurrentUser));Check(!clear.Contains("test-pass"),"Password never persisted");
  var restored=new CyanSession(home,api,"fixture-device"){Clock=()=>now};Check(restored.Allowed&&restored.HasSession,"Encrypted session restore");Check(!new CyanSession(home,api,"different-device").HasSession,"Fingerprint mismatch rejects copied session");
  mode="badlogin";try{await restored.Login("other@example.test","test-pass");}catch(CyanApiException){}Check(restored.Allowed&&restored.Email=="sample@example.test","Failed login does not destroy existing valid session");mode="valid";
  now+=3700;await restored.Verify();Check(refreshes==1&&restored.State.Refresh=="rotated-refresh","Expired access token refreshed and rotation saved");Check(restored.Allowed,"Refreshed session verified");
  mode="network";now+=3600;await restored.Verify();Check(restored.Allowed&&restored.Offline,"Network failure permits bounded cached license");long checkpoint=restored.State.CheckedLocal;now=checkpoint+3*86400+1;Check(!restored.Allowed,"Offline grace ends after 72 hours");now=checkpoint-1000;Check(!restored.Allowed,"Clock rollback cannot extend grace");now=checkpoint+7200;Check(restored.Allowed,"Normal clock remains within grace");restored.State.License["expires_at"]=restored.State.ServerTime+100;Check(!restored.Allowed,"Server license expiry limits offline grace");
  mode="valid";await restored.Verify();Check(restored.Allowed,"Online verification restores session");mode="expired";await restored.Verify();Check(!restored.Allowed&&!restored.Offline,"HTTP200 valid:false cannot use offline fallback");mode="valid";await restored.Verify();mode="revoked";await restored.Verify();Check(!restored.HasSession&&!restored.Allowed,"Revocation clears tokens");
  mode="valid";await session.Login("sample@example.test","test-pass");mode="network";bool failed=false;try{await session.Logout();}catch(WebException){failed=true;}Check(failed&&session.HasSession,"Failed logout retains session until device can be released");mode="valid";await session.Logout();Check(logouts==1&&!session.HasSession&&!File.Exists(file),"Successful logout releases device and removes tokens");
  CyanSession.Current=session;CyanSession.Enforced=true;Reject(CyanSession.Require,"New mounts require license");CyanSession.Enforced=false;CyanSession.Require();Check(true,"Fixture backend tests stay independent of production gating");
  string url="https://github.com/"+Core.UpdateRepository+"/releases/download/v9.1.0/CyanNasSetup.exe";var release=CyanRelease.Parse(Data("{\"ok\":true,\"update_available\":true,\"latest_version\":\"9.1.0\",\"download_url\":\""+url+"\",\"is_mandatory\":true}"));Check(release!=null&&release.Mandatory,"Parse version response supplied by CYANPRO");
  Check(CyanRelease.Parse(Data("{\"ok\":true,\"update_available\":false}"))==null,"No fabricated release when unavailable");Reject(()=>CyanRelease.ValidateUrl(url.Replace("github.com","evil.example"),"9.1.0"),"Reject foreign executable host");Reject(()=>CyanRelease.ValidateUrl(url,"9.2.0"),"Reject mismatched release version");Reject(()=>CyanRelease.ValidateUrl(url+"?redirect=bad","9.1.0"),"Reject unexpected query");Reject(()=>CyanRelease.ValidateUrl(url.Replace("CyanNasSetup.exe","CyanNas.exe"),"9.1.0"),"Require full installer");
  string hash=new string('a',64);Check(CyanRelease.HashFrom(hash+"  CyanNasSetup.exe\n")==hash,"Checksum parser accepts expected filename");Reject(()=>CyanRelease.HashFrom(hash+"  Wrong.exe"),"Reject wrong checksum filename");Reject(()=>CyanRelease.HashFrom("bad"),"Reject malformed checksum");
  File.WriteAllText(file,"tampered ciphertext");Check(!new CyanSession(home,api,"fixture-device").Allowed,"Corrupt session fails closed");
 }
 static int Main(string[] args){try{Run(args[0]).GetAwaiter().GetResult();if(args.Length>1){CyanRelease.ValidateFile(args[1],Core.Version);Check(true,"Actual setup product/version validation");Reject(()=>CyanRelease.ValidateFile(args[1],"9.9.9"),"Wrong setup file version rejected");}Console.WriteLine(count+" CYANPRO login, refresh, DPAPI, expiry, offline, revocation, logout and update trust checks passed.");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{CyanSession.Enforced=false;CyanSession.Current=null;}}
}
