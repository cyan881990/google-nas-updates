using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GoogleNas;
class NasTests {
    static int count;
    static void Check(bool ok,string name) { count++; if(!ok) throw new Exception(name); }
    static void Reject(Action test,string name) { bool rejected=false; try { test(); } catch { rejected=true; } Check(rejected,name); }
    static int Main(string[] args) {
        try {
            Check(NasService.IsTailIp("100.64.0.1") && NasService.IsTailIp("100.127.255.254"),"Tailnet boundaries");
            foreach(string bad in new[]{"100.63.255.255","100.128.0.1","192.168.1.1","127.0.0.1","example.com","100.64.1","100.064.0.1","100.64.1.999",null}) Check(!NasService.IsTailIp(bad),"Reject non-tailnet address");
            Check(NasService.RemotePath("100.64.0.1","Data_1")==@"\\100.64.0.1\Data_1","UNC generation");
            foreach(string bad in new[]{"C$","../x","a b","a;whoami","a\nb",""}) Reject(()=>NasService.ValidateShare(bad),"Share validation");
            Reject(()=>NasService.ValidateUser("Administrator;net user"),"User injection"); Reject(()=>NasService.ValidateLetter("C"),"System drive mapping");
            Check(NasService.LocalAccount("nasuser")==Environment.MachineName+@"\nasuser","Copy uses actual Windows computer name, not Tailscale display name");
            int passwordChecks=0;
            NasService.CheckLocalPassword("nasuser","Fixture!Local12",(user,password)=>{ passwordChecks++; Check(user=="nasuser" && password=="Fixture!Local12","Local check receives exact password without trimming"); return 0; });
            Reject(()=>NasService.CheckLocalPassword("nasuser","",(user,password)=>{ passwordChecks++; return 0; }),"Empty password never reaches Windows");
            Check(passwordChecks==1,"Password checks require explicit nonempty input");
            try { NasService.CheckLocalPassword("nasuser","Rejected!Local12",(user,password)=>1326); throw new Exception("Expected logon rejection"); }
            catch(System.ComponentModel.Win32Exception e) { Check(e.NativeErrorCode==1326 && e.Message.Contains("1326") && !e.Message.Contains("Rejected!Local12"),"Windows logon code preserved without password disclosure"); }
            Check(NasService.ConnectionError(5,"NAS").Message.Contains("NTFS") && !NasService.ConnectionError(5,"NAS").Message.Contains("không chấp nhận tài khoản"),"Access denied distinguished from incorrect password");
            foreach(int code in new[]{86,1219,1331,1909,1330,1907,1385,1327,67,53,64,1223}) Check(NasService.ConnectionError(code,"NAS").NativeErrorCode==code && NasService.ConnectionError(code,"NAS").Message.Contains(code.ToString()),"Precise Windows status retained: "+code);
            Check(NasService.ConnectionPath(1201,@"\\100.64.0.1\Data_1")==@"\\100.64.0.1\Data_1","Offline persistent mapping keeps UNC");
            Check(NasService.ConnectionPath(2250,@"\\100.64.0.1\Data_1")==null,"Missing mapping is not trusted");
            NasService.ValidateMappingTarget(@"\\100.64.0.1\Data_1",@"\\100.64.0.1\data_1",true,"N");
            Reject(()=>NasService.ValidateMappingTarget(@"\\100.64.0.2\Other",@"\\100.64.0.1\Data_1",true,"N"),"Reconnect cannot replace another share");
            Reject(()=>NasService.ValidateMappingTarget(null,@"\\100.64.0.1\Data_1",true,"N"),"Reconnect cannot replace local drive");
            Check(NasService.CanMaintain(@"\\100.64.0.1\DATA_1",@"\\100.64.0.1\Data_1"),"Maintain owned mapping");
            Check(!NasService.CanMaintain(null,@"\\100.64.0.1\Data_1"),"Do not recreate removed mapping");
            Check(!NasService.CanMaintain(@"\\100.64.0.2\Other",@"\\100.64.0.1\Data_1"),"Do not maintain reassigned drive");
            var monitor=new NasConnectionMonitor(); var completion=new TaskCompletionSource<string>(); int workers=0;
            Func<Task<string>> slow=()=> { System.Threading.Interlocked.Increment(ref workers); return completion.Task; };
            Check(monitor.Poll(slow).GetAwaiter().GetResult()==null,"Slow Windows call does not block polling");
            Check(monitor.Poll(slow).GetAwaiter().GetResult()==null && workers==1,"No duplicate workers while stalled");
            completion.SetResult("connected"); Check(monitor.Poll(slow).GetAwaiter().GetResult()=="connected","Completed reconnect result delivered");
            Check(monitor.Poll(slow).GetAwaiter().GetResult()==null && workers==1,"Retry interval avoids reconnect loop");
            var pending=NasService.ParseNetwork("{\"BackendState\":\"NeedsLogin\",\"TailscaleIPs\":null,\"Peer\":null}"); Check(!pending.Ready && pending.Address=="","Pending login null values");
            var network=NasService.ParseNetwork("{\"BackendState\":\"Running\",\"TailscaleIPs\":[\"fd7a::1\",\"100.100.0.1\"],\"Peer\":{\"node1\":{\"HostName\":\"nas-host\",\"TailscaleIPs\":[\"100.100.0.2\"]},\"node2\":{\"TailscaleIPs\":null}}}");
            Check(network.Ready && network.Address=="100.100.0.1" && network.Peers.Count==1,"Status and peer parsing");
            Check(!NasService.ParseNetwork("{\"BackendState\":\"Stopped\",\"TailscaleIPs\":[\"100.100.0.1\"]}").Ready,"Stopped cannot share");
            Directory.CreateDirectory(args[0]); var service=new NasService(args[0]); var profile=new NasProfile { Host="100.64.0.1",Share="Data_1",CreateUser=false }; service.Save(profile); var saved=service.Load(); Check(saved.Host==profile.Host && saved.Share==profile.Share && !saved.CreateUser,"Profile roundtrip");
            Check(!File.ReadAllText(Path.Combine(args[0],"nas.json")).Contains("Password"),"Profile has no passwords");
            service.SavePassword("nasuser","Fixture!PasswordSecret12");
            service.SavePassword("otheruser","Other!SecretPassword12");
            Check(new NasService(args[0]).LoadPassword("NASUSER")=="Fixture!PasswordSecret12","Password survives restart and matches account ignoring case");
            Check(service.LoadPassword("missinguser")=="","No password from another NAS account");
            service.SavePassword("nasuser","Changed!SecretPassword12");
            Check(service.LoadPassword("nasuser")=="Changed!SecretPassword12" && service.LoadPassword("otheruser")=="Other!SecretPassword12","Replacing one password preserves other accounts");
            var remoteProfile=new NasProfile { Host="100.64.0.1",RemoteShare="Data_1",Letter="N" }; int windowsSaved=0;
            var warning=service.MapAndSave(IntPtr.Zero,remoteProfile,@"NASPC\nasuser","Remote!Password12",(user,password)=> { Check(user==@"NASPC\nasuser" && password=="Remote!Password12","First mapping receives entered credentials"); },(host,user,password)=> { Check(host=="100.64.0.1" && password=="Remote!Password12","Windows credentials use selected host only"); windowsSaved++; });
            Check(warning=="" && windowsSaved==1 && remoteProfile.RemoteUser==@"NASPC\nasuser","Successful mapping automatically remembers login");
            var restarted=new NasService(args[0]);
            restarted.MapAndSave(IntPtr.Zero,remoteProfile,"","",(user,password)=> { Check(user==@"NASPC\nasuser" && password=="Remote!Password12","Restart reconnect reuses saved credentials without prompt"); },(host,user,password)=>{});
            Check(restarted.LoadRemoteLogin("100.64.0.2","Data_1")==null && restarted.LoadRemoteLogin("100.64.0.1","Data_2")==null,"Remote passwords cannot leak across hosts or shares");
            Reject(()=>restarted.MapAndSave(IntPtr.Zero,remoteProfile,@"NASPC\nasuser","Rejected!Password12",(user,password)=> { throw new Exception("Login rejected"); },(host,user,password)=> { windowsSaved++; }),"Failed authentication is not remembered");
            Check(restarted.LoadRemoteLogin("100.64.0.1","Data_1").Password=="Remote!Password12" && windowsSaved==1,"Rejected password cannot replace valid saved password");
            Reject(()=>NasService.ResolveLogin(restarted.LoadRemoteLogin("100.64.0.1","Data_1"),@"NASPC\otheruser",""),"Changing account cannot reuse another password");
            Reject(()=>NasService.ValidateRemoteUser("user\nname"),"Remote account rejects control characters");
            Check(NasService.ResolveLogin(null,"","")==null,"Legacy Windows credential entry can be used without tool password");
            int prompts=0; Func<NasLogin> prompt=()=>{ prompts++; return new NasLogin { User=@"NASPC\nasuser",Password="Windows!Prompt12" }; };
            var chosen=NasService.ChooseLogin(null,"","",false,prompt);
            Check(prompts==1 && chosen.Password=="Windows!Prompt12","First connection collects credentials through Windows UI");
            chosen=NasService.ChooseLogin(chosen,"","",false,prompt);
            Check(prompts==1 && chosen.User==@"NASPC\nasuser","Remembered connection never prompts again");
            chosen=NasService.ChooseLogin(chosen,chosen.User,"",true,prompt);
            Check(prompts==2,"Explicit login again always opens Windows UI");
            chosen=NasService.ChooseLogin(null,"nasuser","Manual!Password12",false,prompt);
            Check(prompts==2 && chosen.Password=="Manual!Password12","Inline credentials remain available without a redundant prompt");
            Check(NasService.ChooseLogin(null,"","",true,()=>null)==null,"Cancel Windows login leaves mapping and storage unchanged");
            NasService.ChooseLogin(new NasLogin { User="old",Password="Old!Password12" },"new","",false,prompt);
            Check(prompts==3,"Changing user without a password prompts instead of silently reusing old credentials");
            warning=restarted.MapAndSave(IntPtr.Zero,remoteProfile,"","",(user,password)=>{},(host,user,password)=> { throw new Exception("Credential Manager policy denies storage"); });
            Check(warning.Contains("Windows") && restarted.LoadRemoteLogin("100.64.0.1","Data_1").Password=="Remote!Password12","Windows storage failure preserves encrypted tool fallback");
            var protectedPassword=File.ReadAllBytes(Path.Combine(args[0],"nas-passwords.dat"));
            Check(!Encoding.UTF8.GetString(protectedPassword).Contains("SecretPassword12"),"Stored passwords are not plaintext");
            protectedPassword[protectedPassword.Length/2]^=1; File.WriteAllBytes(Path.Combine(args[0],"nas-passwords.dat"),protectedPassword);
            Reject(()=>service.LoadPassword("nasuser"),"Tampered password file fails closed");
            service.RememberConnection(new NasProfile { Host="100.64.0.1",RemoteShare="Data_1",Letter="N" });
            service.RememberConnection(new NasProfile { Host="100.64.0.2",RemoteShare="Data_2",Letter="P" });
            var savedConnections=File.ReadAllText(Path.Combine(args[0],"nas-connections.json"));
            Check(savedConnections.Contains("100.64.0.1") && savedConnections.Contains("100.64.0.2") && !savedConnections.Contains("Password"),"Multiple remembered mappings without credentials");
            byte[] clear=Encoding.UTF8.GetBytes("fixture password secret"),cipher=ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser); Check(Encoding.UTF8.GetString(cipher).IndexOf("fixture password secret",StringComparison.Ordinal)<0,"DPAPI request encryption"); Check(Encoding.UTF8.GetString(ProtectedData.Unprotect(cipher,null,DataProtectionScope.CurrentUser))=="fixture password secret","DPAPI roundtrip");
            Console.WriteLine(count+" NAS address, share, state, profile and DPAPI tests passed."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
