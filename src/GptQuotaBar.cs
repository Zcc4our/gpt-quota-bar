using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GptQuotaBar
{
    static class Program
    {
        internal static readonly string Folder = AppDomain.CurrentDomain.BaseDirectory;
        internal static readonly string DataFolder = InitDataFolder();
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "GPT Quota Bar";
        internal static readonly EventWaitHandle Stop = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\GptQuotaBar.Stop");
        internal static readonly EventWaitHandle Inspect = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\GptQuotaBar.Inspect");

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                if (args.Contains("--self-test")) { Tests.Run(); return 0; }
                if (args.Contains("--ui-test")) { Application.EnableVisualStyles(); return Tests.RunUi() ? 0 : 1; }
                if (args.Contains("--probe")) { Probe().GetAwaiter().GetResult(); return 0; }
                if (args.Contains("--diagnose")) { Save("diagnostics.json", Native.Diagnose()); return 0; }
                if (args.Contains("--inspect")) { Inspect.Set(); return 0; }
                if (args.Contains("--render-preview"))
                {
                    int i = Array.IndexOf(args, "--render-preview");
                    using (var bitmap = Renderer.Preview()) bitmap.Save(i + 1 < args.Length ? args[i + 1] : Path.Combine(Folder, "preview.png"));
                    return 0;
                }
                if (args.Contains("--uninstall"))
                {
                    StartupTask.Remove();
                    using (var key = Registry.CurrentUser.CreateSubKey(RunKey)) key.DeleteValue(RunName, false);
                    Stop.Set(); return 0;
                }
                if (args.Contains("--stop")) { Stop.Set(); return 0; }
                if (args.Contains("--install"))
                {
                    StartupTask.Install();
                    using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                        key.DeleteValue(RunName, false);
                    return 0;
                }
                bool created;
                using (var mutex = new Mutex(true, @"Local\GptQuotaBar.Instance", out created))
                {
                    if (!created) return 0;
                    Stop.Reset();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (var context = new BarContext()) Application.Run(context);
                }
                return 0;
            }
            catch (Exception ex)
            {
                // Deliberately exclude exception messages and RPC payloads from the report.
                Save("error.json", new { error = ex.GetType().Name, at = DateTimeOffset.Now.ToString("o") });
                return 1;
            }
        }
        static string InitDataFolder()
        {
            try {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GptQuotaBar");
                Directory.CreateDirectory(folder);
                string oldFile = Path.Combine(Folder, "settings.json"), newFile = Path.Combine(folder, "settings.json");
                if (!File.Exists(newFile) && File.Exists(oldFile) && !string.Equals(oldFile, newFile, StringComparison.OrdinalIgnoreCase)) {
                    try { File.Copy(oldFile, newFile); File.Delete(oldFile); } catch { }
                }
                return folder;
            } catch { return Folder; }
        }
        internal static void Save(string name, object value) { File.WriteAllText(Path.Combine(DataFolder, name), Json.Encode(value), new UTF8Encoding(false)); }
        internal static void SaveBeside(string name, object value) { File.WriteAllText(Path.Combine(Folder, name), Json.Encode(value), new UTF8Encoding(false)); }
        static async Task Probe()
        {
            using (var client = new QuotaClient())
            {
                await client.Start();
                var snapshot = await client.Read();
                Save("quota-probe.json", snapshot.Export());
            }
        }
    }

    static class StartupTask
    {
        const string Name = "GPT Quota Bar";
        static dynamic Service()
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            service.Connect(); return service;
        }
        internal static void Install()
        {
            dynamic service = Service();
            dynamic task = service.NewTask(0);
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            task.RegistrationInfo.Description = "Independent GPT quota bar watcher for the current Windows user.";
            task.Principal.UserId = user;
            task.Principal.LogonType = 3;
            task.Principal.RunLevel = 0;
            task.Settings.Enabled = true;
            task.Settings.DisallowStartIfOnBatteries = false;
            task.Settings.StopIfGoingOnBatteries = false;
            task.Settings.ExecutionTimeLimit = "PT0S";
            task.Settings.MultipleInstances = 2;
            dynamic trigger = task.Triggers.Create(9);
            trigger.UserId = user;
            dynamic action = task.Actions.Create(0);
            action.Path = Application.ExecutablePath;
            action.WorkingDirectory = Program.Folder;
            dynamic folder = service.GetFolder("\\");
            dynamic registered = folder.RegisterTaskDefinition(Name, task, 6, user, null, 3, null);
            registered.Run(null);
            Program.Save("installation.json", new { mechanism = "Windows Task Scheduler", task = Name, version = "1.3.0" });
        }
        internal static void Remove()
        {
            try { dynamic service = Service(); service.GetFolder("\\").DeleteTask(Name, 0); }
            catch (COMException ex) { if ((uint)ex.ErrorCode != 0x80070002) throw; }
        }
    }

    static class Json
    {
        static readonly object Gate = new object();
        static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = 4194304 };
        internal static string Encode(object value) { lock (Gate) return Serializer.Serialize(value); }
        internal static Dictionary<string, object> Decode(string text) { lock (Gate) return Serializer.DeserializeObject(text) as Dictionary<string, object>; }
        internal static object Get(Dictionary<string, object> obj, string key) { object value; return obj != null && obj.TryGetValue(key, out value) ? value : null; }
        internal static Dictionary<string, object> Obj(Dictionary<string, object> obj, string key) { return Get(obj, key) as Dictionary<string, object>; }
    }

    static class Settings
    {
        internal static bool Light, Notify = true;
        static Settings()
        {
            try {
                var data = Json.Decode(File.ReadAllText(Path.Combine(Program.DataFolder, "settings.json")));
                if (Json.Get(data, "light") is bool) Light = (bool)data["light"];
                if (Json.Get(data, "notify") is bool) Notify = (bool)data["notify"];
            } catch { }
        }
        internal static void Save() { Program.Save("settings.json", new { light = Light, notify = Notify }); }
    }

    sealed class QuotaWindow
    {
        internal int Minutes;
        internal double Used;
        internal long Reset;
        internal double Remaining { get { return Math.Max(0, Math.Min(100, 100 - Used)); } }
        internal bool Expired(DateTimeOffset now) { return Reset > 0 && now.ToUnixTimeSeconds() >= Reset; }
        internal string Display(DateTimeOffset now) { return Expired(now) ? "--" : Remaining.ToString("0.#", CultureInfo.InvariantCulture) + "%"; }
    }

    sealed class Snapshot
    {
        internal readonly List<QuotaWindow> Windows = new List<QuotaWindow>();
        internal QuotaWindow Five { get { return Windows.FirstOrDefault(w => w.Minutes == 300); } }
        internal QuotaWindow Week { get { return Windows.FirstOrDefault(w => w.Minutes == 10080); } }
        internal DateTimeOffset At = DateTimeOffset.Now;
        internal string Plan;
        internal bool HasCredits, Unlimited;
        internal string Balance;
        internal bool Empty { get { return Windows.Count == 0 && !HasCredits && !Unlimited; } }
        internal bool CreditsOnly { get { return Windows.Count == 0 && (HasCredits || Unlimited); } }
        internal string PlanName
        {
            get {
                switch (Plan) {
                    case "free": return "Free"; case "go": return "Go"; case "plus": return "Plus";
                    case "pro": return "Pro"; case "team": return "Team"; case "business": return "Business";
                    case "enterprise": return "Enterprise"; case "edu": return "Edu"; default: return "";
                }
            }
        }
        internal static string PeriodLabel(int minutes)
        {
            if (minutes <= 0) return "";
            if (minutes % 1440 == 0) return (minutes / 1440).ToString(CultureInfo.InvariantCulture) + "d";
            if (minutes % 60 == 0) return (minutes / 60).ToString(CultureInfo.InvariantCulture) + "h";
            return minutes.ToString(CultureInfo.InvariantCulture) + "min";
        }
        internal static Snapshot Parse(Dictionary<string, object> result)
        {
            bool hasBuckets = result != null && result.ContainsKey("rateLimitsByLimitId");
            object rawBuckets = Json.Get(result, "rateLimitsByLimitId");
            var buckets = rawBuckets as Dictionary<string, object>;
            var bucket = hasBuckets ? Json.Obj(buckets, "codex") : Json.Obj(result, "rateLimits");
            // An explicit multi-bucket response without codex must never select another model's quota.
            string plan = Convert.ToString(Json.Get(bucket, "planType"), CultureInfo.InvariantCulture).ToLowerInvariant();
            var snapshot = new Snapshot { Plan = System.Text.RegularExpressions.Regex.IsMatch(plan, "^[a-z0-9_-]{1,24}$") ? plan : "" };
            var credits = Json.Obj(bucket, "credits");
            snapshot.HasCredits = Json.Get(credits, "hasCredits") is bool && (bool)credits["hasCredits"];
            snapshot.Unlimited = Json.Get(credits, "unlimited") is bool && (bool)credits["unlimited"];
            string balance = Convert.ToString(Json.Get(credits, "balance"), CultureInfo.InvariantCulture);
            if (balance.Length <= 12 && System.Text.RegularExpressions.Regex.IsMatch(balance, "^[0-9]+(\\.[0-9]+)?$")) snapshot.Balance = balance;
            foreach (string name in new[] { "primary", "secondary" })
            {
                var w = Json.Obj(bucket, name);
                if (w == null || Json.Get(w, "usedPercent") == null || Json.Get(w, "windowDurationMins") == null) continue;
                try {
                    double used = Convert.ToDouble(w["usedPercent"], CultureInfo.InvariantCulture);
                    int minutes = Convert.ToInt32(w["windowDurationMins"], CultureInfo.InvariantCulture);
                    if (double.IsNaN(used) || double.IsInfinity(used) || minutes <= 0 || snapshot.Windows.Any(x => x.Minutes == minutes)) continue;
                    snapshot.Windows.Add(new QuotaWindow { Minutes = minutes, Used = used,
                        Reset = Json.Get(w, "resetsAt") == null ? 0 : Convert.ToInt64(w["resetsAt"], CultureInfo.InvariantCulture) });
                } catch (FormatException) { } catch (OverflowException) { } catch (InvalidCastException) { }
            }
            snapshot.Windows.Sort((a, b) => a.Minutes.CompareTo(b.Minutes));
            return snapshot;
        }
        internal object Export()
        {
            return new { plan = Plan, updatedAt = At.ToString("o"), source = "codex app-server account/rateLimits/read",
                credits = new { hasCredits = HasCredits, unlimited = Unlimited, balance = Balance },
                windows = Windows.Select(w => new {
                    durationMinutes = w.Minutes, usedPercent = w.Used, remainingPercent = w.Remaining, resetsAt = w.Reset }).ToArray() };
        }
    }

    sealed class RpcFailure : Exception
    {
        internal int Code;
        internal RpcFailure(int code) { Code = code; }
    }

    static class ChildJob
    {
        [StructLayout(LayoutKind.Sequential)] struct BasicLimits {
            internal long ProcessTime, JobTime; internal uint Flags; internal UIntPtr MinWorking, MaxWorking;
            internal uint Active; internal UIntPtr Affinity; internal uint Priority, Scheduling;
        }
        [StructLayout(LayoutKind.Sequential)] struct IoCounters {
            internal ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes;
        }
        [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits {
            internal BasicLimits Basic; internal IoCounters Io; internal UIntPtr ProcessMemory, JobMemory, PeakProcess, PeakJob;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        static readonly IntPtr Job = Create();
        static IntPtr Create()
        {
            IntPtr job = IntPtr.Zero, buffer = IntPtr.Zero;
            try {
                job = CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero) return IntPtr.Zero;
                var limits = new ExtendedLimits(); limits.Basic.Flags = 0x2000;
                int size = Marshal.SizeOf(typeof(ExtendedLimits)); buffer = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(limits, buffer, false);
                if (SetInformationJobObject(job, 9, buffer, (uint)size)) return job;
            } catch { }
            finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
            if (job != IntPtr.Zero) CloseHandle(job);
            return IntPtr.Zero;
        }
        internal static void Assign(Process process) { try { if (Job != IntPtr.Zero) AssignProcessToJobObject(Job, process.Handle); } catch { } }
    }

    sealed class CodexExecutable
    {
        internal string Path, Tier;
        internal CodexExecutable(string path, string tier) { Path = path; Tier = tier; }
    }
    sealed class UntrustedPathException : Exception { }
    static class ExecutableTrust
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct FileInfoData {
            internal uint Size; internal string FilePath; internal IntPtr FileHandle, KnownSubject;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct TrustData {
            internal uint Size; internal IntPtr PolicyCallback, SipClient; internal uint UiChoice, RevocationChecks, UnionChoice;
            internal IntPtr FileInfo; internal uint StateAction; internal IntPtr StateData, UrlReference; internal uint ProviderFlags, UiContext;
            internal IntPtr SignatureSettings;
        }
        [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] static extern int WinVerifyTrust(IntPtr hwnd, [In] ref Guid action, ref TrustData data);
        internal static bool Verify(string path)
        {
            IntPtr file = IntPtr.Zero; var data = new TrustData();
            Guid action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            try {
                var info = new FileInfoData { Size = (uint)Marshal.SizeOf(typeof(FileInfoData)), FilePath = path };
                file = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfoData)));
                Marshal.StructureToPtr(info, file, false);
                data.Size = (uint)Marshal.SizeOf(typeof(TrustData)); data.UiChoice = 2; data.UnionChoice = 1;
                data.FileInfo = file; data.StateAction = 1; data.ProviderFlags = 0x1010;
                if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) return false;
                using (var certificate = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path))
                    return certificate.Subject.IndexOf("OpenAI", StringComparison.OrdinalIgnoreCase) >= 0;
            } catch { return false; }
            finally {
                if (data.StateData != IntPtr.Zero) { data.StateAction = 2; try { WinVerifyTrust(new IntPtr(-1), ref action, ref data); } catch { } }
                if (file != IntPtr.Zero) { Marshal.DestroyStructure(file, typeof(FileInfoData)); Marshal.FreeHGlobal(file); }
            }
        }
    }

    sealed class QuotaClient : IDisposable
    {
        Process child;
        readonly object writeGate = new object();
        readonly ConcurrentDictionary<int, TaskCompletionSource<Dictionary<string, object>>> pending = new ConcurrentDictionary<int, TaskCompletionSource<Dictionary<string, object>>>();
        int nextId;
        volatile bool disposed;
        volatile bool trusted;
        string accountKey;
        internal event Action<Snapshot, string> Changed;
        internal event Action AccountChanged;
        internal async Task Start()
        {
            CodexExecutable exe = FindCodex();
            child = Process.Start(new ProcessStartInfo(exe.Path, "app-server") {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Program.Folder });
            ChildJob.Assign(child);
            new Thread(ReadLoop) { IsBackground = true }.Start();
            Process started = child;
            new Thread((ThreadStart)delegate { try { while (started.StandardError.ReadLine() != null) { } } catch { } }) { IsBackground = true }.Start();
            await Rpc("initialize", new { clientInfo = new { name = "gpt_quota_bar", title = "GPT Quota Bar", version = "1.3.0" } });
            Send(new { method = "initialized", @params = new { } });
        }
        internal async Task<Snapshot> Read()
        {
            trusted = false;
            var result = await Rpc("account/read", new { refreshToken = false });
            var account = Json.Obj(result, "account");
            string kind = Convert.ToString(Json.Get(account, "type"));
            if (account == null || kind == "apiKey" || kind == "amazonBedrock")
            {
                Raise(null, "请在 GPT / Codex 中登录 ChatGPT 账户");
                throw new RpcFailure(401);
            }
            string identity = Convert.ToString(Json.Get(account, "id")) + ":" + Convert.ToString(Json.Get(account, "email"));
            if (accountKey != null && accountKey != identity) Raise(null, "账户已切换，正在更新");
            accountKey = identity;
            var quota = await Rpc("account/rateLimits/read", new { });
            var snapshot = Snapshot.Parse(quota);
            trusted = true;
            return snapshot;
        }
        void Raise(Snapshot snapshot, string status) { var changed = Changed; if (changed != null && !disposed) changed(snapshot, status); }
        void ReadLoop()
        {
            try
            {
                string line;
                while (!disposed && (line = child.StandardOutput.ReadLine()) != null)
                {
                    try {
                    var message = Json.Decode(line);
                    object idValue = Json.Get(message, "id");
                    string method = Convert.ToString(Json.Get(message, "method"));
                    if (idValue != null && method.Length == 0)
                    {
                        TaskCompletionSource<Dictionary<string, object>> waiter;
                        if (!pending.TryRemove(Convert.ToInt32(idValue), out waiter)) continue;
                        var error = Json.Obj(message, "error");
                        if (error != null) waiter.TrySetException(new RpcFailure(Convert.ToInt32(Json.Get(error, "code"))));
                        else waiter.TrySetResult(Json.Obj(message, "result"));
                    }
                    else if (method == "account/rateLimits/updated" && trusted)
                        Raise(Snapshot.Parse(Json.Obj(message, "params")), "");
                    else if (method == "account/updated")
                    {
                        trusted = false;
                        Raise(null, "登录状态已变化，正在重新读取");
                        var changed = AccountChanged; if (changed != null) changed();
                    }
                    else if (idValue != null && method.Length > 0)
                        Send(new { id = idValue, error = new { code = -32601, message = "Unsupported read-only client request" } });
                    } catch { }
                }
            }
            catch { }
            finally
            {
                foreach (var p in pending) p.Value.TrySetException(new IOException("app-server disconnected"));
                pending.Clear();
            }
        }
        async Task<Dictionary<string, object>> Rpc(string method, object parameters)
        {
            int id = Interlocked.Increment(ref nextId);
            var completion = new TaskCompletionSource<Dictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = completion;
            try
            {
                Send(new { id = id, method = method, @params = parameters });
                if (await Task.WhenAny(completion.Task, Task.Delay(25000)) != completion.Task) throw new TimeoutException();
                return await completion.Task;
            }
            finally { TaskCompletionSource<Dictionary<string, object>> unused; pending.TryRemove(id, out unused); }
        }
        void Send(object value)
        {
            lock (writeGate)
            {
                if (disposed) throw new ObjectDisposedException("QuotaClient");
                child.StandardInput.WriteLine(Json.Encode(value));
                child.StandardInput.Flush();
            }
        }
        internal static CodexExecutable FindCodex()
        {
            string configured = Environment.GetEnvironmentVariable("GPT_QUOTA_CODEX_PATH");
            if (!string.IsNullOrWhiteSpace(configured) && string.Equals(Path.GetFileName(configured), "codex.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(configured)) return new CodexExecutable(configured, "Configured");
            // Prefer the current desktop bundle, so its credential handling matches the host.
            foreach (string name in new[] { "ChatGPT", "Codex" })
                foreach (var p in Process.GetProcessesByName(name))
                    using (p) try {
                        string host = p.MainModule.FileName;
                        bool desktop = name == "ChatGPT" || Directory.Exists(Path.Combine(Path.GetDirectoryName(host), "resources"));
                        string candidate = Path.Combine(Path.GetDirectoryName(host), "resources", "codex.exe");
                        if (desktop && host.IndexOf("OpenAI", StringComparison.OrdinalIgnoreCase) >= 0 && File.Exists(candidate)) return new CodexExecutable(candidate, "DesktopBundle");
                    } catch { }
            string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
            if (Directory.Exists(local))
                foreach (string dir in Directory.GetDirectories(local).OrderByDescending(Directory.GetLastWriteTimeUtc)) {
                    string candidate = Path.Combine(dir, "codex.exe");
                    if (File.Exists(candidate)) return new CodexExecutable(candidate, "LocalInstall");
                }
            bool rejected = false;
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                if (dir.Length > 0) try {
                    string candidate = Path.Combine(dir.Trim('"'), "codex.exe");
                    if (File.Exists(candidate)) { if (ExecutableTrust.Verify(candidate)) return new CodexExecutable(candidate, "Path"); rejected = true; }
                } catch { }
            if (rejected) throw new UntrustedPathException();
            throw new FileNotFoundException("codex.exe");
        }
        public void Dispose()
        {
            disposed = true;
            trusted = false;
            if (child == null) return;
            try { child.StandardInput.Close(); if (!child.WaitForExit(1500)) child.Kill(); } catch { }
            child.Dispose(); child = null;
        }
    }

    sealed class Poller : IDisposable
    {
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Thread worker;
        volatile bool running, foreground, stopping;
        int force;
        internal event Action<Snapshot, string> Changed;
        internal Poller() { worker = new Thread(Loop) { IsBackground = true, Name = "GPT quota reader" }; worker.Start(); }
        internal void SetState(bool exists, bool active)
        {
            if (running != exists || foreground != active) { running = exists; foreground = active; wake.Set(); }
        }
        internal void Refresh() { Interlocked.Exchange(ref force, 1); wake.Set(); }
        void Raise(Snapshot s, string status) { var handler = Changed; if (handler != null && !stopping) handler(s, status); }
        void Loop()
        {
            QuotaClient client = null;
            DateTime last = DateTime.MinValue, retry = DateTime.MinValue;
            int failures = 0;
            try
            {
                while (!stopping)
                {
                    if (!running)
                    {
                        if (client != null) { client.Dispose(); client = null; Raise(null, "等待 GPT 启动"); }
                        last = DateTime.MinValue; retry = DateTime.MinValue;
                        wake.WaitOne(1000); continue;
                    }
                    bool manual = Interlocked.Exchange(ref force, 0) != 0;
                    int interval = foreground ? 30 : 300;
                    if (!manual && (DateTime.UtcNow < retry || (DateTime.UtcNow - last).TotalSeconds < interval)) { wake.WaitOne(1000); continue; }
                    try
                    {
                        if (client == null)
                        {
                            client = new QuotaClient();
                            client.Changed += delegate(Snapshot s, string status) {
                                if (s != null && s.Empty) { Raise(null, "未读取到 Codex 额度（套餐或接口可能不支持）"); Refresh(); return; }
                                Raise(s, status);
                            };
                            client.AccountChanged += Refresh;
                            client.Start().GetAwaiter().GetResult();
                        }
                        Snapshot snapshot = client.Read().GetAwaiter().GetResult();
                        if (snapshot.Empty) {
                            Raise(null, "正在重新读取额度");
                            Thread.Sleep(1000); if (stopping) break;
                            snapshot = client.Read().GetAwaiter().GetResult();
                            if (snapshot.Empty) {
                                Raise(null, "未读取到 Codex 额度（套餐或接口可能不支持）");
                                last = DateTime.UtcNow; retry = last.AddSeconds(120); continue;
                            }
                        }
                        Raise(snapshot, ""); failures = 0; retry = DateTime.MinValue;
                    }
                    catch (RpcFailure ex)
                    {
                        failures++;
                        if (ex.Code == 401 || ex.Code == 403) Raise(null, "请重新登录 ChatGPT 账户");
                        else Raise(null, "额度读取失败，稍后重试");
                        retry = DateTime.UtcNow.AddSeconds(ex.Code == 401 || ex.Code == 403 ? 300 : Math.Min(300, 30 * failures));
                    }
                    catch (UntrustedPathException)
                    {
                        Raise(null, "PATH 中的 codex.exe 未通过 OpenAI 签名校验，已忽略");
                        retry = DateTime.UtcNow.AddSeconds(300);
                    }
                    catch
                    {
                        failures++;
                        Raise(null, "连接暂不可用，稍后重试");
                        if (client != null) { client.Dispose(); client = null; }
                        retry = DateTime.UtcNow.AddSeconds(Math.Min(300, 30 * failures));
                    }
                    last = DateTime.UtcNow;
                    wake.WaitOne(1000);
                }
            }
            finally { if (client != null) client.Dispose(); }
        }
        public void Dispose() { stopping = true; wake.Set(); worker.Join(2000); }
    }

    sealed class BarContext : ApplicationContext
    {
        readonly BarForm bar;
        readonly NotifyIcon tray;
        readonly Icon icon;
        readonly System.Windows.Forms.Timer timer;
        readonly Poller poller;
        readonly WindowsFormsSynchronizationContext ui;
        DateTime discoverAt;
        List<IntPtr> targets = new List<IntPtr>();
        IntPtr previous;
        bool desktopRunning;
        bool lastVisible;
        DateTime repaintAt;
        bool disposed;
        readonly HashSet<long> refreshedResets = new HashSet<long>();
        readonly HashSet<string> notifiedWindows = new HashSet<string>();
        DateTime lastResetRefresh = DateTime.MinValue;
        internal static string LimitTrayText(string text) { return text.Length <= 127 ? text : text.Substring(0, 127); }
        internal BarContext()
        {
            bar = new BarForm();
            ui = new WindowsFormsSynchronizationContext();
            poller = new Poller();
            poller.Changed += delegate(Snapshot s, string status) {
                if (disposed || bar.IsDisposed) return;
                try { ui.Post(delegate {
                    if (disposed) return;
                    if (s != null) bar.Data = s;
                    // Fail closed: unknown identity, unavailable connection and failed reads clear values.
                    else bar.Data = null;
                    bar.Status = status; bar.Invalidate();
                    tray.Text = LimitTrayText(s == null ? "GPT 额度栏 · " + status : TrayText(s));
                    if (s != null && Settings.Notify) foreach (var w in s.Windows) {
                        string key = w.Minutes.ToString(CultureInfo.InvariantCulture) + ":" + w.Reset.ToString(CultureInfo.InvariantCulture);
                        if (!w.Expired(DateTimeOffset.Now) && w.Remaining <= 10 && notifiedWindows.Add(key))
                            tray.ShowBalloonTip(5000, "额度提醒", Snapshot.PeriodLabel(w.Minutes) + " 剩余 " + w.Display(DateTimeOffset.Now), ToolTipIcon.Warning);
                    }
                }, null); } catch (InvalidOperationException) { }
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add("立即刷新", null, delegate { poller.Refresh(); });
            menu.Items.Add("切换深色 / 浅色", null, delegate { bar.Light = !bar.Light; bar.SaveTheme(); bar.Invalidate(); });
            var notifyItem = new ToolStripMenuItem("低额度提醒") { Checked = Settings.Notify, CheckOnClick = true };
            notifyItem.CheckedChanged += delegate { Settings.Notify = notifyItem.Checked; Settings.Save(); };
            menu.Items.Add(notifyItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出额度栏", null, delegate { ExitThread(); });
            icon = Renderer.MakeIcon();
            tray = new NotifyIcon { Icon = icon, Text = "GPT 顶部额度栏", Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += delegate { poller.Refresh(); };
            timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += Tick; timer.Start(); Tick(null, EventArgs.Empty);
        }
        void Tick(object sender, EventArgs args)
        {
            if (Program.Stop.WaitOne(0)) { ExitThread(); return; }
            if (bar.Data != null && (DateTime.UtcNow - lastResetRefresh).TotalSeconds >= 20) {
                var expired = bar.Data.Windows.FirstOrDefault(w => w.Expired(DateTimeOffset.Now) && !refreshedResets.Contains(w.Reset));
                if (expired != null) { refreshedResets.Add(expired.Reset); lastResetRefresh = DateTime.UtcNow; poller.Refresh(); }
            }
            if (DateTime.UtcNow >= discoverAt)
            {
                targets = Native.FindTargets(out desktopRunning);
                discoverAt = DateTime.UtcNow.AddSeconds(1);
            }
            IntPtr fg = Native.GetAncestor(Native.GetForegroundWindow(), 2);
            IntPtr target = targets.Contains(fg) ? fg : targets.Contains(previous) ? previous : targets.FirstOrDefault();
            previous = target;
            bool active = fg == target || (target != IntPtr.Zero && Native.GetAncestor(fg, 3) == target);
            bool visible = target != IntPtr.Zero && Native.IsWindowVisible(target) && !Native.IsIconic(target) && !Native.IsCloaked(target);
            Rectangle bounds;
            float scale;
            visible = visible && Native.TryPlacement(target, out bounds, out scale);
            if (visible && Native.TryPlacement(target, out bounds, out scale))
            {
                visible = bar.AttachTo(target);
                if (visible) {
                    bar.Present(bounds, scale);
                    if (!lastVisible || DateTime.UtcNow >= repaintAt) {
                        bar.Invalidate(); repaintAt = DateTime.UtcNow.AddSeconds(1);
                    }
                }
            }
            else bar.HidePassive();
            if (bar.Host != IntPtr.Zero && !Native.IsWindow(bar.Host)) bar.Detach();
            poller.SetState(desktopRunning, visible && active);
            lastVisible = visible;
            if (Program.Inspect.WaitOne(0))
            {
                bool shown = bar.IsHandleCreated && Native.IsWindowVisible(bar.Handle);
                Program.Save("live-state.json", new { at = DateTimeOffset.Now.ToString("o"), visible = shown,
                    nativeVisible = shown, mode = "embedded-child", foreground = fg.ToInt64(), target = target.ToInt64(),
                    host = bar.Host.ToInt64(), screenBounds = bar.IsHandleCreated ? Native.ScreenBounds(bar.Handle).ToString() : "",
                    status = bar.Status, quota = bar.Data == null ? null : bar.Data.Export() });
                if (bar.IsHandleCreated) using (var bitmap = new Bitmap(bar.ClientSize.Width, bar.ClientSize.Height)) {
                    bar.DrawToBitmap(bitmap, bar.ClientRectangle); bitmap.Save(Path.Combine(Program.DataFolder, "live-bar.png"));
                }
            }
        }
        static string TrayText(Snapshot snapshot)
        {
            var lines = new List<string>();
            if (snapshot.PlanName.Length > 0) lines.Add(snapshot.PlanName);
            foreach (var w in snapshot.Windows)
                lines.Add(Snapshot.PeriodLabel(w.Minutes) + " " + Renderer.Value(w) + (w.Reset > 0
                    ? " · 重置 " + DateTimeOffset.FromUnixTimeSeconds(w.Reset).ToLocalTime().ToString("MM-dd HH:mm") : ""));
            if (snapshot.CreditsOnly) lines.Add(snapshot.Unlimited ? "积分 无限" : snapshot.Balance == null ? "积分 可用" : "积分 余额 " + snapshot.Balance);
            return string.Join("\n", lines);
        }
        protected override void ExitThreadCore() { Dispose(); base.ExitThreadCore(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true; timer.Stop(); timer.Dispose(); poller.Dispose(); tray.Visible = false; tray.Dispose(); icon.Dispose(); bar.Dispose(); ui.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    sealed class BarForm : Form
    {
        internal Snapshot Data;
        internal string Status = "正在读取额度";
        internal float ScaleFactor = 1;
        internal bool Light;
        internal IntPtr Host { get; private set; }
        Rectangle localBounds;
        internal BarForm()
        {
            TopLevel = false; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true; Text = "GPT Quota Bar";
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false); TabStop = false;
            Light = Settings.Light;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get {
                var p = base.CreateParams;
                p.Parent = Host;
                p.Style = (p.Style & ~unchecked((int)0x80000000)) | 0x40000000;
                p.ExStyle = (p.ExStyle & ~0x00010008) | 0x08000000 | 0x00080000 | 0x00000020;
                return p;
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!Native.SetLayeredWindowAttributes(Handle, 0, 255, 2)) throw new System.ComponentModel.Win32Exception();
        }
        internal bool AttachTo(IntPtr host)
        {
            if (host == Host && IsHandleCreated) return true;
            Detach();
            if (!Native.IsWindow(host)) return false;
            Host = host;
            IntPtr previousDpi = Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(host));
            try { IntPtr created = Handle; return true; }
            catch (System.ComponentModel.Win32Exception) { Detach(); return false; }
            finally { if (previousDpi != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(previousDpi); }
        }
        internal void Detach() { if (IsHandleCreated) DestroyHandle(); Host = IntPtr.Zero; localBounds = Rectangle.Empty; }
        internal void Present(Rectangle bounds, float scale)
        {
            if (Host == IntPtr.Zero || !Native.IsWindow(Host)) return;
            if (IsHandleCreated && localBounds == bounds && ScaleFactor == scale && Native.IsWindowVisible(Handle)) return;
            ScaleFactor = scale;
            // Child coordinates stay fixed while Windows moves the parent; no screen-position polling.
            if (Native.SetWindowPos(Handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0010 | 0x0040 | 0x0200)) localBounds = bounds;
        }
        internal void HidePassive() { if (IsHandleCreated) Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0080); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0021) { m.Result = new IntPtr(4); return; }
            if (m.Msg == 0x0084) { m.Result = new IntPtr(-1); return; }
            base.WndProc(ref m);
        }
        internal void SaveTheme() { Settings.Light = Light; Settings.Save(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Renderer.Paint(e.Graphics, ClientSize.Width / ScaleFactor, ClientSize.Height / ScaleFactor, ScaleFactor, Data, Status, Light);
        }
    }

    static class Renderer
    {
        internal static string Value(QuotaWindow w) { return w == null ? "--" : w.Display(DateTimeOffset.Now); }
        internal static string Countdown(QuotaWindow window, DateTimeOffset now)
        {
            if (window == null || window.Reset <= 0) return "--";
            long seconds = window.Reset - now.ToUnixTimeSeconds();
            if (seconds <= 0) return "0min";
            long minutes = Math.Max(1, seconds / 60);
            if (minutes >= 1440) return (minutes / 1440) + "d" + ((minutes % 1440) / 60) + "h";
            if (minutes >= 60) return (minutes / 60) + "h" + (minutes % 60).ToString("00", CultureInfo.InvariantCulture) + "min";
            return minutes + "min";
        }
        static Color Ink(bool light) { return light ? Color.FromArgb(35, 43, 49) : Color.FromArgb(229, 232, 235); }
        static Color Dim(bool light) { return light ? Color.FromArgb(95, 105, 112) : Color.FromArgb(151, 158, 164); }
        internal static void Paint(Graphics g, float width, float height, float scale, Snapshot data, string status, bool light)
        {
            g.Clear(light ? Color.FromArgb(248, 248, 248) : Color.FromArgb(32, 32, 32));
            g.ScaleTransform(scale, scale); g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var font = new Font("Microsoft YaHei UI", 12, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var number = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip })
            {
                float baseline = height / 2 + 8;
                float center = baseline - 5;
                const float gap = 12, padding = 8, separatorWidth = 1, dotWidth = 4;
                DateTimeOffset now = DateTimeOffset.Now;
                var windows = data == null ? new QuotaWindow[] { null, null } : data.Windows.ToArray();
                var labels = windows.Select((w, i) => Countdown(w, now) + "/" + (w == null ? (i == 0 ? "5h" : "7d") : Snapshot.PeriodLabel(w.Minutes))).ToArray();
                var values = windows.Select(w => w == null ? "--" : w.Display(now)).ToArray();
                var labelWidths = labels.Select(t => TextWidth(g, t, font, sf)).ToArray();
                var valueWidths = values.Select(t => TextWidth(g, t, number, sf)).ToArray();
                bool expired = windows.Any(w => w != null && w.Expired(now));
                string statusText = data == null ? (string.IsNullOrEmpty(status) ? "等待更新" : status) : expired ? "重置待刷新" : "已更新 " + data.At.LocalDateTime.ToString("HH:mm");
                if (data != null && data.PlanName.Length > 0) statusText = data.PlanName + " · " + statusText;
                if (data != null && data.CreditsOnly) {
                    string credits = data.Unlimited ? "积分 无限" : data.Balance == null ? "积分 可用" : "积分 余额 " + data.Balance;
                    using (var brush = new SolidBrush(Ink(light))) DrawText(g, credits, font, brush, padding, baseline, sf);
                    float statusX = padding + TextWidth(g, credits, font, sf) + gap;
                    if (statusX + dotWidth + gap + TextWidth(g, statusText, font, sf) + padding <= width) {
                        using (var brush = new SolidBrush(Color.FromArgb(70, 189, 150))) g.FillEllipse(brush, statusX, center - 2, dotWidth, dotWidth);
                        using (var brush = new SolidBrush(Dim(light))) DrawText(g, statusText, font, brush, statusX + dotWidth + gap, baseline, sf);
                    }
                    return;
                }
                if (windows.Length == 0) return;
                float textWidth = labelWidths.Sum() + valueWidths.Sum() + separatorWidth * (windows.Length - 1) + (2 * windows.Length + 2 * (windows.Length - 1)) * gap + 2 * padding;
                if (data == null && !string.IsNullOrEmpty(status)) {
                    string shortened = statusText;
                    float available = Math.Max(0, width - textWidth - dotWidth - 2 * gap);
                    while (shortened.Length > 1 && TextWidth(g, shortened + "…", font, sf) > available) shortened = shortened.Substring(0, shortened.Length - 1);
                    if (shortened != statusText) statusText = shortened + "…";
                }
                float statusWidth = dotWidth + 2 * gap + TextWidth(g, statusText, font, sf);
                bool showStatus = width >= textWidth + statusWidth + (data == null && !string.IsNullOrEmpty(status) ? 0 : 24 * windows.Length);
                float barWidth = Math.Max(0, (width - textWidth - (showStatus ? statusWidth : 0)) / windows.Length);
                float x = padding;
                for (int i = 0; i < windows.Length; i++)
                {
                    using (var brush = new SolidBrush(Dim(light))) DrawText(g, labels[i], font, brush, x, baseline, sf);
                    x += labelWidths[i] + gap;
                    DrawProgress(g, x, center, barWidth, windows[i], now, light);
                    x += barWidth + gap;
                    using (var brush = new SolidBrush(Ink(light))) DrawText(g, values[i], number, brush, x, baseline, sf);
                    x += valueWidths[i];
                    if (i < windows.Length - 1) {
                        x += gap;
                        using (var pen = new Pen(light ? Color.FromArgb(217, 222, 225) : Color.FromArgb(59, 64, 67))) g.DrawLine(pen, x, center - 8, x, center + 8);
                        x += separatorWidth + gap;
                    }
                }
                if (showStatus)
                {
                    x += gap;
                    using (var brush = new SolidBrush(data == null || expired ? Color.FromArgb(217, 167, 74) : Color.FromArgb(70, 189, 150))) g.FillEllipse(brush, x, center - 2, dotWidth, dotWidth);
                    x += dotWidth + gap;
                    using (var brush = new SolidBrush(Dim(light))) DrawText(g, statusText, font, brush, x, baseline, sf);
                }
            }
        }
        static float TextWidth(Graphics g, string text, Font font, StringFormat sf) { return (float)Math.Ceiling(g.MeasureString(text, font, new SizeF(10000, 100), sf).Width); }
        static void DrawText(Graphics g, string text, Font font, Brush brush, float x, float baseline, StringFormat sf)
        {
            float ascent = font.Size * font.FontFamily.GetCellAscent(font.Style) / font.FontFamily.GetEmHeight(font.Style);
            g.DrawString(text, font, brush, new PointF(x, baseline - ascent), sf);
        }
        static void DrawProgress(Graphics g, float x, float center, float width, QuotaWindow value, DateTimeOffset now, bool light)
        {
            if (width <= 0) return;
            using (var pen = new Pen(light ? Color.FromArgb(219, 224, 227) : Color.FromArgb(64, 70, 73), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, x, center, x + width, center);
            if (value == null || value.Expired(now) || value.Remaining <= 0) return;
            Color color = value.Remaining <= 10 ? Color.FromArgb(234, 109, 109) : value.Remaining <= 30 ? Color.FromArgb(225, 176, 77) : Color.FromArgb(76, 193, 155);
            using (var pen = new Pen(color, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, x, center, x + width * (float)value.Remaining / 100, center);
        }
        internal static Icon MakeIcon()
        {
            using (var b = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(b))
                using (var p = new Pen(Color.FromArgb(76, 193, 155), 5)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); g.DrawArc(p, 5, 5, 22, 22, -90, 290); }
                IntPtr h = b.GetHicon();
                try { using (var i = Icon.FromHandle(h)) return (Icon)i.Clone(); } finally { Native.DestroyIcon(h); }
            }
        }
        internal static Bitmap Preview()
        {
            var bitmap = new Bitmap(1200, 144);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(238, 240, 242));
                foreach (bool light in new[] { false, true })
                    using (var strip = new Bitmap(1200, 56))
                    {
                        long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                        using (var sg = Graphics.FromImage(strip)) Paint(sg, 600, 28, 2, new Snapshot {
                            Windows = { new QuotaWindow { Minutes = 300, Used = light ? 75 : 51, Reset = now + 7080 + 50 },
                            new QuotaWindow { Minutes = 10080, Used = light ? 94 : 52, Reset = now + 4 * 86400 + 3 * 3600 + 50 } } }, "", light);
                        g.DrawImageUnscaled(strip, 0, light ? 88 : 0);
                    }
            }
            return bitmap;
        }
    }

    static class Native
    {
        internal delegate bool EnumProc(IntPtr window, IntPtr param);
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct Point2 { internal int X, Y; }
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] internal static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr hwnd, ref Point2 point);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder name, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point2 point);
        [DllImport("user32.dll")] internal static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);
        [DllImport("user32.dll")] internal static extern IntPtr GetThreadDesktop(uint id);
        [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] internal static extern IntPtr GetProcessWindowStation();
        [DllImport("user32.dll")] internal static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll")] internal static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int length, out uint needed);
        static string ObjectName(IntPtr handle) { var text = new StringBuilder(256); uint needed; return GetUserObjectInformation(handle, 2, text, text.Capacity * 2, out needed) ? text.ToString() : "unavailable"; }
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] internal static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr handle);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
        internal static bool IsCloaked(IntPtr hwnd) { int value; return DwmGetWindowAttribute(hwnd, 14, out value, 4) == 0 && value != 0; }
        internal static List<IntPtr> FindTargets(out bool running)
        {
            var ids = new HashSet<int>();
            foreach (string name in new[] { "ChatGPT", "Codex" })
                foreach (var p in Process.GetProcessesByName(name))
                    using (p) try
                    {
                        string path = p.MainModule.FileName;
                        bool openAi = path.IndexOf("OpenAI", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool desktop = name == "ChatGPT" || Directory.Exists(Path.Combine(Path.GetDirectoryName(path), "resources"));
                        if (openAi && desktop) ids.Add(p.Id);
                    } catch { }
            running = ids.Count > 0;
            var windows = new List<IntPtr>();
            EnumWindows(delegate(IntPtr h, IntPtr ignored) {
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (!ids.Contains((int)pid)) return true;
                var c = new StringBuilder(128); GetClassName(h, c, c.Capacity);
                if (c.ToString().StartsWith("Chrome_WidgetWin", StringComparison.Ordinal) && IsWindowVisible(h) && !IsCloaked(h)) windows.Add(h);
                return true;
            }, IntPtr.Zero);
            return windows;
        }
        internal static Rectangle Layout(Rectangle host, float scale)
        {
            int left = (int)Math.Ceiling(320 * scale), right = (int)Math.Ceiling(152 * scale);
            int free = host.Width - left - right;
            if (free < 360 * scale) return Rectangle.Empty;
            int width = Math.Min(free, (int)(600 * scale));
            return new Rectangle(host.Left + left, host.Top + (int)(2 * scale), width, (int)(28 * scale));
        }
        internal static bool TryPlacement(IntPtr host, out Rectangle bounds, out float scale)
        {
            bounds = Rectangle.Empty; scale = 1;
            Rect rect;
            if (!GetClientRect(host, out rect)) return false;
            uint dpi = GetDpiForWindow(host); scale = dpi == 0 ? 1 : dpi / 96f;
            bounds = Layout(new Rectangle(0, 0, rect.Right, rect.Bottom), scale);
            return !bounds.IsEmpty;
        }
        internal static Rectangle ScreenBounds(IntPtr window) { Rect r; GetWindowRect(window, out r); return new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top); }
        internal static object Diagnose()
        {
            bool running; var windows = FindTargets(out running);
            CodexExecutable executable = null; bool rejected = false;
            try { executable = QuotaClient.FindCodex(); } catch (UntrustedPathException) { rejected = true; } catch { }
            return new { desktopRunning = running, hostWindowFound = windows.Count > 0,
                codexExecutableFound = executable != null, codexTrustTier = executable == null ? "" : executable.Tier,
                pathRejected = rejected };
        }

    }

    static class Tests
    {
        static int count;
        static void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); count++; }
        internal static void Run()
        {
            var s = Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""primary"":{""windowDurationMins"":300,""usedPercent"":0},""secondary"":{""windowDurationMins"":10080,""usedPercent"":20}}}"));
            Check(s.Five.Remaining == 100 && s.Week.Remaining == 80, "remaining percentages");
            s = Snapshot.Parse(Json.Decode(@"{""rateLimitsByLimitId"":{""other"":{""primary"":{""windowDurationMins"":300,""usedPercent"":20}}},""rateLimits"":{""primary"":{""windowDurationMins"":300,""usedPercent"":25}}}"));
            Check(s.Five == null && s.Week == null, "never mix quota buckets");
            s = Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""primary"":{""windowDurationMins"":10080,""usedPercent"":110},""secondary"":null}}"));
            Check(s.Five == null && s.Week.Remaining == 0, "duration-based mapping and clamp");
            s = Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""primary"":{""windowDurationMins"":300,""usedPercent"":-2,""resetsAt"":1}}}"));
            Check(s.Five.Remaining == 100 && s.Five.Display(DateTimeOffset.Now) == "--", "expired values are unavailable");
            s = Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""primary"":{""windowDurationMins"":300,""usedPercent"":null}}}"));
            Check(s.Five == null, "null is unavailable not zero");
            var countdownNow = DateTimeOffset.FromUnixTimeSeconds(2000000000);
            long countdownEpoch = countdownNow.ToUnixTimeSeconds();
            Check(Renderer.Countdown(new QuotaWindow { Reset = countdownEpoch + 7080 }, countdownNow) == "1h58min", "hour and minute countdown");
            Check(Renderer.Countdown(new QuotaWindow { Reset = countdownEpoch + 4 * 86400 + 3 * 3600 }, countdownNow) == "4d3h", "day and hour countdown");
            Check(Renderer.Countdown(new QuotaWindow { Reset = countdownEpoch + 59 * 60 }, countdownNow) == "59min", "sub-hour countdown");
            Check(Renderer.Countdown(new QuotaWindow { Reset = countdownEpoch + 3600 }, countdownNow) == "1h00min", "exact hour countdown");
            Check(Renderer.Countdown(new QuotaWindow { Reset = countdownEpoch + 1 }, countdownNow) == "1min", "positive sub-minute countdown");
            Check(Renderer.Countdown(new QuotaWindow { Reset = countdownEpoch }, countdownNow) == "0min", "reset boundary");
            Check(Renderer.Countdown(new QuotaWindow { Reset = countdownEpoch - 1 }, countdownNow) == "0min", "expired countdown never negative");
            Check(Renderer.Countdown(null, countdownNow) == "--" && Renderer.Countdown(new QuotaWindow(), countdownNow) == "--", "unknown reset is unavailable");
            foreach (float dpi in new[] { 1f, 1.25f, 1.5f, 2f })
            {
                var host = new Rectangle(-1920, 100, (int)(1200 * dpi), 800);
                Rectangle b = Native.Layout(host, dpi);
                Check(b.Left >= host.Left + 320 * dpi && b.Right <= host.Right - 152 * dpi && b.Height == (int)(28 * dpi), "menu safe area and DPI");
                Check(b.Left == host.Left + (int)Math.Ceiling(320 * dpi)
                    && Native.Layout(new Rectangle(host.Left, host.Top, (int)(1400 * dpi), 800), dpi).Left == b.Left, "fixed anchor beside menu during resize");
                Check(Native.Layout(new Rectangle(0, 0, (int)(680 * dpi), 600), dpi).IsEmpty, "narrow window hides");
            }
            const string plusFixture = @"{""rateLimits"":{""planType"":""plus"",""primary"":{""windowDurationMins"":300,""usedPercent"":10},""secondary"":{""windowDurationMins"":10080,""usedPercent"":40}}}";
            const string proFixture = @"{""rateLimits"":{""planType"":""pro"",""primary"":{""windowDurationMins"":10080,""usedPercent"":25},""secondary"":null}}";
            const string reversedFixture = @"{""rateLimits"":{""primary"":{""windowDurationMins"":10080,""usedPercent"":10},""secondary"":{""windowDurationMins"":300,""usedPercent"":20}}}";
            const string unknownFixture = @"{""rateLimits"":{""planType"":""go"",""primary"":{""windowDurationMins"":1440,""usedPercent"":5}}}";
            const string unlimitedFixture = @"{""rateLimits"":{""planType"":""pro"",""primary"":null,""secondary"":null,""credits"":{""unlimited"":true}}}";
            const string creditsFixture = @"{""rateLimits"":{""credits"":{""hasCredits"":true,""balance"":""123.50""}}}";
            var plus = Snapshot.Parse(Json.Decode(plusFixture));
            var pro = Snapshot.Parse(Json.Decode(proFixture));
            var reverse = Snapshot.Parse(Json.Decode(reversedFixture));
            var go = Snapshot.Parse(Json.Decode(unknownFixture));
            var unlimited = Snapshot.Parse(Json.Decode(unlimitedFixture));
            var credits = Snapshot.Parse(Json.Decode(creditsFixture));
            Check(plus.Windows.Count == 2 && plus.Five.Remaining == 90 && plus.Week.Remaining == 60, "plus two windows");
            Check(plus.PlanName == "Plus", "plus plan");
            Check(pro.Windows.Count == 1 && pro.Five == null && pro.Week.Remaining == 75, "pro weekly primary only");
            Check(pro.PlanName == "Pro" && Snapshot.PeriodLabel(pro.Week.Minutes) == "7d", "pro weekly label");
            Check(reverse.Windows[0].Minutes == 300 && reverse.Windows[1].Minutes == 10080, "reverse order sorted");
            Check(go.Windows.Count == 1 && Snapshot.PeriodLabel(go.Windows[0].Minutes) == "1d" && go.PlanName == "Go", "go dynamic daily window");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""planType"":""unknown""}}")).PlanName == "", "unknown plan hidden");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""planType"":""P R O""}}")).Plan == "", "plan whitespace rejected");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""planType"":""pro!""}}")).Plan == "", "plan punctuation rejected");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""planType"":""abcdefghijklmnopqrstuvwxy""}}")).Plan == "", "long plan rejected");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""planType"":""PRO""}}")).PlanName == "Pro", "plan normalized");
            Check(unlimited.CreditsOnly && unlimited.Unlimited && !unlimited.Empty, "unlimited credits state");
            Check(credits.CreditsOnly && credits.Balance == "123.50", "string balance");
            Check(Json.Encode(credits.Export()).Contains("123.50"), "sanitized credit probe export");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""credits"":{""hasCredits"":true,""balance"":12.5}}}")).Balance == "12.5", "numeric balance");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""credits"":{""hasCredits"":true,""balance"":""1e10""}}}")).Balance == null, "invalid balance hidden");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""credits"":{""hasCredits"":true,""balance"":""1234567890123""}}}")).Balance == null, "long balance hidden");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{}}")).Empty, "empty snapshot");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimitsByLimitId"":null,""rateLimits"":{""primary"":{""windowDurationMins"":300,""usedPercent"":1}}}" )).Empty, "present null multi bucket never falls back");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimitsByLimitId"":{""other"":{""credits"":{""unlimited"":true}}}}" )).Empty, "no cross-bucket credits");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""primary"":{""windowDurationMins"":300,""usedPercent"":10},""secondary"":{""windowDurationMins"":300,""usedPercent"":80}}}" )).Windows.Single().Used == 10, "duplicate duration keeps first");
            Check(Snapshot.Parse(Json.Decode(@"{""rateLimits"":{""primary"":{""windowDurationMins"":0,""usedPercent"":5}}}" )).Empty, "invalid duration rejected");
            Check(Snapshot.PeriodLabel(300) == "5h", "5h label");
            Check(Snapshot.PeriodLabel(10080) == "7d", "7d label");
            Check(Snapshot.PeriodLabel(1440) == "1d", "day label");
            Check(Snapshot.PeriodLabel(120) == "2h", "hour label");
            Check(Snapshot.PeriodLabel(45) == "45min", "minute label");
            Check(Snapshot.PeriodLabel(0) == "", "invalid period label");
            Check(BarContext.LimitTrayText(new string('x', 200)).Length == 127, "tray text limit");
            Check(BarContext.LimitTrayText("short") == "short", "short tray text unchanged");
            var renderStates = new Snapshot[] { null, pro, plus, credits };
            foreach (var sample in renderStates) {
                using (var bitmap = new Bitmap(600, 28)) using (var graphics = Graphics.FromImage(bitmap))
                    Renderer.Paint(graphics, 600, 28, 1, sample, sample == null ? "请重新登录" : "", false);
                Check(true, "render state bitmap");
            }
            Program.SaveBeside("test-results.json", new { passed = count, failed = 0, at = DateTimeOffset.Now.ToString("o") });
        }
        internal static bool RunUi()
        {
            // Exercise placement against our own test window, without operating the user's GPT window.
            var report = new List<object>();
            bool passed = false;
            using (var host = new Form { Text = "GPT Quota Bar - layout test", StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(120, 120, 1300, 700), BackColor = Color.FromArgb(24, 24, 24) })
            using (var bar = new BarForm())
            using (var tick = new System.Windows.Forms.Timer { Interval = 500 })
            {
                int step = 0;
                host.Shown += delegate { tick.Start(); };
                tick.Tick += delegate {
                    try
                    {
                        Rectangle bounds; float scale;
                        if (step == 0)
                        {
                            Check(Native.TryPlacement(host.Handle, out bounds, out scale), "normal host placement");
                            Check(bar.AttachTo(host.Handle), "attach child window");
                            bar.Present(bounds, scale);
                            bar.Data = new Snapshot { Windows = { new QuotaWindow { Minutes = 300, Used = 6 }, new QuotaWindow { Minutes = 10080, Used = 21 } } };
                            report.Add(new { step = "normal", bounds = bounds.ToString(), scale = scale });
                            using (var b = new Bitmap(bar.Width, bar.Height)) { bar.DrawToBitmap(b, bar.ClientRectangle); b.Save(Path.Combine(Program.Folder, "ui-test-preview.png")); }
                        }
                        if (step == 1)
                        {
                            var old = Native.ScreenBounds(bar.Handle); host.Left += 80; host.Top += 60;
                            Native.TryPlacement(host.Handle, out bounds, out scale);
                            var moved = Native.ScreenBounds(bar.Handle);
                            Check(moved.X == old.X + 80 && moved.Y == old.Y + 60, "Windows moves child without a position refresh");
                            report.Add(new { step = "move-without-refresh", pass = true });
                        }
                        if (step == 2)
                        {
                            host.WindowState = FormWindowState.Minimized;
                            Check(Native.IsIconic(host.Handle), "minimized host detected");
                            bar.HidePassive(); Check(!Native.IsWindowVisible(bar.Handle), "hidden on minimize");
                            report.Add(new { step = "minimize", pass = true });
                        }
                        if (step == 3)
                        {
                            host.WindowState = FormWindowState.Normal; host.Width = 700;
                            Check(!Native.TryPlacement(host.Handle, out bounds, out scale), "narrow host avoids menus");
                            report.Add(new { step = "narrow", pass = true });
                            Program.SaveBeside("ui-test-results.json", new { passed = true, checks = report });
                            passed = true;
                            tick.Stop(); host.Close();
                        }
                        step++;
                    }
                    catch (Exception ex) {
                        Program.SaveBeside("ui-test-results.json", new { passed = false, step = step, error = ex.GetType().Name, checks = report });
                        tick.Stop(); host.Close();
                    }
                };
                Application.Run(host);
            }
            return passed;
        }
    }
}
