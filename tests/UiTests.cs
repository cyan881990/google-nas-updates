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
    static void Click(Form form,string name) {
        foreach(var control in All(form)) if(control is Button && control.Name==name) { ((Button)control).PerformClick(); Application.DoEvents(); return; }
        throw new Exception("Missing UI action: "+name);
    }
    static void VerifyLayout(MainForm form,int page,float scale,Size viewport) {
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var panels=(List<Panel>)typeof(MainForm).GetField("pages",flags).GetValue(form);
        for(int i=0;i<6;i++) if(panels[i].Visible!=(i==page)) throw new Exception("Feature navigation must show exactly one page.");
        var root=panels[page]; if(root.HorizontalScroll.Visible) throw new Exception("Horizontal page scrolling at "+scale+" / "+viewport+" / "+page+" client="+root.ClientSize+" child="+root.Controls[0].Bounds+" display="+root.DisplayRectangle+" scroll="+root.HorizontalScroll.Maximum);
        foreach(var c in All(root)) {
            if(!c.Visible) continue;
            if(c is TextBox || c is ComboBox || c is NumericUpDown || c is Button || c is CheckBox) {
                if(c.Left<0 || c.Right>c.Parent.ClientSize.Width+2 || c.Top<0 || c.Bottom>c.Parent.ClientSize.Height+2) throw new Exception("Clipped control "+c.Name+" / "+c.Text+" at "+scale+" / "+viewport+" / page "+page+": "+c.Bounds+" in "+c.Parent.ClientSize);
            }
            if(c is Label && c.Parent is Card && ((Label)c).AutoSize && c.Right>c.Parent.ClientSize.Width-5) throw new Exception("Clipped card label: "+c.Text+" at "+scale+" / "+viewport);
        }
        var grid=(DashboardColumns)root.Controls[0];
        if(form.ClientSize.Width>=1800 && scale==1 && !grid.IsWide) throw new Exception("Wide PC screen must use two columns: actual="+form.Size+" grid="+grid.Size);
        if(viewport.Width==1280 && scale==2 && grid.IsWide) throw new Exception("Compact high DPI screen must stack columns: grid="+grid.Size+" form="+form.Size+" root="+root.Size+" factor="+typeof(MainForm).GetProperty("UiScale",flags).GetValue(form,null));
        foreach(var c in All(form)) if(c.Name=="PageTitle" && c.Text!=new[]{"Google Drive","Ổ cứng thành NAS","Kết nối NAS","Mạng Tailscale","Cài đặt","Trợ giúp"}[page]) throw new Exception("Navigation title mismatch.");
    }
    [STAThread] static int Main(string[] args) {
        try {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string root=Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
            using(var policy=new DashboardColumns(()=>1)) {
                var a=new Card { Height=180 }; var b=new Card { Height=220 }; policy.Add(a,false); policy.Add(b,true);
                policy.Width=1200; if(!policy.IsWide || b.Left<=a.Right || b.Top!=a.Top) throw new Exception("Wide desktop layout must use two separate columns.");
                policy.Width=600; if(policy.IsWide || b.Left!=0 || b.Top<=a.Bottom) throw new Exception("Compact desktop layout must stack without overlap.");
            }
            foreach(float scale in new[]{1f,1.25f,1.5f,2f}) using(var form=new MainForm(false,new Services(Path.Combine(root,"data")),true)) {
                if(form.Text!="CYAN NAS") throw new Exception("Application title was not renamed.");
                if(form.FormBorderStyle!=FormBorderStyle.None) throw new Exception("Apple design must not show the Windows border or caption.");
                form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-10000,-10000); form.Show(); Application.DoEvents();
                if(scale==1f) {
                    form.PreviewPage(1); Application.DoEvents();
                    var fieldFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    var user=(TextBox)typeof(MainForm).GetField("nasUser",fieldFlags).GetValue(form);
                    var password=(TextBox)typeof(MainForm).GetField("nasPassword",fieldFlags).GetValue(form);
                    user.Text="fixtureuser"; password.Text="Fixture!SavedPassword12";
                    Click(form,"ServerSavePassword");
                    password.Clear(); Click(form,"ServerReveal");
                    if(password.Text!="Fixture!SavedPassword12" || password.UseSystemPasswordChar || !password.Enabled) throw new Exception("Saved NAS password must be viewable for an existing account.");
                    Click(form,"ServerReveal");
                    if(!password.UseSystemPasswordChar) throw new Exception("Hide password button failed.");
                    user.Text="otherfixture"; if(password.Text.Length!=0 || !password.UseSystemPasswordChar) throw new Exception("Changing NAS account must clear displayed password.");
                    Console.WriteLine("NAS password UI: save, reload, show, hide and account-change clearing passed.");
                    form.PreviewPage(2); Application.DoEvents();
                    var host=(ComboBox)typeof(MainForm).GetField("nasHost",fieldFlags).GetValue(form);
                    var share=(TextBox)typeof(MainForm).GetField("remoteShare",fieldFlags).GetValue(form);
                    var remoteAccount=(TextBox)typeof(MainForm).GetField("remoteUser",fieldFlags).GetValue(form);
                    var remoteSecret=(TextBox)typeof(MainForm).GetField("remotePassword",fieldFlags).GetValue(form);
                    new NasService(Path.Combine(root,"data")).SaveRemoteLogin("100.64.0.1","Data_1",@"NASPC\fixtureuser","Remote!FixtureSecret12");
                    host.Text="100.64.0.1"; share.Text="Data_1"; remoteAccount.Text="";
                    Click(form,"ClientReveal");
                    if(remoteAccount.Text!=@"NASPC\fixtureuser" || remoteSecret.Text!="Remote!FixtureSecret12" || remoteSecret.UseSystemPasswordChar) throw new Exception("Remote saved login UI failed.");
                    host.Text="100.64.0.2";
                    if(remoteSecret.Text.Length!=0 || !remoteSecret.UseSystemPasswordChar) throw new Exception("Changing remote host must clear and mask password.");
                    Console.WriteLine("Remote login UI: restore saved account/password and clear on host change passed.");
                }
                // Simulate both logical geometry and font pixel size without changing Windows display settings.
                var controls=All(form); var fonts=new Dictionary<Control,Font>(); foreach(var c in controls) fonts[c]=c.Font;
                form.SuspendLayout(); form.AutoScaleMode=AutoScaleMode.None; form.SetPreviewScale(scale); form.Scale(new SizeF(scale,scale));
                foreach(var pair in fonts) pair.Key.Font=new Font(pair.Value.FontFamily,pair.Value.Size*scale,pair.Value.Style);
                form.ResumeLayout(true); Application.DoEvents();
                foreach(var c in controls) if(c is Label && c.Text=="CYAN NAS") { if(c.Right>c.Parent.ClientSize.Width) throw new Exception("Sidebar title clipped at "+scale); }
                // Hosted Windows desktops may have a 1024px maximum tracking size. Allow offscreen render viewports.
                form.MinimumSize=Size.Empty; form.MaximumSize=new Size(8192,8192);
                foreach(var viewport in new[]{new Size(1024,768),new Size(1280,720),new Size(1366,768),new Size(1920,1080)}) {
                    form.Size=viewport; Application.DoEvents();
                    for(int page=0;page<6;page++) {
                        form.PreviewPage(page); Application.DoEvents();
                        VerifyLayout(form,page,scale,viewport);
                        if(page==0 && viewport.Width==1366) {
                            var advancedInput=(TextBox)typeof(MainForm).GetField("remote",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(form);
                            if(advancedInput.Visible) throw new Exception("Advanced Google connection must start collapsed.");
                            Click(form,"GoogleAdvanced"); form.PreviewPage(0); Application.DoEvents();
                            if(!advancedInput.Visible) throw new Exception("Advanced connection did not expand."); VerifyLayout(form,page,scale,viewport);
                            Click(form,"GoogleAdvanced"); form.PreviewPage(0); Application.DoEvents();
                            if(advancedInput.Visible) throw new Exception("Advanced connection did not collapse."); VerifyLayout(form,page,scale,viewport);
                        }
                        using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size)); bitmap.Save(Path.Combine(root,"page-"+page+"-"+(int)(scale*100)+"-"+viewport.Width+".png")); }
                    }
                }
            }
            using(var form=new MainForm(false,new Services(Path.Combine(root,"lifecycle")))) {
                form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-10000,-10000); form.Show(); Application.DoEvents();
                Click(form,"WindowMaximize"); if(form.WindowState!=FormWindowState.Maximized) throw new Exception("Green window button must maximize.");
                Click(form,"WindowMaximize"); if(form.WindowState!=FormWindowState.Normal) throw new Exception("Green window button must restore.");
                Click(form,"WindowMinimize"); if(form.WindowState!=FormWindowState.Minimized) throw new Exception("Yellow window button must minimize."); form.ShowWindow(); Application.DoEvents();
                if(form.WindowEdge(new Point(1,1)).ToInt32()!=13 || form.WindowEdge(new Point(form.ClientSize.Width-1,form.ClientSize.Height-1)).ToInt32()!=17) throw new Exception("Borderless corner resize hit tests failed.");
                Click(form,"WindowClose"); Application.DoEvents();
                if(form.IsDisposed || !form.Visible || !form.ShowInTaskbar || form.WindowState!=FormWindowState.Minimized) throw new Exception("Close must retain a taskbar window.");
                PostMessage(form.Handle,Program.ShowWindowMessage,IntPtr.Zero,IntPtr.Zero); Application.DoEvents(); if(form.WindowState!=FormWindowState.Normal || !form.Visible) throw new Exception("Instance reopen message failed.");
                var privateFlags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                var pollingField=typeof(MainForm).GetField("polling",privateFlags); pollingField.SetValue(form,true); int invoked=0;
                var pendingAction=(Task)typeof(MainForm).GetMethod("Run",privateFlags).Invoke(form,new object[]{new Func<Task>(()=>{ invoked++; return Task.FromResult(0); })});
                form.PreviewPage(1); Application.DoEvents(); if(invoked!=0 || pendingAction.IsCompleted) throw new Exception("User action must wait for existing background work.");
                pollingField.SetValue(form,false); var actionDeadline=DateTime.UtcNow.AddSeconds(2); while(!pendingAction.IsCompleted && DateTime.UtcNow<actionDeadline) { Application.DoEvents(); Thread.Sleep(5); }
                if(!pendingAction.IsCompleted || invoked!=1) throw new Exception("User click was lost while background polling completed."); pendingAction.GetAwaiter().GetResult();
                Console.WriteLine("User action queues behind background polling without blocking navigation.");
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
            Console.WriteLine("Six desktop pages at 1024/1280/1366/1920 widths and 100/125/150/200%: navigation, responsive columns, input/action bounds and header checks passed."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
