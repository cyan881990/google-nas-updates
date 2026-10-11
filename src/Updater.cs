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
            repository=Core.EffectiveRepository(repository);
            var s = new Settings { Repository = repository }; Core.Validate(s);
            if (repository == "") throw new Exception("Bản cài này chưa có nguồn cập nhật. Liên hệ người phát hành để nhận bộ cài đã cấu hình.");
            using (var c = Services.Client()) {
                string response;
                try { response=await c.DownloadStringTaskAsync("https://api.github.com/repos/" + repository + "/releases/latest"); }
                catch(WebException e) {
                    var http=e.Response as HttpWebResponse;
                    if(http!=null && http.StatusCode==HttpStatusCode.NotFound) throw new Exception("Nguồn cập nhật chưa có bản phát hành công khai hoặc không truy cập được. Vui lòng thử lại sau khi bản mới được phát hành.",e);
                    throw;
                }
                var data = Core.Json.Deserialize<Dictionary<string, object>>(response);
                var tag = Convert.ToString(data["tag_name"]);
                if (!Core.Newer(tag)) return null;
                if (Convert.ToBoolean(data["prerelease"]) || Convert.ToBoolean(data["draft"])) return null;
                var assets = ((IEnumerable)data["assets"]).Cast<Dictionary<string, object>>().ToList();
                var exe = assets.SingleOrDefault(a => Convert.ToString(a["name"]) == "GoogleNas.exe");
                var sha = assets.SingleOrDefault(a => Convert.ToString(a["name"]) == "GoogleNas.exe.sha256");
                if (exe == null || sha == null) throw new Exception("Release thiếu GoogleNas.exe hoặc GoogleNas.exe.sha256.");
                var release = new Release { Tag = tag, Binary = Convert.ToString(exe["browser_download_url"]), Checksum = Convert.ToString(sha["browser_download_url"]) };
                Core.TrustedAsset(release.Binary, repository); Core.TrustedAsset(release.Checksum, repository); return release;
            }
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
