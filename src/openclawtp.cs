using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: AssemblyTitle("OpenClaw Native Tray")]
[assembly: AssemblyProduct("OpenClaw Native Tray")]
[assembly: AssemblyDescription("OpenClaw native Windows tray for local runtimes")]
[assembly: AssemblyCompany("OpenClaw Community")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class NativeMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern bool DestroyIcon(IntPtr handle);
}

internal static class DebugLog
{
    internal static void Write(string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "openclawtp.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }
}

internal sealed class TrayRuntimeConfig
{
    public string RuntimeRoot;
    public string OpenClawRoot;
    public int GatewayPort;
    public string StartupTaskName;
    public string ControlPanelPath;

    public TrayRuntimeConfig()
    {
        GatewayPort = 18789;
        StartupTaskName = "OpenClaw Native Tray";
        ControlPanelPath = "/openclaw/";
    }
}

internal sealed class RuntimePaths
{
    public string AppDir;
    public string RuntimeRoot;
    public string OpenClawRoot;
    public string ScriptsDir;
    public string StateDir;
    public string LogsDir;
    public string EnvScript;
    public string StartupScript;
    public string StopScript;
    public string RestartScript;
    public string StatusScript;
    public string StartupRegistrationScript;
    public string GatewayMetaPath;
    public int GatewayPort;
    public string StartupTaskName;
    public string ControlPanelPath;

    public RuntimePaths(string appDir, string runtimeRoot, string openClawRoot, int gatewayPort, string startupTaskName, string controlPanelPath)
    {
        AppDir = appDir;
        RuntimeRoot = runtimeRoot;
        OpenClawRoot = openClawRoot;
        ScriptsDir = Path.Combine(runtimeRoot, "scripts");
        StateDir = Path.Combine(runtimeRoot, "state");
        LogsDir = Path.Combine(StateDir, "logs");
        EnvScript = Path.Combine(runtimeRoot, "env", "lobster-teams.local.ps1");
        StartupScript = Path.Combine(ScriptsDir, "start-lobster-teams-background.ps1");
        StopScript = Path.Combine(ScriptsDir, "stop-lobster-teams.ps1");
        RestartScript = Path.Combine(ScriptsDir, "restart-lobster-teams.ps1");
        StatusScript = Path.Combine(ScriptsDir, "status-lobster-teams.ps1");
        StartupRegistrationScript = Path.Combine(ScriptsDir, "register-lobster-teams-startup.ps1");
        GatewayMetaPath = Path.Combine(StateDir, "gateway-process.json");
        GatewayPort = gatewayPort > 0 ? gatewayPort : 18789;
        StartupTaskName = string.IsNullOrWhiteSpace(startupTaskName) ? "OpenClaw Native Tray" : startupTaskName.Trim();
        ControlPanelPath = NormalizeControlPanelPath(controlPanelPath);
    }

    private static string NormalizeControlPanelPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "/openclaw/";
        string normalized = value.Trim().Replace('\\', '/');
        if (!normalized.StartsWith("/")) normalized = "/" + normalized;
        if (!normalized.EndsWith("/")) normalized += "/";
        return normalized;
    }
}

internal sealed class TrayState
{
    public bool Running;
    public bool WrapperRunning;
    public int WrapperPid;
    public bool PortOpen;
    public DateTime UpdatedAt;
    public string LogFile;
    public string StartedAt;
}

internal sealed class StartupTaskInfoSnapshot
{
    public bool Registered;
    public bool Enabled;
    public string State;
}

internal sealed class OpenClawTrayApp : ApplicationContext
{
    private readonly RuntimePaths paths;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem startItem;
    private readonly ToolStripMenuItem restartItem;
    private readonly ToolStripMenuItem stopItem;
    private readonly ToolStripMenuItem startupToggleItem;
    private readonly Timer timer;
    private readonly Icon baseIcon;
    private readonly Icon runningIcon;
    private readonly Icon startingIcon;
    private readonly Icon stoppedIcon;
    private TrayState lastState;
    private DateTime lastStartupCheck = DateTime.MinValue;
    private StartupTaskInfoSnapshot startupSnapshot;

