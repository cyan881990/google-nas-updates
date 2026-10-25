using System;
using System.Reflection;
using System.Net;
using System.Threading;
using System.Windows.Forms;

#if CYAN_UNIFIED
[assembly: AssemblyTitle("CYAN NAS")]
[assembly: AssemblyProduct("CYAN NAS")]
#elif CYAN_SERVER
[assembly: AssemblyTitle("CYAN NAS SERVER")]
[assembly: AssemblyProduct("CYAN NAS SERVER")]
#elif CYAN_CLIENT
[assembly: AssemblyTitle("CYAN NAS CLIENT")]
[assembly: AssemblyProduct("CYAN NAS CLIENT")]
#else
[assembly: AssemblyTitle("CYAN NAS")]
[assembly: AssemblyProduct("Google NAS")]
#endif

namespace GoogleNas {
    static class Program {
        internal static readonly int ShowWindowMessage=RegisterWindowMessage("GoogleNas.ShowWindow."+Environment.UserName);
        internal static readonly int TaskbarCreatedMessage=RegisterWindowMessage("TaskbarCreated");
        [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] private static extern int RegisterWindowMessage(string name);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int processId);
        [STAThread]
        static int Main(string[] args) {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (SuiteIdentity.Enabled) return SuiteProgram.Run(args);
            if(Array.IndexOf(args,"--health-check")<0&&Array.IndexOf(args,"--render-preview")<0&&System.IO.File.Exists(System.IO.Path.Combine(Application.StartupPath,"CyanNas.exe"))){System.Diagnostics.Process.Start(System.IO.Path.Combine(Application.StartupPath,"CyanNas.exe"),Core.Args(args));return 0;}
            if(Array.IndexOf(args,"--health-check")<0&&Array.IndexOf(args,"--render-preview")<0&&System.IO.File.Exists(System.IO.Path.Combine(Application.StartupPath,"CyanNasClient.exe"))&&System.IO.File.Exists(System.IO.Path.Combine(Application.StartupPath,"CyanNasServer.exe"))) {
                System.Diagnostics.Process.Start(System.IO.Path.Combine(Application.StartupPath,"CyanNasClient.exe"),Core.Args(args));return 0;
            }
            if (Array.IndexOf(args, "--health-check") >= 0) {
                try {
                    Core.Validate(new Settings());
                    string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoogleNas", "settings.json");
                    if (System.IO.File.Exists(file)) Core.Validate(Core.Json.Deserialize<Settings>(System.IO.File.ReadAllText(file)));
                    using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("apply-update.ps1")) if (resource == null || resource.Length == 0) return 1;
                    using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("nas-admin.ps1")) if (resource == null || resource.Length == 0) return 1;
                    return 0;
                } catch { return 1; }
            }
            if ((args.Length == 2 || args.Length == 3) && args[0] == "--render-preview") {
                string preview = System.IO.Path.GetFullPath(args[1]);
                try {
                    using (var form = new MainForm(false, new Services(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(preview), "preview-data")), true)) {
                        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-10000, -10000);
                        form.Show(); Application.DoEvents(); form.PerformLayout();
                        if (args.Length == 3) { form.PreviewPage(int.Parse(args[2])); Application.DoEvents(); }
                        using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(preview); }
                    }
                    return 0;
                } catch (Exception ex) { System.IO.File.WriteAllText(preview + ".error.txt", ex.ToString()); return 1; }
            }
            bool created;
            using (var mutex = new Mutex(true, "Local\\GoogleNas-" + Environment.UserName, out created)) {
                if (!created) {
                    if(Array.IndexOf(args,"--tray")<0) {
                        foreach(var process in System.Diagnostics.Process.GetProcessesByName("GoogleNas")) using(process) { if(process.SessionId==System.Diagnostics.Process.GetCurrentProcess().SessionId) AllowSetForegroundWindow(process.Id); }
                        PostMessage(new IntPtr(0xffff),ShowWindowMessage,IntPtr.Zero,IntPtr.Zero);
                    }
                    return 0;
                }
                try { Application.Run(new MainForm(Array.IndexOf(args, "--tray") >= 0)); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Không thể khởi chạy CYAN NAS", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
                finally { mutex.ReleaseMutex(); }
            }
            return 0;
        }
    }
}
