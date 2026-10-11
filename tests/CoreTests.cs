using System;
using System.Collections.Generic;
using GoogleNas;
class Tests {
    static int count;
    static void Check(bool ok, string name) { count++; if (!ok) throw new Exception(name); }
    static void Reject(Action action, string name) { bool threw = false; try { action(); } catch { threw = true; } Check(threw, name); }
    static int Main() {
        try {
            Core.Validate(new Settings());
            Reject(() => Core.Validate(new Settings { Remote = "a:bad" }), "Remote injection");
            Reject(() => Core.Validate(new Settings { Letter = "C" }), "System drive");
            Reject(() => Core.Validate(new Settings { CacheGB = 0 }), "Invalid cache");
            Reject(() => Core.Validate(new Settings { Repository = "https://github.com/a/b" }), "Repository URL");
            Reject(() => Core.Validate(new Settings { Folder = "a\nb" }), "Folder newline");
            Check(Core.Quote("a b") == "\"a b\"", "Spaces");
            Check(Core.Quote("a\"b") == "\"a\\\"b\"", "Quote");
            Check(Core.Quote("C:\\dir\\") == "\"C:\\dir\\\\\"", "Trailing slash");
            Check(!Core.Newer("v" + Core.Version) && !Core.Newer(Core.Version + ".0"), "Equal versions");
            var currentVersion=new Version(Core.Version);
            Check(Core.Newer("v"+currentVersion.Major+"."+currentVersion.Minor+"."+(currentVersion.Build+1)) && Core.Newer("v"+(currentVersion.Major+1)+".0.0"), "Newer release");
            Check(!Core.Newer("v1.1.0-beta") && !Core.Newer("garbage"), "Reject nonstable");
            Core.TrustedAsset("https://github.com/me/repo/releases/download/v1.0.1/GoogleNas.exe", "me/repo");
            Reject(() => Core.TrustedAsset("https://github.com/other/repo/releases/download/v1/a", "me/repo"), "Other repo");
            Reject(() => Core.TrustedAsset("http://github.com/me/repo/releases/download/v1/a", "me/repo"), "Non TLS");
            Reject(() => Core.TrustedAsset("https://github.com.evil.test/me/repo/releases/download/v1/a", "me/repo"), "Spoof host");
            var disk = new Dictionary<string,object> { {"uploadsQueued",0}, {"uploadsInProgress",0}, {"erroredFiles",0} };
            var stats = new Dictionary<string,object> { {"diskCache", disk}, {"inUse",0} };
            Check(Core.SafeToStop(stats), "Idle safe");
            disk["uploadsQueued"] = 1; Check(!Core.SafeToStop(stats), "Queued writes block");
            disk["uploadsQueued"] = 0; disk["uploadsInProgress"] = 1; Check(!Core.SafeToStop(stats), "Active uploads block");
            disk["uploadsInProgress"] = 0; disk["erroredFiles"] = 1; Check(!Core.SafeToStop(stats), "Errors block");
            disk["erroredFiles"] = 0; stats["inUse"] = 1; Check(Core.SafeToStop(stats), "Mounted idle VFS reference does not block exit");
            var cache = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GoogleNas-exit-" + Guid.NewGuid().ToString("N"));
            try {
                var meta = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(cache,"vfsMeta")).FullName;
                var data = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(cache,"vfs")).FullName;
                var metaFile = System.IO.Path.Combine(meta,"sample"); var dataFile = System.IO.Path.Combine(data,"sample");
                System.IO.File.WriteAllText(metaFile,"{\"Dirty\":false}"); System.IO.File.WriteAllText(dataFile,"cached content");
                Core.CheckCacheIdle(cache); Check(true,"Clean unlocked cache allows exit");
                System.IO.File.WriteAllText(metaFile,"{\"Dirty\":true}"); Reject(()=>Core.CheckCacheIdle(cache),"Dirty cache blocks exit before writeback queue");
                System.IO.File.WriteAllText(metaFile,"{}"); Reject(()=>Core.CheckCacheIdle(cache),"Unknown metadata blocks exit");
                System.IO.File.WriteAllText(metaFile,"{\"Dirty\":false}");
                using(var held = new System.IO.FileStream(dataFile,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.ReadWrite)) Reject(()=>Core.CheckCacheIdle(cache),"Open cache handle blocks exit");
            } finally { System.IO.Directory.Delete(cache,true); }
            disk.Remove("uploadsQueued"); Reject(() => Core.SafeToStop(stats), "Unknown state blocks");
            Console.WriteLine(count + " core tests passed."); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
