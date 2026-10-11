using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using GoogleNasInstaller;
class InstallerCoreTests {
    [STAThread] static int Main(string[] args) {
        if(args.Length==1 && args[0]=="--sleep") { Thread.Sleep(5000); return 0; }
        try {
            string root=Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
            string menu=Path.Combine(root,"Start Menu","CYAN NAS"),app=Path.Combine(root,"Installed App"); Directory.CreateDirectory(app);
            string warning=SetupEngine.CreateShortcuts(menu,app);
            if(warning!=null) throw new Exception("Shortcut creation failed: "+warning);
            dynamic shortcutShell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic uninstallLink=shortcutShell.CreateShortcut(Path.Combine(menu,"Uninstall CYAN NAS.lnk"));
            if((string)uninstallLink.TargetPath!=Path.Combine(app,"Uninstall.exe") || (string)uninstallLink.Arguments!="--uninstall" || (string)uninstallLink.WorkingDirectory!=app) throw new Exception("Uninstall shortcut target/arguments mismatch.");
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(uninstallLink); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcutShell);
            string blockedMenu=Path.Combine(root,"blocked-menu"); File.WriteAllText(blockedMenu,"file instead of directory");
            if(string.IsNullOrEmpty(SetupEngine.CreateShortcuts(blockedMenu,app))) throw new Exception("Optional shortcut failure did not return a warning.");
            string cmd=SetupEngine.InstallerArgs(new[]{"/i",@"C:\My App\winfsp.msi","/qn","INSTALLLEVEL=100","/l*v",@"C:\My Log\install.log"});
            if(cmd!="/i \"C:\\My App\\winfsp.msi\" /qn INSTALLLEVEL=100 /l*v \"C:\\My Log\\install.log\"") throw new Exception("MSI command line quoting regression.");
            using(var sleeper=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--sleep") { UseShellExecute=false,CreateNoWindow=true })) {
                try {
                    int beats=0;
                    if(SetupEngine.WaitForInstaller(sleeper,150,seconds=>beats++)) throw new Exception("Timeout did not return.");
                    if(beats==0 || sleeper.HasExited) throw new Exception("Timeout should report progress and preserve installer process.");
                } finally { if(!sleeper.HasExited) { sleeper.Kill(); sleeper.WaitForExit(); } }
            }
            string missing=Path.Combine(root,"missing-"+Guid.NewGuid().ToString("N")+".msi");
            using(var msi=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe"),SetupEngine.InstallerArgs(new[]{"/i",missing,"/qn"})) { UseShellExecute=false,CreateNoWindow=true })) {
                if(!SetupEngine.WaitForInstaller(msi,5000,seconds=>{})) { msi.Kill(); msi.WaitForExit(); throw new Exception("MSI parser regression: missing package command hung."); }
                if(msi.ExitCode!=1619) throw new Exception("Expected missing MSI error 1619, got "+msi.ExitCode);
            }
            if(!SetupEngine.SafePath(root,"app/GoogleNas.exe").StartsWith(root)) throw new Exception("Valid path rejected.");
            bool rejected=false; try { SetupEngine.SafePath(root,"../outside.exe"); } catch { rejected=true; } if(!rejected) throw new Exception("Path traversal accepted.");
            string stage=Path.Combine(root,"stage"),target=Path.Combine(root,"target"); Directory.CreateDirectory(Path.Combine(stage,"app")); Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(stage,"app","old.txt"),"new version"); File.WriteAllText(Path.Combine(stage,"app","zzlocked.txt"),"new file"); File.WriteAllText(Path.Combine(target,"old.txt"),"old version");
            SetupEngine.InstallFiles(stage,target);
            if(File.ReadAllText(Path.Combine(target,"old.txt"))!="new version" || !File.Exists(Path.Combine(target,"zzlocked.txt"))) throw new Exception("Copy/replace failed.");
            File.WriteAllText(Path.Combine(target,"old.txt"),"rollback baseline");
            File.WriteAllText(Path.Combine(stage,"app","zzlocked.txt"),"changed locked file");
            using(var locked=File.Open(Path.Combine(target,"zzlocked.txt"),FileMode.Open,FileAccess.Read,FileShare.None)) {
                rejected=false; try { SetupEngine.InstallFiles(stage,target); } catch { rejected=true; } if(!rejected) throw new Exception("Expected lock failure.");
            }
            if(File.ReadAllText(Path.Combine(target,"old.txt"))!="rollback baseline") throw new Exception("Rollback failed.");
            File.SetAttributes(Path.Combine(target,"old.txt"),FileAttributes.ReadOnly);
            SetupEngine.InstallFiles(stage,target);
            if(File.ReadAllText(Path.Combine(target,"old.txt"))!="new version") throw new Exception("Read-only replacement failed.");
            File.WriteAllText(Path.Combine(target,"old.txt.install-backup"),"stale backup");
            File.WriteAllText(Path.Combine(stage,"app","old.txt"),"newer version");
            SetupEngine.InstallFiles(stage,target);
            if(File.ReadAllText(Path.Combine(target,"old.txt"))!="newer version" || File.ReadAllText(Path.Combine(target,"old.txt.install-backup"))!="stale backup") throw new Exception("Stale backup replacement failed.");
            using(var locked=File.Open(Path.Combine(target,"zzlocked.txt"),FileMode.Open,FileAccess.Read,FileShare.Read)) SetupEngine.InstallFiles(stage,target);
            Console.WriteLine("Windows COM shortcuts and optional failure, installer command parser, timeout/progress, path validation, replacement and rollback passed."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
