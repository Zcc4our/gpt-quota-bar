using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class InputRegression
{
    [StructLayout(LayoutKind.Sequential)] struct Point2 { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint ex, string cls, string text, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr data);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point2 p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref Point2 point);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr a, IntPtr b);
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static string Describe(IntPtr h) { uint pid; GetWindowThreadProcessId(h,out pid); try { return h.ToInt64()+"/"+pid+"/"+Process.GetProcessById((int)pid).ProcessName; } catch { return h.ToInt64()+"/"+pid; } }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        if (args[0] == "--fixture")
        {
            IntPtr host = CreateWindowEx(0x08000088, "STATIC", "Quota input regression fixture", 0x80000000, 180, 180, 900, 160, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            IntPtr button = CreateWindowEx(0, "BUTTON", "Underlying control in a separate process", 0x50000000, 20, 20, 750, 42, host, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            SetWindowPos(host, new IntPtr(-1), 180, 180, 900, 160, 0x10 | 0x40);
            Rect r; GetWindowRect(button, out r);
            File.WriteAllText(args[1], Json.Serialize(new { host=host.ToInt64(), button=button.ToInt64(), x=r.Left, y=r.Top, w=r.Right-r.Left, h=r.Bottom-r.Top }));
            using (var timer = new System.Windows.Forms.Timer { Interval=500 })
            {
                int ticks=0;
                timer.Tick += delegate { if (!IsWindow(host) || ++ticks > 30) Application.ExitThread(); };
                timer.Start(); Application.Run(new ApplicationContext());
            }
            return 0;
        }
        string statePath = Path.Combine(Path.GetDirectoryName(args[1]), "fixture-state.json");
        if (File.Exists(statePath)) File.Delete(statePath);
        Process fixture=null; Form bar=null; IntPtr hostHandle=IntPtr.Zero;
        var report=new List<string>();
        try
        {
            fixture=Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--fixture \""+statePath+"\"") { UseShellExecute=false, CreateNoWindow=true, WindowStyle=ProcessWindowStyle.Hidden });
            for (int i=0; !File.Exists(statePath) && i<100; i++) Thread.Sleep(50);
            Check(File.Exists(statePath), "fixture started");
            var state=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(statePath));
            hostHandle=new IntPtr(Convert.ToInt64(state["host"]));
            Rectangle bounds=new Rectangle(Convert.ToInt32(state["x"]),Convert.ToInt32(state["y"]),Convert.ToInt32(state["w"]),Convert.ToInt32(state["h"]));
            Assembly library=Assembly.LoadFrom(args[0]);
            Type type=library.GetType("GptQuotaBar.BarForm", true);
            bar=(Form)Activator.CreateInstance(type, true);
            MethodInfo present=type.GetMethod("Present", BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo attach=type.GetMethod("AttachTo", BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo hide=type.GetMethod("HidePassive", BindingFlags.Instance|BindingFlags.NonPublic);
            IntPtr foreground=GetForegroundWindow();
            attach.Invoke(bar,new object[]{hostHandle});
            report.Add("attach foreground: " + foreground.ToInt64() + " -> " + GetForegroundWindow().ToInt64() + "; fixture=" + hostHandle.ToInt64() + "; child=" + bar.Handle.ToInt64());
            var origin=new Point2(); ClientToScreen(hostHandle,ref origin);
            Rectangle local=new Rectangle(bounds.X-origin.X,bounds.Y-origin.Y,bounds.Width,bounds.Height);
            for (int i=0;i<30;i++)
            {
                present.Invoke(bar,new object[]{local,1.5f}); Application.DoEvents(); Thread.Sleep(30);
                Check(GetForegroundWindow()==foreground, "foreground must remain unchanged during show/update: " + Describe(foreground) + " -> " + Describe(GetForegroundWindow()) + "; test="+Process.GetCurrentProcess().Id+"; fixture="+fixture.Id);
                Check(GetAncestor(WindowFromPoint(new Point2{X=bounds.Left+bounds.Width/2,Y=bounds.Top+bounds.Height/2}),2)==hostHandle, "mouse hit must pass through to the separate-process host");
            }
            Check(IsWindowVisible(bar.Handle),"display remains visible"); report.Add("30 consecutive shows/updates preserve foreground");
            report.Add("30 hit-tests reach the underlying window in a separate process");
            long style=GetWindowLongPtr(bar.Handle,-20).ToInt64();
            Check((style & 0x08080020)==0x08080020,"layered, transparent, no-activate styles"); report.Add("non-activating mouse-pass-through styles present");
            Check(GetParent(bar.Handle)==hostHandle && (GetWindowLongPtr(bar.Handle,-16).ToInt64() & 0x40000000)!=0,"real child window relationship"); report.Add("real child window, not an owned popup");
            Rect parentStart; GetWindowRect(hostHandle,out parentStart);
            for(int i=1;i<=30;i++)
            {
                SetWindowPos(hostHandle,IntPtr.Zero,parentStart.Left+i*3,parentStart.Top+i*2,0,0,0x1|0x4|0x10);
                Rect childNow; GetWindowRect(bar.Handle,out childNow);
                Check(childNow.Left==bounds.Left+i*3 && childNow.Top==bounds.Top+i*2,"child moves synchronously without any Present call");
                Check(GetForegroundWindow()==foreground,"parent move preserves foreground");
            }
            report.Add("30 parent moves: zero coordinate error without refreshing the child position");
            Check(IsWindowEnabled(hostHandle),"host remains enabled"); report.Add("underlying host remains enabled");
            hide.Invoke(bar,null); Application.DoEvents();
            Check(!IsWindowVisible(bar.Handle) && GetForegroundWindow()==foreground,"hide preserves foreground"); report.Add("hide preserves foreground");
            type.GetMethod("Detach",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bar,null);
            Check((bool)attach.Invoke(bar,new object[]{hostHandle}),"reattach succeeds");
            present.Invoke(bar,new object[]{local,1.5f}); Application.DoEvents();
            Check(GetParent(bar.Handle)==hostHandle && GetForegroundWindow()==foreground,"reattach preserves parent and foreground");
            Check(!(bool)attach.Invoke(bar,new object[]{IntPtr.Zero}),"closed or invalid host is safely ignored");
            report.Add("reattach preserves foreground; unavailable host is ignored without a dialog");
            File.WriteAllText(args[1],Json.Serialize(new { passed=true,checks=report })); return 0;
        }
        catch(Exception ex) { File.WriteAllText(args[1],Json.Serialize(new { passed=false,error=ex.GetType().Name,checks=report })); return 1; }
        finally
        {
            if(bar!=null)bar.Dispose();
            if(hostHandle!=IntPtr.Zero)PostMessage(hostHandle,0x10,IntPtr.Zero,IntPtr.Zero);
            if(fixture!=null) { if(!fixture.WaitForExit(3000))fixture.Kill(); fixture.Dispose(); }
        }
    }
}
