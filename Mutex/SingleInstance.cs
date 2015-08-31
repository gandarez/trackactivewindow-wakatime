using System;

namespace WakaTime.Mutex
{
    static class SingleInstance
    {
        public static readonly int WM_SHOWFIRSTINSTANCE = WinApi.RegisterWindowMessage("WM_SHOWFIRSTINSTANCE|{0}", ProgramInfo.AssemblyGuid);
        private static System.Threading.Mutex _mutex;

        static public bool Start()
        {
            bool onlyInstance;
            string mutexName = $"Local\\{ProgramInfo.AssemblyGuid}";

            // if you want your app to be limited to a single instance
            // across ALL SESSIONS (multiple users & terminal services), then use the following line instead:
            // string mutexName = String.Format("Global\\{0}", ProgramInfo.AssemblyGuid);

            _mutex = new System.Threading.Mutex(true, mutexName, out onlyInstance);
            return onlyInstance;
        }

        public static void ShowFirstInstance()
        {
            WinApi.PostMessage(
                (IntPtr)WinApi.HWND_BROADCAST,
                WM_SHOWFIRSTINSTANCE,
                IntPtr.Zero,
                IntPtr.Zero);
        }

        public static void Stop()
        {
            _mutex.ReleaseMutex();
        }
    }
}
