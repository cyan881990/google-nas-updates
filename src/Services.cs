using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GoogleNas {
    public class Services {
        public readonly string Home;
        public string Config { get { return Path.Combine(Home, "rclone.conf"); } }
        public string Rclone { get {
            string bundled = Path.Combine(System.Windows.Forms.Application.StartupPath, "runtime", "rclone.exe");
            return File.Exists(bundled) ? bundled : Path.Combine(Home, "tools", "rclone.exe");
        } }
        public string Cache { get { return Path.Combine(Home, "cache"); } }
        public string Log { get { return Path.Combine(Home, "rclone.log"); } }
        public Process Mount;
        private int port;
        private string password;
        public Services() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoogleNas")) { }
        public Services(string home) {
            Home = home;
            Directory.CreateDirectory(Home);
            // OAuth config and cache remain local, restricted to this Windows account and SYSTEM.
            var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.SetAccessControl(Home, acl);
        }
        public Settings Load() {
            string file = Path.Combine(Home, "settings.json");
            if (!File.Exists(file)) return new Settings();
            var s = Core.Json.Deserialize<Settings>(File.ReadAllText(file)); s.Repository=Core.EffectiveRepository(s.Repository); Core.Validate(s); return s;
        }
        public void Save(Settings s) {
            Core.Validate(s); string file = Path.Combine(Home, "settings.json"); string temp = file + ".tmp";
            File.WriteAllText(temp, Core.Json.Serialize(s), Encoding.UTF8);
            if (File.Exists(file)) File.Replace(temp, file, file + ".bak"); else File.Move(temp, file);
        }
        public bool Running { get { return Mount != null && !Mount.HasExited; } }
        public bool WinFspInstalled {
            get {
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var key = root.OpenSubKey(@"SOFTWARE\WinFsp")) return key != null;
            }
        }
        public static WebClient Client() {
            var c = new WebClient(); c.Headers[HttpRequestHeader.UserAgent] = "GoogleNas/" + Core.Version; return c;
        }
        public async Task InstallRclone() {
            if (Running) throw new Exception("Ngắt ổ đĩa trước khi thay rclone.");
            Directory.CreateDirectory(Path.GetDirectoryName(Rclone));
            string work = Path.Combine(Home, "download-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
            try {
                using (var c = Client()) {
                    string version = (await c.DownloadStringTaskAsync("https://downloads.rclone.org/version.txt")).Trim();
                    if (!Regex.IsMatch(version, @"^rclone v\d+\.\d+\.\d+$")) throw new Exception("Không đọc được phiên bản rclone.");
                    string v = version.Substring(7), name = "rclone-" + v + "-windows-amd64.zip";
                    string checks = await c.DownloadStringTaskAsync("https://downloads.rclone.org/" + v + "/SHA256SUMS");
                    var match = Regex.Match(checks, @"(?im)^([a-f0-9]{64})\s+\*?" + Regex.Escape(name) + @"\s*$");
                    if (!match.Success) throw new Exception("Không tìm thấy SHA256 chính thức của rclone.");
                    string zip = Path.Combine(work, name); await c.DownloadFileTaskAsync("https://downloads.rclone.org/" + v + "/" + name, zip);
                    if (!string.Equals(Core.Hash(zip), match.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) throw new Exception("Checksum rclone không khớp.");
                    using (var archive = ZipFile.OpenRead(zip)) {
                        var entry = archive.Entries.Single(e => e.FullName == "rclone-" + v + "-windows-amd64/rclone.exe");
                        string stage = Rclone + ".new"; entry.ExtractToFile(stage, true);
                        if (File.Exists(Rclone)) File.Replace(stage, Rclone, Rclone + ".bak"); else File.Move(stage, Rclone);
                    }
                }
            } finally { Directory.Delete(work, true); }
        }
        public async Task Connect(Settings s) {
            if (Running) throw new Exception("Ngắt ổ đĩa trước khi đổi tài khoản.");
            RequireRclone();
            // Keep OAuth output (which may contain tokens) away from the UI and log.
            var p = Process.Start(new ProcessStartInfo(Rclone, Core.Args(new[] {"config", "create", s.Remote, "drive", "scope", "drive", "config_is_local", "true", "--config", Config, "--no-output"})) { UseShellExecute = false });
            await Task.Run(() => p.WaitForExit());
            int code = p.ExitCode; p.Dispose(); if (code != 0) throw new Exception("Kết nối Google chưa hoàn tất. Thử lại và cấp quyền trong trình duyệt.");
        }
        public void RequireRclone() { if (!File.Exists(Rclone)) throw new Exception("Thiếu thành phần ổ đĩa. Chạy lại GoogleNasSetup.exe để sửa bản cài."); }
        public void Start(Settings s) {
            Core.Validate(s); RequireRclone();
            if (!WinFspInstalled) throw new Exception("Driver ổ đĩa chưa sẵn sàng. Chạy GoogleNasSetup.exe để cài hoặc khởi động lại Windows nếu vừa cài xong.");
            if (!File.Exists(Config)) throw new Exception("Kết nối tài khoản Google trước.");
            if (Running) return;
            if (DriveInfo.GetDrives().Any(d=>d.Name.StartsWith(s.Letter+":",StringComparison.OrdinalIgnoreCase))) throw new Exception("Ký tự ổ đĩa đã được sử dụng.");
            Directory.CreateDirectory(Cache);
            if (File.Exists(Log) && new FileInfo(Log).Length > 10 * 1024 * 1024) { File.Copy(Log, Log + ".previous", true); File.Delete(Log); }
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            password = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var args = new[] {"mount", s.Remote + ":" + s.Folder.Trim('/'), s.Letter + ":", "--config", Config,
                "--vfs-cache-mode", "full", "--cache-dir", Cache, "--vfs-cache-max-size", s.CacheGB + "G",
                "--vfs-cache-max-age", "72h", "--vfs-write-back", "5s", "--dir-cache-time", "5m", "--poll-interval", "30s",
                "--network-mode", "--volname", "CYAN NAS", "--log-file", Log, "--log-level", "INFO", "--rc", "--rc-addr", "127.0.0.1:" + port};
            var info = new ProcessStartInfo(Rclone, Core.Args(args)) { UseShellExecute = false, CreateNoWindow = true };
            info.EnvironmentVariables["RCLONE_RC_USER"] = "googlenas"; info.EnvironmentVariables["RCLONE_RC_PASS"] = password;
            Mount = Process.Start(info);
        }
        public async Task<Dictionary<string, object>> Rc(string endpoint) {
            if (!Running) throw new Exception("Ổ đĩa chưa chạy.");
            var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/" + endpoint);
            request.Proxy = null; request.Method = "POST"; request.ContentType = "application/json"; request.Timeout = 5000; request.ReadWriteTimeout = 5000;
            request.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("googlenas:" + password));
            byte[] body = Encoding.UTF8.GetBytes("{}"); request.ContentLength = body.Length;
            var task = Task.Run(()=>Send(request, body));
            if (await Task.WhenAny(task, Task.Delay(6000)) != task) { request.Abort(); throw new Exception("Hết thời gian chờ rclone."); }
            return await task;
        }
        private async Task<Dictionary<string, object>> Send(HttpWebRequest r, byte[] body) {
            using (var stream = await r.GetRequestStreamAsync()) await stream.WriteAsync(body, 0, body.Length);
            using (var response = await r.GetResponseAsync()) using (var reader = new StreamReader(response.GetResponseStream())) return Core.Json.Deserialize<Dictionary<string, object>>(await reader.ReadToEndAsync());
        }
        public async Task Stop() {
            if (!Running) return;
            // Two checks separated by write-back interval reduce the window for recently closed writes.
            if (!Core.SafeToStop(await Rc("vfs/stats"))) throw new Exception("Đóng các file đang mở và chờ upload hoàn tất trước khi ngắt/cập nhật.");
            await Task.Run(()=>Core.CheckCacheIdle(Cache));
            await Task.Delay(6000);
            if (!Core.SafeToStop(await Rc("vfs/stats"))) throw new Exception("Drive còn file đang dùng hoặc chờ upload. Thử lại sau.");
            await Task.Run(()=>Core.CheckCacheIdle(Cache));
            try { await Rc("core/quit"); } catch (WebException) { /* quit may close HTTP before response */ }
            var p = Mount;
            if (!await Task.Run(() => p.WaitForExit(10000))) throw new Exception("rclone chưa thoát. Không cưỡng chế dừng để giữ cache.");
            p.Dispose(); Mount = null;
        }
        public bool StartupEnabled() {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key != null && key.GetValue("GoogleNas") != null;
            } catch (System.Security.SecurityException) { return false; }
        }
        public void SetStartup(bool enabled) {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if (enabled) key.SetValue("GoogleNas", Core.Quote(System.Windows.Forms.Application.ExecutablePath) + " --tray"); else key.DeleteValue("GoogleNas", false);
            }
        }
    }
}
