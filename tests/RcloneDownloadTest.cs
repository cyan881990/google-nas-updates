using System;
using System.Diagnostics;
using System.Net;
using GoogleNas;
class DownloadTest {
    static int Main(string[] args) {
        try {
            if (args.Length != 1) throw new Exception("Provide an isolated test data directory.");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var services = new Services(args[0]);
            services.InstallRclone().GetAwaiter().GetResult();
            var p = Process.Start(new ProcessStartInfo(services.Rclone, "version") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
            Console.WriteLine(p.StandardOutput.ReadToEnd()); p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception("Downloaded rclone failed to run.");
            Console.WriteLine("Official download + SHA256 + extraction + execution passed."); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
