using System.Configuration;
using Microsoft.Win32;

namespace Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Tests.TestSupport
{
    internal class ConfigurationHelper
    {
        public static string GetSetting(string settingName)
        {
            string value = null;
            using (var subKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\EntLib") ?? Registry.CurrentUser)
            {
                var keyValue = subKey.GetValue(settingName);
                if (keyValue != null)
                {
                    value = keyValue.ToString();
                }
            }

            if (string.IsNullOrEmpty(value))
            {
                value = ConfigurationManager.AppSettings[settingName];
            }

            return value;
        }
    }
}