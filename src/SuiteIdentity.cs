using System;
using System.IO;
namespace GoogleNas {
 public static class SuiteIdentity {
#if CYAN_SERVER
  public const bool Enabled=true; public const bool Server=true;
#elif CYAN_CLIENT
  public const bool Enabled=true; public const bool Server=false;
#else
  public const bool Enabled=false; public const bool Server=false;
#endif
  public static string Id { get { return Server?"CyanNasServer":"CyanNasClient"; } }
  public static string Name { get { return Server?"CYAN NAS SERVER":"CYAN NAS CLIENT"; } }
  public static string Home { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Id); } }
 }
}
