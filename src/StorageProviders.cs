using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace GoogleNas {
 public class StorageField {
  public string Key,Label,Default; public bool Secret,File;
  public StorageField(string key,string label,string value="",bool secret=false,bool file=false) { Key=key;Label=label;Default=value;Secret=secret;File=file; }
 }
 public class StorageProvider {
  public string Id,Name,Backend,Hint; public bool OAuth; public StorageField[] Fields;
  public StorageProvider(string id,string name,string backend,bool oauth,string hint,params StorageField[] fields) { Id=id;Name=name;Backend=backend;OAuth=oauth;Hint=hint;Fields=fields; }
 }
 public class StorageConnection {
  public string Id=Guid.NewGuid().ToString("N"),Name="",Provider="smb",Letter="N",Path="";
  public bool AutoConnect=true,Verified=false,ReadOnly=false;
  public Dictionary<string,string> Options=new Dictionary<string,string>();
  public string Get(string key) { string value;return Options.TryGetValue(key,out value)?value:""; }
 }
 public static class StorageProviders {
  public static readonly StorageProvider[] All={
   new StorageProvider("smb","CYAN NAS / SMB","smb",false,"Nhập máy chủ LAN hoặc IP Tailscale. Thư mục gốc là tên share, ví dụ MEDIA.",new StorageField("host","Máy chủ / IP Tailscale"),new StorageField("user","Tài khoản"),new StorageField("pass","Mật khẩu","",true),new StorageField("domain","Tên máy / Domain (nếu có)")),
   new StorageProvider("ftp","FTP","ftp",false,"FTP tiêu chuẩn. Chọn FTPS nếu máy chủ yêu cầu TLS.",new StorageField("host","Máy chủ"),new StorageField("port","Cổng","21"),new StorageField("user","Tài khoản"),new StorageField("pass","Mật khẩu","",true)),
   new StorageProvider("ftps","FTPS","ftp",false,"Explicit TLS: cổng 21. Implicit TLS: thường cổng 990. Luôn kiểm tra chứng chỉ.",new StorageField("host","Máy chủ"),new StorageField("port","Cổng","21"),new StorageField("tls_mode","Chế độ TLS","Explicit TLS"),new StorageField("user","Tài khoản"),new StorageField("pass","Mật khẩu","",true)),
   new StorageProvider("sftp","SFTP","sftp",false,"Dùng mật khẩu hoặc khóa SSH. Chọn known_hosts đã xác minh để kiểm tra danh tính máy chủ.",new StorageField("host","Máy chủ"),new StorageField("port","Cổng","22"),new StorageField("user","Tài khoản"),new StorageField("pass","Mật khẩu","",true),new StorageField("key_file","Tệp khóa SSH (nếu dùng)","",false,true),new StorageField("key_file_pass","Mật khẩu khóa SSH (nếu có)","",true),new StorageField("known_hosts_file","Tệp known_hosts đã tin cậy","",false,true)),
   new StorageProvider("webdav","WebDAV","webdav",false,"Địa chỉ WebDAV đầy đủ, khuyến nghị HTTPS.",new StorageField("url","Địa chỉ WebDAV"),new StorageField("user","Tài khoản"),new StorageField("pass","Mật khẩu / App password","",true)),
   new StorageProvider("s3","Amazon S3","s3",false,"Thư mục gốc là bucket hoặc bucket/prefix. Có thể dùng endpoint S3 tương thích.",new StorageField("access_key_id","Access key ID"),new StorageField("secret_access_key","Secret access key","",true),new StorageField("region","Region","ap-southeast-1"),new StorageField("endpoint","Endpoint (tùy chọn)")),
   new StorageProvider("b2","Backblaze B2","b2",false,"Dùng Application key ID và Application key. Thư mục gốc là bucket.",new StorageField("account","Application key ID"),new StorageField("key","Application key","",true)),
   new StorageProvider("dropbox","Dropbox","dropbox",true,"Đăng nhập qua trình duyệt; không nhập mật khẩu tài khoản vào tool."),
   new StorageProvider("onedrive","Microsoft OneDrive","onedrive",true,"Đăng nhập Microsoft rồi chọn loại và ổ OneDrive / SharePoint trong bước cấp quyền."),
   new StorageProvider("drive","Google Drive","drive",true,"Đăng nhập Google qua trình duyệt. Có thể chọn Shared Drive trong bước cấu hình."),
   new StorageProvider("azureblob","Microsoft Azure Blob","azureblob",false,"Dùng Account key hoặc SAS URL. Thư mục gốc là container/prefix.",new StorageField("account","Storage account"),new StorageField("key","Account key","",true),new StorageField("sas_url","SAS URL (thay cho key)","",true)),
   new StorageProvider("azurefiles","Microsoft Azure Files","azurefiles",false,"Dùng Account key hoặc SAS URL. Thư mục gốc là tên File share.",new StorageField("account","Storage account"),new StorageField("key","Account key","",true),new StorageField("sas_url","SAS URL (thay cho key)","",true)),
   new StorageProvider("gcs","Google Cloud Storage","google cloud storage",true,"Dùng Service account JSON hoặc để trống để đăng nhập Google. Thư mục gốc là bucket.",new StorageField("service_account_file","Tệp Service account JSON (tùy chọn)","",false,true),new StorageField("project_number","Project number (tùy chọn)"))
  };
  public static StorageProvider Find(string id) { var p=All.FirstOrDefault(x=>x.Id==id);if(p==null)throw new Exception("Loại kết nối không được hỗ trợ.");return p; }
  public static void Validate(StorageConnection p) {
   Find(p.Provider);if(!Regex.IsMatch(p.Id??"",@"^[a-f0-9]{32}$"))throw new Exception("ID kết nối không hợp lệ.");
   NasService.ValidateLetter(p.Letter);if(string.IsNullOrWhiteSpace(p.Name) || p.Name.Length>70)throw new Exception("Nhập tên hiển thị tối đa 70 ký tự.");
   if((p.Path??"").IndexOfAny(new[]{'\r','\n','\0',':'})>=0)throw new Exception("Thư mục gốc chỉ chứa đường dẫn trong nguồn lưu trữ, không phải URL.");
   foreach(var value in p.Options.Values)if((value??"").IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new Exception("Thông tin kết nối không được có ký tự xuống dòng.");
   if(new[]{"smb","ftp","ftps","sftp"}.Contains(p.Provider)) { var host=p.Get("host");if(string.IsNullOrWhiteSpace(host)||host.Contains("/")||host.Contains("\\")||host.Any(char.IsWhiteSpace))throw new Exception("Nhập máy chủ dạng hostname hoặc IP, không kèm giao thức."); }
   int port;if(p.Get("port")!="" && (!int.TryParse(p.Get("port"),out port)||port<1||port>65535))throw new Exception("Cổng phải từ 1 đến 65535.");
   if(p.Provider=="sftp" && (p.Get("known_hosts_file")=="" || !File.Exists(p.Get("known_hosts_file"))))throw new Exception("Chọn tệp known_hosts đã xác minh cho SFTP.");
   foreach(var f in Find(p.Provider).Fields.Where(x=>x.File))if(p.Get(f.Key)!=""&&!File.Exists(p.Get(f.Key)))throw new Exception("Không tìm thấy tệp: "+f.Label);
   if(p.Provider=="webdav") { Uri uri;if(!Uri.TryCreate(p.Get("url"),UriKind.Absolute,out uri)||!new[]{"https","http"}.Contains(uri.Scheme)||uri.UserInfo!="")throw new Exception("Nhập URL WebDAV HTTP/HTTPS; tài khoản để ở ô riêng."); }
   if(new[]{"smb","s3","b2","azureblob","azurefiles","gcs"}.Contains(p.Provider)&&string.IsNullOrWhiteSpace(p.Path.Trim('/')))throw new Exception("Nhập thư mục gốc: tên share, bucket hoặc container.");
   if(p.Provider=="s3"&&(p.Get("access_key_id")==""||p.Get("secret_access_key")==""))throw new Exception("Nhập Access key ID và Secret access key.");
   if(p.Provider=="b2"&&(p.Get("account")==""||p.Get("key")==""))throw new Exception("Nhập Application key ID và key B2.");
   if(new[]{"azureblob","azurefiles"}.Contains(p.Provider)&&(p.Get("sas_url")==""&&(p.Get("account")==""||p.Get("key")=="")))throw new Exception("Nhập storage account và key, hoặc SAS URL.");
  }
  public static Dictionary<string,string> Options(StorageConnection p) {
   var result=new Dictionary<string,string>(p.Options);result.Remove("tls_mode");result["type"]=Find(p.Provider).Backend;
   if(p.Provider=="smb"){result["domain"]=p.Get("domain");result["user"]=p.Get("user");}
   if(p.Provider=="smb"&&p.Get("user").Contains(@"\")){int slash=p.Get("user").IndexOf('\\');result["domain"]=p.Get("user").Substring(0,slash);result["user"]=p.Get("user").Substring(slash+1);}
   if(p.Provider=="ftps") { bool implicitTls=p.Get("tls_mode")=="Implicit TLS";result["tls"]=implicitTls?"true":"false";result["explicit_tls"]=implicitTls?"false":"true"; }
   if(p.Provider=="ftp") { result["tls"]="false";result["explicit_tls"]="false"; }
   if(p.Provider=="s3") result["provider"]="AWS";
   if(p.Provider=="webdav") result["vendor"]="other";
   if(p.Provider=="drive") result["scope"]="drive";
   return result;
  }
 }
}
