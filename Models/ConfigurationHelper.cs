using System.IO;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace CallRecording.Models
{
    public static class ConfigurationHelper
    {
        private static IConfigurationRoot _configuration;
        private static string _configFile = "appsettings.json";

        static ConfigurationHelper()
        {
            LoadConfiguration();
        }

        private static void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(_configFile, optional: false, reloadOnChange: true);
            _configuration = builder.Build();
        }

        public static string GetSetting(string key)
        {
            var value = _configuration[key];
            if (value == null)
            {
                SetSetting(key, "null"); // 如果配置项不存在，则设置默认值"null"
            }

            return value ?? "null"; // 如果配置项不存在，则返回默认值"null"
        }

        public static void SetSetting(string key, string value)
        {
            var json = File.ReadAllText(_configFile);
            var config = JObject.Parse(json);
            var token = config.SelectToken(key);
            if (token != null)
            {
                token.Replace(value);
            }
            else
            {
                var segments = key.Split(':');
                JToken parent = config;
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    var segment = segments[i];
                    var nextParent = parent[segment] as JObject;
                    if (nextParent == null)
                    {
                        nextParent = new JObject();
                        parent[segment] = nextParent;
                    }

                    parent = nextParent;
                }

                parent[segments[^1]] = value;
            }

            File.WriteAllText(_configFile, config.ToString());
            LoadConfiguration();
        }

        public static void RemoveSetting(string key)
        {
            var json = File.ReadAllText(_configFile);
            var config = JObject.Parse(json);
            var token = config.SelectToken(key);
            if (token != null)
            {
                token.Parent.Remove();
            }

            File.WriteAllText(_configFile, config.ToString());
            LoadConfiguration();
        }

        public static void ReloadConfiguration()
        {
            LoadConfiguration();
        }
    }
}