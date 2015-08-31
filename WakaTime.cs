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
                    var url = PythonManager.PythonDownloadUrl;
                    Downloader.DownloadPython(url, WakaTimeConstants.UserConfigDir);
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
            var process = GetActiveProcessInfo();
            if (process == null) return;            
            HandleActivity(process[0], process[1]);
        }

        static string[] GetActiveProcessInfo()
        {
            try
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                uint pid;
                NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
                var p = Process.GetProcessById((int)pid);                
                return new[] { p.MainWindowTitle, Path.GetFileName(p.MainModule.FileName).ToLower() };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting ingo from active process {ex}");
                return null;
            }
        }

        private void HandleActivity(string windowTitle, string processName)
        {
            if (string.IsNullOrEmpty(windowTitle)) return;

            Task.Run(() =>
            {
                lock (ThreadLock)
                {
                    if (_lastWindowTitle != null && !EnoughTimePassed() && windowTitle.Equals(_lastWindowTitle))
                        return;

                    SendHeartbeat(windowTitle, processName);
                    _lastWindowTitle = windowTitle;
                    _lastHeartbeat = DateTime.UtcNow;
                }
            });
        }

        private bool EnoughTimePassed()
        {
            return _lastHeartbeat < DateTime.UtcNow.AddMinutes(-1);
        }

        public static void SendHeartbeat(string windowTitle, string processName)
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
            }
            else
                Logger.Error("Could not send heartbeat because python is not installed");
        }

        private ToolStripMenuItem ToolStripMenuItemWithHandler(
            string displayText, int enabledCount, int disabledCount, EventHandler eventHandler)
        {
            var item = new ToolStripMenuItem(displayText);
            if (eventHandler != null) { item.Click += eventHandler; }

            //item.Image = (enabledCount > 0 && disabledCount > 0) ? Properties.Resources.signal_yellow
            //             : (enabledCount > 0) ? Properties.Resources.signal_green
            //             : (disabledCount > 0) ? Properties.Resources.signal_red
            //             : null;
            item.Image = null;
            //item.ToolTipText = (enabledCount > 0 && disabledCount > 0) ?
            //                                     string.Format("{0} enabled, {1} disabled", enabledCount, disabledCount)
            //             : (enabledCount > 0) ? string.Format("{0} enabled", enabledCount)
            //             : (disabledCount > 0) ? string.Format("{0} disabled", disabledCount)
            //             : "";
            item.ToolTipText = "";
            return item;            
        }

        public ToolStripMenuItem ToolStripMenuItemWithHandler(string displayText, EventHandler eventHandler)
        {
            return ToolStripMenuItemWithHandler(displayText, 0, 0, eventHandler);
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
