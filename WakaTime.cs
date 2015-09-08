using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using WakaTime.Forms;

namespace WakaTime
{
    internal class WakaTime
    {
        private static string _version = string.Empty;
        private static WakaTimeConfigFile _wakaTimeConfigFile;
        internal SettingsForm SettingsForm;

        public static bool Debug;
        public static string ApiKey;
        static readonly PythonCliParameters PythonCliParameters = new PythonCliParameters();
        private static string _lastWindowTitle;
        DateTime _lastHeartbeat = DateTime.UtcNow.AddMinutes(-3);
        private static readonly object ThreadLock = new object();

        const uint WINEVENT_OUTOFCONTEXT = 0;
        const uint EVENT_SYSTEM_FOREGROUND = 3;
        private IntPtr _winHook;
        private NativeMethods.WinEventProc _listener;
        private readonly NotifyIcon _notifyIcon;

        public WakaTime(NotifyIcon notifyIcon)
        {
            _notifyIcon = notifyIcon;
        }

        public void Initialize()
        {
            SetNotifyIconToolTip();
            _version = $"{CoreAssembly.Version.Major}.{CoreAssembly.Version.Minor}.{CoreAssembly.Version.Build}";

            try
            {
                Logger.Info($"Initializing WakaTime v{_version}");

                SettingsForm = new SettingsForm();
                SettingsForm.ConfigSaved += SettingsFormOnConfigSaved;
                _wakaTimeConfigFile = new WakaTimeConfigFile();

                // Make sure python is installed
                if (!PythonManager.IsPythonInstalled())
                {
                    var dialogResult = MessageBox.Show(@"Let's download and install Python now?", @"WakaTime requires Python", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        var url = PythonManager.PythonDownloadUrl;
                        Downloader.DownloadPython(url, WakaTimeConstants.UserConfigDir);
                    }
                    else
                        MessageBox.Show(
                            @"Please install Python (https://www.python.org/downloads/) and restart Visual Studio to enable the WakaTime plugin.",
                            @"WakaTime", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                if (!DoesCliExist() || !IsCliLatestVersion())
                {
                    try
                    {
                        Directory.Delete($"{WakaTimeConstants.UserConfigDir}\\wakatime-master", true);
                    }
                    catch { /* ignored */ }

                    Downloader.DownloadCli(WakaTimeConstants.CliUrl, WakaTimeConstants.UserConfigDir);
                }

                GetSettings();

                if (string.IsNullOrEmpty(ApiKey))
                    PromptApiKey();

                StartListeningForWindowChanges();

                Logger.Info($"Finished initializing WakaTime v{_version}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error initializing Wakatime", ex);
            }
        }

        private static void SettingsFormOnConfigSaved(object sender, EventArgs eventArgs)
        {
            _wakaTimeConfigFile.Read();
            GetSettings();
        }

        static bool DoesCliExist()
        {
            return File.Exists(PythonCliParameters.Cli);
        }

        static bool IsCliLatestVersion()
        {
            var process = new RunProcess(PythonManager.GetPython(), PythonCliParameters.Cli, "--version");
            process.Run();

            var wakatimeVersion = WakaTimeConstants.CurrentWakaTimeCliVersion();

            return process.Success && process.Error.Equals(wakatimeVersion);
        }

        private static void GetSettings()
        {
            ApiKey = _wakaTimeConfigFile.ApiKey;
            Debug = _wakaTimeConfigFile.Debug;
        }

        private static void PromptApiKey()
        {
            var form = new ApiKeyForm();
            form.ShowDialog();
        }

        internal void StartListeningForWindowChanges()
        {
            _listener = EventCallback;
            //setting the window hook
            _winHook = NativeMethods.SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _listener, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        internal void StopListeningForWindowChanges()
        {
            NativeMethods.UnhookWinEvent(_winHook);
        }

        private void EventCallback(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime)
        {
            var windowTitle = GetActiveProcessInfo();
            if (windowTitle == null) return;
            HandleActivity(windowTitle);
        }

        static string GetActiveProcessInfo()
        {
            try
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                uint pid;
                NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
                var p = Process.GetProcessById((int)pid);
                return p.MainWindowTitle;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting ingo from active process {ex}");
                return null;
            }
        }

        private void HandleActivity(string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle)) return;

            Task.Run(() =>
            {
                lock (ThreadLock)
                {
                    if (_lastWindowTitle != null && !EnoughTimePassed() && windowTitle.Equals(_lastWindowTitle))
                        return;

                    SendHeartbeat(windowTitle);
                    _lastWindowTitle = windowTitle;
                    _lastHeartbeat = DateTime.UtcNow;
                }
            });
        }

        private bool EnoughTimePassed()
        {
            return _lastHeartbeat < DateTime.UtcNow.AddMinutes(-1);
        }

        public static void SendHeartbeat(string windowTitle)
        {
            PythonCliParameters.Key = ApiKey;
            PythonCliParameters.Entity = windowTitle;
            PythonCliParameters.Plugin = $"{WakaTimeConstants.PluginName}/{_version}";

            var pythonBinary = PythonManager.GetPython();
            if (pythonBinary != null)
            {
                var process = new RunProcess(pythonBinary, PythonCliParameters.ToArray());
                if (Debug)
                {
                    Logger.Debug($"[\"{pythonBinary}\", \"{string.Join("\", \"", PythonCliParameters.ToArray(true))}\"]");
                    process.Run();
                    Logger.Debug($"CLI STDOUT: {process.Output}");
                    Logger.Debug($"CLI STDERR: {process.Error}");
                }
                else
                    process.RunInBackground();

                if (!process.Success)
                    Logger.Error($"Could not send heartbeat: {process.Error}");
            }
            else
                Logger.Error("Could not send heartbeat because python is not installed");
        }

        public ToolStripMenuItem ToolStripMenuItemWithHandler(string displayText, EventHandler eventHandler)
        {
            var item = new ToolStripMenuItem(displayText);
            if (eventHandler != null) { item.Click += eventHandler; }
            item.Image = null;
            item.ToolTipText = string.Empty;
            return item;
        }

        private void SetNotifyIconToolTip()
        {
            _notifyIcon.Text = "WakaTime Track Active Window";
        }
    }

    static class CoreAssembly
    {
        static readonly Assembly Reference = typeof(CoreAssembly).Assembly;
        public static readonly Version Version = Reference.GetName().Version;
    }
}
