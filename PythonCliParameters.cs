using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace WakaTime
{
    internal class PythonCliParameters
    {
        public string Cli => Path.Combine(WakaTimeConstants.UserConfigDir, WakaTimeConstants.CliFolder);
        public string Key { get; set; }
        public string Entity { get; set; }
        public string Plugin { get; set; }        

        public string[] ToArray(bool obfuscate = false)
        {
            var parameters = new Collection<string>
            {
                Cli,
                "--key",
                obfuscate ? $"********-****-****-****-********{Key.Substring(Key.Length - 4)}" : Key,
                "--entity",
                Entity,
                "--plugin",
                Plugin,
                "--entitytype",
                "app",
                "--project",
                "<<LAST_PROJECT>>"
            };            

            return parameters.ToArray();
        }
    }
}