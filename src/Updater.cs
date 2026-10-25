using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoogleNas {
    public class Release {
        public string Tag;
        public string Binary;
        public string Checksum;
    }
    public static class Updater {
        public static async Task<Release> Check(string repository) {
            var metadata=await CyanRelease.Check();if(metadata==null)return null;
            string binary=metadata.Url.Substring(0,metadata.Url.LastIndexOf('/')+1)+"GoogleNas.exe";
            var release=new Release{Tag=metadata.Version,Binary=binary,Checksum=binary+".sha256"};
            Core.TrustedAsset(release.Binary,Core.UpdateRepository);Core.TrustedAsset(release.Checksum,Core.UpdateRepository);return release;
        }
        public static async Task<string> Stage(Release release, string home) {
            string work = Path.Combine(home, "updates", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
            string stage = Path.Combine(work, "GoogleNas.exe");
            using (var c = Services.Client()) {
                string checks = await c.DownloadStringTaskAsync(release.Checksum);
                var match = Regex.Match(checks.Trim(), @"\A([a-fA-F0-9]{64})(?:\s+\*?GoogleNas\.exe)?\z");
                if (!match.Success) throw new Exception("File SHA256 của release không hợp lệ.");
                await c.DownloadFileTaskAsync(release.Binary, stage);
                if (!Core.Hash(stage).Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) { File.Delete(stage); throw new Exception("Bản tải về sai SHA256. Đã hủy cập nhật."); }
                var info = FileVersionInfo.GetVersionInfo(stage);
                if ((info.ProductName != "Google NAS" && info.ProductName != "CYAN NAS") || !Core.Newer(info.FileVersion) || Core.NormalizeVersion(new Version(info.FileVersion)) != Core.NormalizeVersion(new Version(release.Tag.TrimStart('v')))) throw new Exception("Phiên bản trong EXE không khớp release.");
            }
            return stage;
        }
        public static void Launch(string stage) {
            string script = Path.Combine(Path.GetDirectoryName(stage), "apply-update.ps1");
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("apply-update.ps1"))
            using (var file = File.Create(script)) resource.CopyTo(file);
            string target = System.Windows.Forms.Application.ExecutablePath;
            string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            Process.Start(new ProcessStartInfo(shell, Core.Args(new[] {"-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-ProcessId", Process.GetCurrentProcess().Id.ToString(), "-Stage", stage, "-Target", target, "-ExpectedHash", Core.Hash(stage)})) { UseShellExecute = false, CreateNoWindow = true });
        }
    }
}
