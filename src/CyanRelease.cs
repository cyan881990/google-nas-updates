using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace GoogleNas {
 public class CyanRelease {
  public string Version,Url,Notes;public bool Mandatory,IsFree;
  internal static CyanRelease Parse(Dictionary<string,object> data){
   string version=CyanApi.Text(data,"latest_version");if(!CyanApi.Flag(data,"update_available")||!Core.Newer(version))return null;
   var release=new CyanRelease{Version=version,Url=CyanApi.Text(data,"download_url"),Notes=CyanApi.Text(data,"notes"),Mandatory=CyanApi.Flag(data,"is_mandatory"),IsFree=CyanApi.Flag(data,"is_free")};
   ValidateUrl(release.Url,version);return release;
  }
  internal static void ValidateUrl(string url,string version){
   Core.TrustedAsset(url,Core.UpdateRepository);var uri=new Uri(url);string expected="/"+Core.UpdateRepository+"/releases/download/v"+version.TrimStart('v')+"/CyanNasSetup.exe";
   if(!uri.AbsolutePath.Equals(expected,StringComparison.Ordinal)||uri.Query!=""||uri.Fragment!="")throw new Exception("Link bộ cài do CYANPRO trả về không khớp phiên bản CYAN NAS.");
  }
  public static async Task<CyanRelease> Check(){var release=Parse(await new CyanApi().Version());if(release!=null&&release.Mandatory)CyanSession.UpdateRequired=true;return release;}
  internal static string HashFrom(string text){var match=Regex.Match(text.Trim(),@"\A([a-fA-F0-9]{64})(?:\s+\*?CyanNasSetup\.exe)?\z");if(!match.Success)throw new Exception("File SHA256 của bộ cài không hợp lệ.");return match.Groups[1].Value;}
  public async Task<string> Download(string home){
   ValidateUrl(Url,Version);string folder=Path.Combine(home,"updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string file=Path.Combine(folder,"CyanNasSetup.exe");
   try{using(var c=Services.Client()){
    var hashTask=c.DownloadStringTaskAsync(Url+".sha256");if(await Task.WhenAny(hashTask,Task.Delay(15000))!=hashTask){c.CancelAsync();try{await hashTask;}catch{}throw new Exception("Quá thời gian tải checksum. Thử lại sau.");}string expected=HashFrom(await hashTask);
    var download=c.DownloadFileTaskAsync(Url,file);if(await Task.WhenAny(download,Task.Delay(15*60000))!=download){c.CancelAsync();try{await download;}catch{}throw new Exception("Tải bộ cài quá thời gian. Thử lại khi mạng ổn định.");}await download;
    if(!Core.Hash(file).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new Exception("Checksum bộ cài không đúng. Đã hủy tải.");ValidateFile(file,Version);
   }return file;}catch{if(File.Exists(file))File.Delete(file);throw;}
  }
  internal static void ValidateFile(string file,string version){var info=FileVersionInfo.GetVersionInfo(file);System.Version actual,wanted;if(info.ProductName!="CYAN NAS Setup"||!System.Version.TryParse(info.FileVersion,out actual)||!System.Version.TryParse(version.TrimStart('v'),out wanted)||Core.NormalizeVersion(actual)!=Core.NormalizeVersion(wanted))throw new Exception("Phiên bản trong bộ cài không khớp thông tin CYANPRO.");
}
  public static async Task Offer(IWin32Window owner){
   var release=await Check();if(release==null){SuiteNotice.Show(owner,"Đang dùng phiên bản mới nhất theo CYANPRO.");return;}
   string text="CYAN NAS "+release.Version+(release.Mandatory?" · Bản cập nhật bắt buộc":"")+"\n\n"+release.Notes+"\n\nTải bộ cài và kiểm tra SHA256? Thoát an toàn CYAN NAS trước khi cài.";
   if(SuiteNotice.Show(owner,text,"Cập nhật qua CYANPRO",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;string file=await release.Download(SuiteIdentity.Home);Process.Start("explorer.exe","/select,"+Core.Quote(file));SuiteNotice.Show(owner,"Bộ cài đã được xác minh SHA256 và phiên bản. Thoát an toàn rồi chạy CyanNasSetup.exe trong thư mục vừa mở.");
  }
 }
}