    public OpenClawTrayApp(RuntimePaths paths)
    {
        DebugLog.Write("OpenClawTrayApp ctor");
        this.paths = paths;
        this.baseIcon = LoadBaseIcon();
        this.runningIcon = CreateStateIcon(this.baseIcon, false, Color.FromArgb(46, 204, 113), 18, 1.12f);
        this.startingIcon = CreateStateIcon(this.baseIcon, false, Color.FromArgb(241, 196, 15), 18, 1.12f);
        this.stoppedIcon = CreateStateIcon(this.baseIcon, true, Color.FromArgb(231, 76, 60), 18, 1.12f);

        ContextMenuStrip menu = new ContextMenuStrip();
        this.startItem = new ToolStripMenuItem("启动 OpenClaw", null, delegate { StartGateway(); });
        this.restartItem = new ToolStripMenuItem("重启 OpenClaw", null, delegate { RestartGateway(); });
        this.stopItem = new ToolStripMenuItem("停止 OpenClaw", null, delegate { StopGateway(); });
        this.startupToggleItem = new ToolStripMenuItem("开机启动 Tray + OpenClaw", null, delegate { ToggleStartupRegistration(); });
        this.startupToggleItem.CheckOnClick = false;
        menu.Items.Add(this.startItem);
        menu.Items.Add(this.restartItem);
        menu.Items.Add(this.stopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(this.startupToggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("查看状态", null, delegate { ShowStatusDialog(); }));
        menu.Items.Add(new ToolStripMenuItem("打开控制台", null, delegate { OpenConsoleWindow(); }));
        menu.Items.Add(new ToolStripMenuItem("打开控制面板", null, delegate { OpenControlPanel(); }));
        menu.Items.Add(new ToolStripMenuItem("打开日志目录", null, delegate { OpenLogDirectory(); }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出托盘", null, delegate { ExitTray(false); }));
        menu.Items.Add(new ToolStripMenuItem("退出并停止 OpenClaw", null, delegate { ExitTray(true); }));

        this.notifyIcon = new NotifyIcon();
        this.notifyIcon.Icon = this.stoppedIcon;
        this.notifyIcon.Visible = true;
        this.notifyIcon.Text = "OpenClaw Stopped";
        this.notifyIcon.ContextMenuStrip = menu;
        this.notifyIcon.DoubleClick += delegate { OpenControlPanel(); };

        this.timer = new Timer();
        this.timer.Interval = 5000;
        this.timer.Tick += delegate { try { RefreshState(); } catch { } };
        this.timer.Start();

        RefreshState();
        DebugLog.Write("state refreshed");
        EnsureGatewayRunning(true);
        DebugLog.Write("ensure gateway running ok");
        ShowBalloon("OpenClaw 通知", "托盘已启动，Gateway 已准备就绪。", ToolTipIcon.Info);
        DebugLog.Write("balloon shown");
    }

    private Icon LoadBaseIcon()
    {
        string[] candidates = new string[]
        {
            Path.Combine(AppContext.BaseDirectory, "openclawtp.ico"),
            string.IsNullOrWhiteSpace(this.paths.OpenClawRoot) ? string.Empty : Path.Combine(this.paths.OpenClawRoot, "dist", "control-ui", "favicon.ico"),
            string.IsNullOrWhiteSpace(this.paths.OpenClawRoot) ? string.Empty : Path.Combine(this.paths.OpenClawRoot, "assets", "icons", "favicon.ico")
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(candidates[i]) && File.Exists(candidates[i])) return new Icon(candidates[i]);
            }
            catch { }
        }
        return SystemIcons.Application;
    }

    private static Icon CreateStateIcon(Icon icon, bool grayscale, Color badgeColor, int badgeSize, float baseScale)
    {
        Bitmap bitmap = new Bitmap(32, 32);
        Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Bitmap iconBitmap = icon.ToBitmap();
        int scaled = (int)Math.Round(32 * baseScale);
        int offset = (int)Math.Floor((32 - scaled) / 2.0);
        graphics.DrawImage(iconBitmap, new Rectangle(offset, offset, scaled, scaled));

        if (grayscale)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.A == 0) continue;
                    int gray = (int)Math.Round((pixel.R * 0.299) + (pixel.G * 0.587) + (pixel.B * 0.114));
                    gray = Math.Max(60, Math.Min(225, gray));
                    bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, gray, gray, gray));
                }
            }
        }

        if (badgeSize > 0)
        {
            Rectangle rect = new Rectangle(32 - badgeSize - 1, 32 - badgeSize - 1, badgeSize, badgeSize);
            Brush brush = new SolidBrush(badgeColor);
            Pen pen = new Pen(Color.White, 2.4f);
            graphics.FillEllipse(brush, rect);
            graphics.DrawEllipse(pen, rect);
            pen.Dispose();
            brush.Dispose();
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
            iconBitmap.Dispose();
            graphics.Dispose();
            bitmap.Dispose();
        }
    }

    private void RefreshState()
    {
        this.lastState = ReadState();
        ApplyVisualState(this.lastState.Running ? "running" : "stopped");
        this.startItem.Enabled = !this.lastState.Running;
        this.restartItem.Enabled = this.lastState.Running;
        this.stopItem.Enabled = this.lastState.Running;
        UpdateStartupToggleState();
    }

    private TrayState ReadState()
    {
        TrayState state = new TrayState();
        state.WrapperPid = ReadJsonInt(this.paths.GatewayMetaPath, "pid");
        state.StartedAt = ReadJsonString(this.paths.GatewayMetaPath, "startedAt");
        state.LogFile = ReadJsonString(this.paths.GatewayMetaPath, "logFile");
        if (state.WrapperPid > 0)
        {
            try
            {
                Process.GetProcessById(state.WrapperPid);
                state.WrapperRunning = true;
            }
            catch
            {
                state.WrapperRunning = false;
            }
        }
        state.PortOpen = TestPort(this.paths.GatewayPort, 250);
        state.Running = state.WrapperRunning || state.PortOpen;
        state.UpdatedAt = DateTime.Now;
        return state;
    }

    private StartupTaskInfoSnapshot GetStartupTaskSnapshot()
    {
        if (this.startupSnapshot != null && (DateTime.Now - this.lastStartupCheck).TotalSeconds < 30) return this.startupSnapshot;
        this.lastStartupCheck = DateTime.Now;
        StartupTaskInfoSnapshot info = new StartupTaskInfoSnapshot();
        info.Registered = false;
        info.Enabled = false;
        info.State = "missing";
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "schtasks.exe";
            psi.Arguments = "/Query /TN \"" + this.paths.StartupTaskName + "\" /FO LIST";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            using (Process process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    info.Registered = true;
                    Match m = Regex.Match(output, @"Status:\s*(.+)");
                    info.State = m.Success ? m.Groups[1].Value.Trim() : "Unknown";
                    info.Enabled = !info.State.Contains("Disabled");
                }
            }
        }
        catch { }
        this.startupSnapshot = info;
        return info;
    }

    private void UpdateStartupToggleState()
    {
        StartupTaskInfoSnapshot startup = GetStartupTaskSnapshot();
        bool enabled = startup.Registered && startup.Enabled;
        this.startupToggleItem.Checked = enabled;
        this.startupToggleItem.Text = "开机启动（Tray + OpenClaw）";
    }

    private void ToggleStartupRegistration()
    {
        StartupTaskInfoSnapshot startup = GetStartupTaskSnapshot();
        ConfigureStartupRegistration(!(startup.Registered && startup.Enabled));
    }

    private void ConfigureStartupRegistration(bool enable)
    {
        try
        {
            string extraArgs = "-DelaySeconds 30 -TaskName \"" + this.paths.StartupTaskName.Replace("\"", "\\\"") + "\"";
            if (!enable) extraArgs += " -Remove";
            ExecuteScript(this.paths.StartupRegistrationScript, extraArgs, true);
            this.startupSnapshot = null;
            this.lastStartupCheck = DateTime.MinValue;
            RefreshState();
            ShowBalloon("OpenClaw 通知", enable ? "已启用开机启动。登录 Windows 后会自动启动 Tray 和 OpenClaw。" : "已关闭开机启动。", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            this.startupSnapshot = null;
            this.lastStartupCheck = DateTime.MinValue;
            RefreshState();
            MessageBox.Show(ex.Message, "OpenClaw", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyVisualState(string state)
    {
        if (state == "starting")
        {
            this.notifyIcon.Icon = this.startingIcon;
            this.notifyIcon.Text = "OpenClaw Starting";
        }
        else if (state == "running")
        {
            this.notifyIcon.Icon = this.runningIcon;
            this.notifyIcon.Text = this.lastState != null && this.lastState.WrapperPid > 0 ? "OpenClaw Running PID " + this.lastState.WrapperPid : "OpenClaw Running";
        }
        else
        {
            this.notifyIcon.Icon = this.stoppedIcon;
            this.notifyIcon.Text = "OpenClaw Stopped";
        }
    }

    private void ShowStatusDialog()
    {
        RefreshState();
        StartupTaskInfoSnapshot startup = GetStartupTaskSnapshot();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("OpenClaw Native Tray");
        sb.AppendLine();
        sb.AppendLine("运行状态: " + (this.lastState.Running ? "运行中" : "已停止"));
        if (this.lastState.WrapperPid > 0) sb.AppendLine("托管进程 PID: " + this.lastState.WrapperPid);
        sb.AppendLine("网关端口: 127.0.0.1:" + this.paths.GatewayPort);
        sb.AppendLine("端口探活: " + (this.lastState.PortOpen ? "正常" : "未监听"));
        sb.AppendLine("运行时目录: " + this.paths.RuntimeRoot);
        if (!string.IsNullOrWhiteSpace(this.paths.OpenClawRoot)) sb.AppendLine("OpenClaw 程序目录: " + this.paths.OpenClawRoot);
        if (!string.IsNullOrWhiteSpace(this.lastState.LogFile)) sb.AppendLine("日志文件: " + this.lastState.LogFile);
        if (!string.IsNullOrWhiteSpace(this.lastState.StartedAt)) sb.AppendLine("启动时间: " + this.lastState.StartedAt);
        sb.AppendLine("上次刷新: " + this.lastState.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();
        sb.AppendLine("开机自启: " + (startup.Registered ? "已注册" : "未注册"));
        if (startup.Registered)
        {
            sb.AppendLine("自启任务名称: " + this.paths.StartupTaskName);
            sb.AppendLine("自启任务状态: " + startup.State);
            sb.AppendLine("自启任务启用: " + (startup.Enabled ? "是" : "否"));
            sb.AppendLine("启动作用: 启动托盘时会自动拉起 OpenClaw 服务");
        }
        sb.AppendLine();
        sb.AppendLine("说明: 当前通知和托盘由原生程序托管，可通过 openclawtp.runtime.json 覆盖路径和端口等配置。");
        MessageBox.Show(sb.ToString(), "OpenClaw 状态", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void StartGateway()
    {
        ExecuteGatewayAction(this.paths.StartupScript, true, "Gateway 已启动。");
    }

    private void StopGateway()
    {
        ExecuteGatewayAction(this.paths.StopScript, false, "Gateway 已停止。");
    }

    private void RestartGateway()
    {
        ExecuteGatewayAction(this.paths.RestartScript, true, "Gateway 已重启。");
    }

    private void EnsureGatewayRunning(bool silent)
    {
        RefreshState();
        if (this.lastState.Running) return;
        ExecuteScript(this.paths.StartupScript, true);
        WaitForGateway(true, 15000);
        RefreshState();
        if (!this.lastState.Running) throw new InvalidOperationException("Gateway 启动后仍未进入运行状态。");
        if (!silent) ShowBalloon("OpenClaw 通知", "Gateway 已启动。", ToolTipIcon.Info);
    }

    private void ExecuteGatewayAction(string scriptPath, bool expectRunning, string successText)
    {
        try
        {
            ApplyVisualState("starting");
            ExecuteScript(scriptPath, true);
            WaitForGateway(expectRunning, expectRunning ? 18000 : 12000);
            RefreshState();
            ShowBalloon("OpenClaw 通知", successText, ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            RefreshState();
            MessageBox.Show(ex.Message, "OpenClaw", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void WaitForGateway(bool desiredRunning, int timeoutMs)
    {
        DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < deadline)
        {
            RefreshState();
            if (this.lastState.Running == desiredRunning) return;
            Application.DoEvents();
            System.Threading.Thread.Sleep(400);
        }
    }

    private void OpenConsoleWindow()
    {
        Process.Start(new ProcessStartInfo(FindPwsh(), "-NoExit -NoProfile -ExecutionPolicy Bypass -File \"" + this.paths.StatusScript + "\"")
        {
            WorkingDirectory = this.paths.ScriptsDir,
            UseShellExecute = true
        });
    }

    private void OpenControlPanel()
    {
        string url = "http://127.0.0.1:" + this.paths.GatewayPort + this.paths.ControlPanelPath;
        string token = ReadGatewayToken();
        if (!string.IsNullOrWhiteSpace(token)) url += "#token=" + Uri.EscapeDataString(token);
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void OpenLogDirectory()
    {
        Directory.CreateDirectory(this.paths.LogsDir);
        Process.Start(new ProcessStartInfo(this.paths.LogsDir) { UseShellExecute = true });
    }

    private void ExitTray(bool stopGateway)
    {
        if (stopGateway)
        {
            try { ExecuteScript(this.paths.StopScript, true); } catch { }
        }
        this.timer.Stop();
        this.notifyIcon.Visible = false;
        this.notifyIcon.Dispose();
        ExitThread();
    }

    private void ExecuteScript(string scriptPath, bool throwOnError)
    {
        ExecuteScript(scriptPath, string.Empty, throwOnError);
    }

    private void ExecuteScript(string scriptPath, string extraArguments, bool throwOnError)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = FindPwsh();
        psi.Arguments = BuildPowerShellFileArguments(scriptPath, extraArguments);
        psi.WorkingDirectory = this.paths.ScriptsDir;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;
        using (Process process = Process.Start(psi))
        {
            string output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (throwOnError && process.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(output) ? "脚本执行失败。" : output.Trim());
            }
        }
    }

    private static string BuildPowerShellFileArguments(string scriptPath, string extraArguments)
    {
        string args = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"";
        if (!string.IsNullOrWhiteSpace(extraArguments)) args += " " + extraArguments.Trim();
        return args;
    }

    private string ReadGatewayToken()
    {
        if (!File.Exists(this.paths.EnvScript)) return string.Empty;
        string[] lines = File.ReadAllLines(this.paths.EnvScript, Encoding.UTF8);
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("OPENCLAW_GATEWAY_TOKEN")) continue;
            int idx = lines[i].IndexOf('=');
            if (idx < 0) continue;
            return lines[i].Substring(idx + 1).Trim().Trim('"', '\'', ' ');
        }
        return string.Empty;
    }

    private static bool TestPort(int port, int timeoutMs)
    {
        TcpClient client = new TcpClient();
        try
        {
            IAsyncResult result = client.BeginConnect("127.0.0.1", port, null, null);
            bool ok = result.AsyncWaitHandle.WaitOne(timeoutMs, false);
            if (!ok) return false;
            client.EndConnect(result);
            return true;
        }
        catch { return false; }
        finally { client.Close(); }
    }

    private static int ReadJsonInt(string path, string key)
    {
        if (!File.Exists(path)) return 0;
        string text = File.ReadAllText(path, Encoding.UTF8);
        Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    private static string ReadJsonString(string path, string key)
    {
        if (!File.Exists(path)) return string.Empty;
        string text = File.ReadAllText(path, Encoding.UTF8);
        Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static string FindPwsh()
    {
        string[] candidates = new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PowerShell", "7", "pwsh.exe"),
            "pwsh.exe"
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            bool hasSlash = candidates[i].Contains(Path.DirectorySeparatorChar.ToString()) || candidates[i].Contains(Path.AltDirectorySeparatorChar.ToString());
            if (hasSlash && File.Exists(candidates[i])) return candidates[i];
            if (!hasSlash) return candidates[i];
        }
        throw new InvalidOperationException("未找到 pwsh.exe，请先安装 PowerShell 7。");
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        this.notifyIcon.BalloonTipTitle = title;
        this.notifyIcon.BalloonTipText = text;
        this.notifyIcon.BalloonTipIcon = icon;
        this.notifyIcon.ShowBalloonTip(3000);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (this.timer != null) this.timer.Dispose();
            if (this.notifyIcon != null) this.notifyIcon.Dispose();
            if (!object.ReferenceEquals(this.baseIcon, SystemIcons.Application)) this.baseIcon.Dispose();
            if (this.runningIcon != null) this.runningIcon.Dispose();
            if (this.startingIcon != null) this.startingIcon.Dispose();
            if (this.stoppedIcon != null) this.stoppedIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal static class Program
{
    private const string DefaultRuntimeFolderName = "lobster-teams";
    private const string DefaultConfigFileName = "openclawtp.runtime.json";

    private static void AddCandidate(List<string> candidates, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        string normalized = value.Trim();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i], normalized, StringComparison.OrdinalIgnoreCase)) return;
        }
        candidates.Add(normalized);
    }

    private static string NormalizeCandidatePath(string baseDir, string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return string.Empty;
        string expanded = Environment.ExpandEnvironmentVariables(rawValue.Trim().Trim('"', '\''));
        try
        {
            if (Path.IsPathRooted(expanded)) return Path.GetFullPath(expanded);
            return Path.GetFullPath(Path.Combine(baseDir, expanded));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FindFirstValidPath(List<string> candidates, Func<string, bool> validator)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];
            if (validator(candidate)) return candidate;
        }
        return string.Empty;
    }

    private static bool IsValidRuntimeRoot(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && Directory.Exists(path)
            && File.Exists(Path.Combine(path, "scripts", "start-lobster-teams-background.ps1"));
    }

    private static bool IsValidOpenClawRoot(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && Directory.Exists(path)
            && File.Exists(Path.Combine(path, "openclaw.mjs"));
    }

    private static string ReadJsonString(string path, string key)
    {
        if (!File.Exists(path)) return string.Empty;
        string text = File.ReadAllText(path, Encoding.UTF8);
        Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static int ReadJsonInt(string path, string key)
    {
        if (!File.Exists(path)) return 0;
        string text = File.ReadAllText(path, Encoding.UTF8);
        Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    private static string ReadPsEnvAssignment(string scriptPath, string name)
    {
        if (!File.Exists(scriptPath)) return string.Empty;
        string text = File.ReadAllText(scriptPath, Encoding.UTF8);
        Match m = Regex.Match(text, "\\$env:" + Regex.Escape(name) + "\\s*=\\s*['\\\"]([^'\\\"]+)['\\\"]");
        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ResolveRuntimeRoot(string appDir, string configuredRuntimeRoot)
    {
        List<string> candidates = new List<string>();
        AddCandidate(candidates, configuredRuntimeRoot);
        AddCandidate(candidates, NormalizeCandidatePath(appDir, Environment.GetEnvironmentVariable("LOBSTER_RUNTIME_ROOT")));
        AddCandidate(candidates, NormalizeCandidatePath(appDir, Environment.GetEnvironmentVariable("OPENCLAW_RUNTIME_ROOT")));
        AddCandidate(candidates, NormalizeCandidatePath(appDir, Path.Combine(appDir, DefaultRuntimeFolderName)));
        AddCandidate(candidates, NormalizeCandidatePath(appDir, Path.Combine(appDir, "..", DefaultRuntimeFolderName)));
        DirectoryInfo appParent = Directory.GetParent(appDir);
        if (appParent != null)
        {
            AddCandidate(candidates, Path.GetFullPath(Path.Combine(appParent.FullName, DefaultRuntimeFolderName)));
        }
        return FindFirstValidPath(candidates, IsValidRuntimeRoot);
    }

    private static string ResolveOpenClawRoot(string appDir, string runtimeRoot, string configuredOpenClawRoot)
    {
        List<string> candidates = new List<string>();
        AddCandidate(candidates, configuredOpenClawRoot);
        AddCandidate(candidates, NormalizeCandidatePath(appDir, Environment.GetEnvironmentVariable("OPENCLAW_APP_ROOT")));
        AddCandidate(candidates, NormalizeCandidatePath(appDir, Environment.GetEnvironmentVariable("OPENCLAW_ROOT")));
        if (!string.IsNullOrWhiteSpace(runtimeRoot))
        {
            string envScript = Path.Combine(runtimeRoot, "env", "lobster-teams.local.ps1");
            AddCandidate(candidates, NormalizeCandidatePath(runtimeRoot, ReadPsEnvAssignment(envScript, "OPENCLAW_APP_ROOT")));
            DirectoryInfo runtimeParent = Directory.GetParent(runtimeRoot);
            if (runtimeParent != null)
            {
                AddCandidate(candidates, Path.Combine(runtimeParent.FullName, "openclaw"));
                AddCandidate(candidates, Path.Combine(runtimeParent.FullName, "OpenClaw"));
            }
        }
        DirectoryInfo appParent = Directory.GetParent(appDir);
        if (appParent != null)
        {
            AddCandidate(candidates, Path.Combine(appParent.FullName, "openclaw"));
            AddCandidate(candidates, Path.Combine(appParent.FullName, "OpenClaw"));
            DirectoryInfo grandParent = appParent.Parent;
            if (grandParent != null)
            {
                AddCandidate(candidates, Path.Combine(grandParent.FullName, "openclaw"));
                AddCandidate(candidates, Path.Combine(grandParent.FullName, "OpenClaw"));
            }
        }
        AddCandidate(candidates, NormalizeCandidatePath(appDir, Environment.CurrentDirectory));
        AddCandidate(candidates, NormalizeCandidatePath(appDir, "D:/Programs/openclaw"));
        AddCandidate(candidates, NormalizeCandidatePath(appDir, "C:/Programs/openclaw"));
        return FindFirstValidPath(candidates, IsValidOpenClawRoot);
    }

    private static TrayRuntimeConfig LoadRuntimeConfig(string appDir)
    {
        TrayRuntimeConfig config = new TrayRuntimeConfig();
        string configPath = Path.Combine(appDir, DefaultConfigFileName);
        if (File.Exists(configPath))
        {
            config.RuntimeRoot = NormalizeCandidatePath(appDir, ReadJsonString(configPath, "runtimeRoot"));
            string openClawRoot = ReadJsonString(configPath, "openClawRoot");
            if (string.IsNullOrWhiteSpace(openClawRoot)) openClawRoot = ReadJsonString(configPath, "openclawRoot");
            config.OpenClawRoot = NormalizeCandidatePath(appDir, openClawRoot);
            int gatewayPort = ReadJsonInt(configPath, "gatewayPort");
            if (gatewayPort > 0) config.GatewayPort = gatewayPort;
            string startupTaskName = ReadJsonString(configPath, "startupTaskName");
            if (!string.IsNullOrWhiteSpace(startupTaskName)) config.StartupTaskName = startupTaskName.Trim();
            string controlPanelPath = ReadJsonString(configPath, "controlPanelPath");
            if (!string.IsNullOrWhiteSpace(controlPanelPath)) config.ControlPanelPath = controlPanelPath.Trim();
        }
        config.RuntimeRoot = ResolveRuntimeRoot(appDir, config.RuntimeRoot);
        config.OpenClawRoot = ResolveOpenClawRoot(appDir, config.RuntimeRoot, config.OpenClawRoot);
        return config;
    }

    [STAThread]
    private static void Main()
    {
        try
        {
            DebugLog.Write("main start");
            Application.EnableVisualStyles();
            DebugLog.Write("after visuals");
            Application.SetCompatibleTextRenderingDefault(false);
            DebugLog.Write("after text render");
            bool createdNew;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "OpenClaw.LobsterTeams.NativeTray", out createdNew))
            {
                DebugLog.Write("after mutex");
                if (!createdNew) return;
                try
                {
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    TrayRuntimeConfig config = LoadRuntimeConfig(appDir);
                    DebugLog.Write("runtimeRoot=" + config.RuntimeRoot);
                    DebugLog.Write("openClawRoot=" + config.OpenClawRoot);
                    if (!IsValidRuntimeRoot(config.RuntimeRoot))
                    {
                        MessageBox.Show("未找到兼容的本地运行时目录。请检查 openclawtp.runtime.json 中的 runtimeRoot，或确保运行时目录遵循 lobster-teams 脚本约定。", "OpenClaw Native Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    DebugLog.Write("before Application.Run");
                    Application.Run(new OpenClawTrayApp(new RuntimePaths(appDir, config.RuntimeRoot, config.OpenClawRoot, config.GatewayPort, config.StartupTaskName, config.ControlPanelPath)));
                    DebugLog.Write("after Application.Run");
                }
                catch (Exception ex)
                {
                    DebugLog.Write("fatal: " + ex.ToString());
                    MessageBox.Show(ex.Message, "OpenClaw Native Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write("outer fatal: " + ex.ToString());
            MessageBox.Show(ex.Message, "OpenClaw Native Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
