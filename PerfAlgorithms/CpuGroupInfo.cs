using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

namespace PerfAlgorithms
{
    public class CpuGroupInfo
    {
        public static bool CanEnableGCCpuGroups()
        {
            return s_data.Value.EnableGCCpuGroups && s_data.Value.HasMultileGroups;
        }

        public static bool CanEnableThreadUseAllCpuGroups()
        {
            return s_data.Value.ThreadsUseAllCpuGroups && s_data.Value.HasMultileGroups;
        }

        public static void Dump(TextWriter tw)
        {
            tw.WriteLine("EnableGCCpuGroups = {0}", s_data.Value.EnableGCCpuGroups);
            tw.WriteLine("ThreadsUseAllCpuGroups = {0}", s_data.Value.ThreadsUseAllCpuGroups);
            tw.WriteLine("HasMultileGroups = {0}", s_data.Value.HasMultileGroups);
            tw.WriteLine("GroupCount = {0}", s_data.Value.GroupCount);
        }

        private class Data
        {
            public bool HasMultileGroups => GroupCount > 1;
            public bool EnableGCCpuGroups;
            public bool ThreadsUseAllCpuGroups;
            public int GroupCount;
        }

        private static readonly Lazy<XmlElement> s_runtimeSection = new Lazy<XmlElement>(() =>
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                string rawXml = config.GetSection("runtime")?.SectionInformation.GetRawXml();
                if (!string.IsNullOrEmpty(rawXml))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(rawXml);
                    if (doc.DocumentElement != null)
                        return doc.DocumentElement;
                }
                return null;
            }
            catch (Exception ex)
            {
                Trace.Write(ex);
                return null;
            }
        });

        private static readonly Lazy<Data> s_data = new Lazy<Data>(() =>
        {
            var data = new Data();
            data.GroupCount = NativeMethods.GetCpuGroupCount();
            data.EnableGCCpuGroups = GetClrConfigValue("GCCpuGroup");
            data.ThreadsUseAllCpuGroups = GetClrConfigValue("Thread_UseAllCpuGroups");
            return data;
        });

        private static bool GetClrConfigValue(string switchName)
        {
            // A poor man's version of the CLRConfig native class in the CLR.
            // The real thing, does a lot more things, e.g. one can say wether
            // to prefer env over app.config over registry, or other variations
            // thereof.
            // Note also that this version currently ignores the registry.

            string env = Environment.GetEnvironmentVariable("COMPLUS_" + switchName);
            if (env != null)
            {
                bool flag;
                if (Boolean.TryParse(env, out flag))
                    return flag;
            }

            var enabled = s_runtimeSection.Value?
                .ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(n => n.Name.Equals(switchName, StringComparison.OrdinalIgnoreCase))?
                .GetAttribute("enabled");

            if (!string.IsNullOrEmpty(enabled))
            {
                bool flag;
                if (Boolean.TryParse(enabled, out flag))
                    return flag;
            }

            return false;
        }
    }
}