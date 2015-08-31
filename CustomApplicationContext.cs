using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;

namespace WakaTime
{
    public class CustomApplicationContext : ApplicationContext
    {        
        private const string DefaultTooltip = "WakaTime settings menu";
        private readonly WakaTime _wakaTime;

        private IContainer components;
        private NotifyIcon _notifyIcon;

        public CustomApplicationContext()
        {
            InitializeContext();
            _wakaTime = new WakaTime(_notifyIcon);
            _wakaTime.Initialize();            
        }

        private void InitializeContext()
        {
            components = new Container();
            _notifyIcon = new NotifyIcon(components)
            {
                ContextMenuStrip = new ContextMenuStrip(),
                Icon =  Resource1.wakatime_32,
                Text = DefaultTooltip,
                Visible = true
            };
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
            _notifyIcon.MouseUp += notifyIcon_MouseUp;
        }

        private void ContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            e.Cancel = false;
            _notifyIcon.ContextMenuStrip.Items.Clear();
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add(_wakaTime.ToolStripMenuItemWithHandler("Settings", showSettingsItem_Click));            
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add(_wakaTime.ToolStripMenuItemWithHandler("&Exit", exitItem_Click));
        }

        private void notifyIcon_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke(_notifyIcon, null);
        }

        private void showSettingsItem_Click(object sender, EventArgs e)
        {
            _wakaTime.SettingsForm.ShowDialog();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { components?.Dispose(); }
        }

        private void exitItem_Click(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            _notifyIcon.Visible = false; // should remove lingering tray icon
            _wakaTime.StopListeningForWindowChanges();
            base.ExitThreadCore();
        }
    }
}
