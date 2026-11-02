using System;
using System.Collections.Generic;

namespace Coop.App
{
    public sealed class LaunchOptions
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public LaunchOptions()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "+connect_lobby" && i + 1 < args.Length)
                {
                    values["address"] = args[++i];
                    values["join"] = "true";
                    values["transport"] = "steam";
                    continue;
                }
                if (args[i].StartsWith("--"))
                    values[args[i].Substring(2)] = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[++i] : "true";
            }
        }
        public string Get(string key, string fallback = "") => values.TryGetValue(key, out var value) ? value : fallback;
        public bool Has(string key) => values.ContainsKey(key);
        public int Number(string key, int fallback) => int.TryParse(Get(key), out int value) ? value : fallback;
    }
}
