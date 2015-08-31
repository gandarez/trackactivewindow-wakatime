using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WakaTime.Mutex
{
    static class ProgramInfo
    {
        static internal string AssemblyGuid
        {
            get
            {
                var attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(GuidAttribute), false);
                return attributes.Length == 0 ? string.Empty : ((GuidAttribute)attributes[0]).Value;
            }
        }
        static internal string AssemblyTitle
        {
            get
            {
                var attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    var titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                        return titleAttribute.Title;
                }
                return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().CodeBase);
            }
        }
    }
}