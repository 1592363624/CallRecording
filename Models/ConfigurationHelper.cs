using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            return _configuration[key];
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
