using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
[assembly:AssemblyTitle("CYAN NAS compatibility launcher")]
[assembly:AssemblyProduct("CYAN NAS")]
class RoleLauncher {
 [STAThread]static int Main(string[] args){string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CyanNas.exe");if(Array.IndexOf(args,"--health-check")>=0)return File.Exists(exe)?0:1;if(!File.Exists(exe)){MessageBox.Show("Chạy bộ cài CYAN NAS để nâng cấp đầy đủ.","CYAN NAS");return 1;}
#if SERVER_LINK
 string role="--server";
#else
 string role="--client";
#endif
 Process.Start(exe,role+(Array.IndexOf(args,"--tray")>=0?" --tray":""));return 0;}
}
