using System.Configuration;

namespace ConfigurationProvider
{
    public class ConfigProvider
    {
        public static string GetSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

    }
}
