using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace GoogleNas {
    public class Settings {
        public string Remote = "gdrive";
        public string Letter = "G";
        public string Folder = "";
        public int CacheGB = 20;
        public bool AutoMount = true;
        public bool AutoUpdate = false;
        public string Repository = Core.UpdateRepository;
    }
    public static class Core {
        // Desktop UI and multiresolution icon release; preserve the existing data and update identifiers during upgrades.
        public const string Version = "1.4.1";
        public const string UpdateRepository = "cyan881990/google-nas-updates";
        public static string EffectiveRepository(string legacyRepository) { return UpdateRepository.Length>0?UpdateRepository:(legacyRepository??""); }
        public static JavaScriptSerializer Json { get { return new JavaScriptSerializer(); } }
        public static void Validate(Settings s) {
            if (!Regex.IsMatch(s.Remote ?? "", @"^[A-Za-z0-9_-]{1,64}$")) throw new Exception("Tên kết nối chỉ gồm chữ, số, dấu - và _.");
            if (!Regex.IsMatch(s.Letter ?? "", @"^[D-Z]$")) throw new Exception("Chọn ký tự ổ đĩa từ D đến Z.");
            if (s.CacheGB < 1 || s.CacheGB > 2048) throw new Exception("Cache phải từ 1 đến 2048 GB.");
            if ((s.Folder ?? "").IndexOfAny(new char[] {'\r','\n','\0'}) >= 0 || (s.Folder ?? "").Contains(":")) throw new Exception("Đường dẫn thư mục Drive không hợp lệ.");
            if (s.Repository != "" && !Regex.IsMatch(s.Repository ?? "", @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new Exception("Repository cần có dạng owner/repo.");
        }
        // Windows CommandLineToArgvW quoting, including trailing backslashes.
        public static string Quote(string s) {
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in s) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { b.Append('\\', slashes * 2 + 1); b.Append(c); }
                else { b.Append('\\', slashes); b.Append(c); }
                slashes = 0;
            }
            b.Append('\\', slashes * 2); b.Append('"'); return b.ToString();
        }
        public static string Args(IEnumerable<string> args) { return string.Join(" ", args.Select(Quote)); }
        public static string Hash(string path) {
            using (var h = SHA256.Create()) using (var f = File.OpenRead(path)) return BitConverter.ToString(h.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
        }
        public static System.Version NormalizeVersion(System.Version v) { return new System.Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision)); }
        public static bool Newer(string tag) {
            System.Version v; return System.Version.TryParse((tag ?? "").TrimStart('v'), out v) && NormalizeVersion(v) > NormalizeVersion(new System.Version(Version));
        }
        public static long Number(Dictionary<string, object> d, string k) {
            if (!d.ContainsKey(k)) throw new Exception("Thiếu trường trạng thái " + k + ". Không thể xác nhận upload đã hoàn tất.");
            return Convert.ToInt64(d[k]);
        }
        public static bool SafeToStop(Dictionary<string, object> stats) {
            object raw;
            if (!stats.TryGetValue("diskCache", out raw)) throw new Exception("Không đọc được trạng thái cache.");
            var disk = raw as Dictionary<string, object>;
            if (disk == null) throw new Exception("Trạng thái cache không hợp lệ.");
            // Top-level inUse is the VFS reference count, not the number of open files.
            return Number(disk, "uploadsQueued") == 0 && Number(disk, "uploadsInProgress") == 0 && Number(disk, "erroredFiles") == 0;
        }
        public static void CheckCacheIdle(string cache) {
            foreach (string name in new[] { "vfsMeta", "vfs" }) {
                string root = Path.Combine(cache, name);
                if (!Directory.Exists(root)) continue;
                var pending = new Stack<string>(); pending.Push(root);
                while (pending.Count > 0) {
                    string directory = pending.Pop();
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new Exception("Không thể kiểm tra cache liên kết.");
                    foreach (string child in Directory.GetDirectories(directory)) pending.Push(child);
                    foreach (string file in Directory.GetFiles(directory)) {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new Exception("Không thể kiểm tra file cache liên kết.");
                        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None)) {
                            if (name != "vfsMeta") continue;
                            using (var reader = new StreamReader(stream)) {
                                var metadata = Json.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                                object dirty;
                                if (metadata == null || !metadata.TryGetValue("Dirty", out dirty) || !(dirty is bool)) throw new Exception("Không xác nhận được trạng thái đồng bộ của cache.");
                                if ((bool)dirty) throw new Exception("Cache còn thay đổi chưa upload. Chờ đồng bộ trước khi thoát.");
                            }
                        }
                    }
                }
            }
        }
        public static void TrustedAsset(string url, string repository) {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u) || u.Scheme != "https" || u.Host != "github.com" || !u.AbsolutePath.StartsWith("/" + repository + "/releases/download/", StringComparison.Ordinal)) throw new Exception("URL bản cập nhật không thuộc repository đã cấu hình.");
        }
    }
}
