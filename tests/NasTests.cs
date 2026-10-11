using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
            var pending=NasService.ParseNetwork("{\"BackendState\":\"NeedsLogin\",\"TailscaleIPs\":null,\"Peer\":null}"); Check(!pending.Ready && pending.Address=="","Pending login null values");
            var network=NasService.ParseNetwork("{\"BackendState\":\"Running\",\"TailscaleIPs\":[\"fd7a::1\",\"100.100.0.1\"],\"Peer\":{\"node1\":{\"HostName\":\"nas-host\",\"TailscaleIPs\":[\"100.100.0.2\"]},\"node2\":{\"TailscaleIPs\":null}}}");
            Check(network.Ready && network.Address=="100.100.0.1" && network.Peers.Count==1,"Status and peer parsing");
            Check(!NasService.ParseNetwork("{\"BackendState\":\"Stopped\",\"TailscaleIPs\":[\"100.100.0.1\"]}").Ready,"Stopped cannot share");
            Directory.CreateDirectory(args[0]); var service=new NasService(args[0]); var profile=new NasProfile { Host="100.64.0.1",Share="Data_1",CreateUser=false }; service.Save(profile); var saved=service.Load(); Check(saved.Host==profile.Host && saved.Share==profile.Share && !saved.CreateUser,"Profile roundtrip");
            Check(!File.ReadAllText(Path.Combine(args[0],"nas.json")).Contains("Password"),"Profile has no passwords");
            byte[] clear=Encoding.UTF8.GetBytes("fixture password secret"),cipher=ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser); Check(Encoding.UTF8.GetString(cipher).IndexOf("fixture password secret",StringComparison.Ordinal)<0,"DPAPI request encryption"); Check(Encoding.UTF8.GetString(ProtectedData.Unprotect(cipher,null,DataProtectionScope.CurrentUser))=="fixture password secret","DPAPI roundtrip");
            Console.WriteLine(count+" NAS address, share, state, profile and DPAPI tests passed."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
