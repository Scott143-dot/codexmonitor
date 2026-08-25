using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CodexMonitor
{
    public class AppConfig
    {
        public int pos_x { get; set; }
        public int pos_y { get; set; }
        public bool is_docked { get; set; }
        public string dock_side { get; set; }
        public string trail_mode { get; set; }
        public string color_theme { get; set; }

        public AppConfig()
        {
            pos_x = 300;
            pos_y = 300;
            is_docked = false;
            dock_side = "none";
            trail_mode = "laser";
            color_theme = "slate";
        }
    }

    public static class ConfigManager
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "arcane_config.json");

        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string text = File.ReadAllText(ConfigPath);
                    var dict = Serializer.Deserialize<Dictionary<string, object>>(text);
                    if (dict != null)
                    {
                        if (dict.ContainsKey("pos_x")) cfg.pos_x = Convert.ToInt32(dict["pos_x"]);
                        if (dict.ContainsKey("pos_y")) cfg.pos_y = Convert.ToInt32(dict["pos_y"]);
                        if (dict.ContainsKey("is_docked")) cfg.is_docked = Convert.ToBoolean(dict["is_docked"]);
                        if (dict.ContainsKey("dock_side")) cfg.dock_side = Convert.ToString(dict["dock_side"]);
                        if (dict.ContainsKey("trail_mode")) cfg.trail_mode = Convert.ToString(dict["trail_mode"]);
                        if (dict.ContainsKey("color_theme")) cfg.color_theme = Convert.ToString(dict["color_theme"]);
                    }
                }
            }
            catch { }
            return cfg;
        }

        public static void Save(AppConfig cfg)
        {
            try
            {
                var dict = new Dictionary<string, object>
                {
                    { "pos_x", cfg.pos_x },
                    { "pos_y", cfg.pos_y },
                    { "is_docked", cfg.is_docked },
                    { "dock_side", cfg.dock_side },
                    { "trail_mode", cfg.trail_mode },
                    { "color_theme", cfg.color_theme }
                };
                string json = Serializer.Serialize(dict);
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
