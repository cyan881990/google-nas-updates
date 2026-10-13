using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using GoogleNas;
class UiTests {
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    static List<Control> All(Control parent) {
        var controls=new List<Control>(); foreach(Control child in parent.Controls) { controls.Add(child); controls.AddRange(All(child)); } return controls;
    }
    [STAThread] static int Main(string[] args) {
        try {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string root=Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
            foreach(float scale in new[]{1f,1.25f,1.5f,2f}) using(var form=new MainForm(false,new Services(Path.Combine(root,"data")),true)) {
                if(form.Text!="CYAN NAS") throw new Exception("Application title was not renamed.");
                form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-10000,-10000); form.Show(); Application.DoEvents();
                if(scale==1f) {
                    form.PreviewPage(2); Application.DoEvents();
                    var fieldFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    var user=(TextBox)typeof(MainForm).GetField("nasUser",fieldFlags).GetValue(form);
                    var password=(TextBox)typeof(MainForm).GetField("nasPassword",fieldFlags).GetValue(form);
                    user.Text="fixtureuser"; password.Text="Fixture!SavedPassword12";
                    foreach(var c in All(form)) if(c is Button && c.Text=="Lưu mật khẩu") ((Button)c).PerformClick();
                    password.Clear(); foreach(var c in All(form)) if(c is Button && c.Text=="Hiện mật khẩu") ((Button)c).PerformClick();
                    if(password.Text!="Fixture!SavedPassword12" || password.UseSystemPasswordChar || !password.Enabled) throw new Exception("Saved NAS password must be viewable for an existing account.");
                    foreach(var c in All(form)) if(c is Button && c.Text=="Ẩn mật khẩu") ((Button)c).PerformClick();
                    if(!password.UseSystemPasswordChar) throw new Exception("Hide password button failed.");
                    user.Text="otherfixture"; if(password.Text.Length!=0 || !password.UseSystemPasswordChar) throw new Exception("Changing NAS account must clear displayed password.");
                    Console.WriteLine("NAS password UI: save, reload, show, hide and account-change clearing passed.");
                }
                // Simulate both logical geometry and font pixel size without changing Windows display settings.
                var controls=All(form); var fonts=new Dictionary<Control,Font>(); foreach(var c in controls) fonts[c]=c.Font;
                form.SuspendLayout(); form.AutoScaleMode=AutoScaleMode.None; form.Scale(new SizeF(scale,scale));
                foreach(var pair in fonts) pair.Key.Font=new Font(pair.Value.FontFamily,pair.Value.Size*scale,pair.Value.Style);
                form.ResumeLayout(true); Application.DoEvents();
                foreach(var c in controls) if(c is Label && c.Text=="CYAN NAS") { if(c.Right>c.Parent.ClientSize.Width) throw new Exception("Sidebar title clipped at "+scale); }
                foreach(var c in controls) if(c is Label && c.Text=="Không gian làm việc của bạn, kết nối với Google Drive.") {
                    foreach(Control sibling in c.Parent.Controls) if(sibling is Label && sibling!=c && sibling.Bounds.IntersectsWith(c.Bounds)) throw new Exception("Header labels overlap at "+scale);
                }
                for(int page=0;page<5;page++) {
                    form.PreviewPage(page); Application.DoEvents();
                    using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size)); bitmap.Save(Path.Combine(root,"page-"+page+"-"+(int)(scale*100)+".png")); }
                }
            }
            using(var form=new MainForm(false,new Services(Path.Combine(root,"lifecycle")))) {
                form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-10000,-10000); form.Show(); Application.DoEvents(); form.Close(); Application.DoEvents();
                if(form.IsDisposed || !form.Visible || !form.ShowInTaskbar || form.WindowState!=FormWindowState.Minimized) throw new Exception("Close must retain a taskbar window.");
                PostMessage(form.Handle,Program.ShowWindowMessage,IntPtr.Zero,IntPtr.Zero); Application.DoEvents(); if(form.WindowState!=FormWindowState.Normal || !form.Visible) throw new Exception("Instance reopen message failed.");
                using(var release=new ManualResetEventSlim(false)) {
                    int probes=0; var probe=new DriveProbe(path=>{ Interlocked.Increment(ref probes); release.Wait(); return true; });
                    try {
                        var waiting=probe.Ready("N:\\",100); form.WindowState=FormWindowState.Minimized;
                        PostMessage(form.Handle,Program.ShowWindowMessage,IntPtr.Zero,IntPtr.Zero);
                        var until=DateTime.UtcNow.AddSeconds(2); while(!waiting.IsCompleted && DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(5); }
                        if(!waiting.IsCompleted || waiting.GetAwaiter().GetResult() || form.WindowState!=FormWindowState.Normal) throw new Exception("Blocked network probe must time out while window reopens.");
                        var again=probe.Ready("N:\\",100); until=DateTime.UtcNow.AddSeconds(2); while(!again.IsCompleted && DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(5); }
                        if(!again.IsCompleted || again.GetAwaiter().GetResult() || probes!=1) throw new Exception("Repeated network timeouts must reuse the existing worker.");
                    } finally { release.Set(); }
                }
                Console.WriteLine("Blocked network probe: UI reopen, bounded wait and single worker passed.");
                Console.WriteLine("Close keeps taskbar entry; reopen restores existing window.");
            }
            Console.WriteLine("Five UI pages rendered at simulated 100/125/150/200%; sidebar/header bounds passed."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
