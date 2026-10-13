using System;
using System.IO;
using System.Threading.Tasks;
namespace GoogleNas {
    // Windows filesystem providers can block indefinitely. Keep one outstanding probe off the UI thread.
    public class DriveProbe {
        private readonly Func<string,bool> exists;
        private Task<bool> pending;
        private string pendingPath;
        public DriveProbe() : this(Directory.Exists) { }
        public DriveProbe(Func<string,bool> check) { exists=check; }
        public async Task<bool> Ready(string path,int timeout=1000) {
            if(pending==null) { pendingPath=path; pending=Task.Run(()=>exists(path)); }
            if(await Task.WhenAny(pending,Task.Delay(timeout))!=pending) return false;
            bool same=string.Equals(path,pendingPath,StringComparison.OrdinalIgnoreCase);
            try { bool result=await pending; return same && result; } finally { pending=null; pendingPath=null; }
        }
    }
}
