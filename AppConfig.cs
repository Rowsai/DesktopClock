using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DesktopClock
{
    public sealed class AppConfig
    {
        public double WindowWidth { get; set; } = 420;
        public double WindowHeight { get; set; } = 220;

        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;

        public bool Topmost { get; set; } = true;

        public string? BackgroundImagePath { get; set; }

        public bool ShowWeatherWidget { get; set; } = true;
        public double WeatherX { get; set; } = 20;
        public double WeatherY { get; set; } = 20;
        public bool LockWeatherMove { get; set; } = false;
        public string WeatherPrefecture { get; set; } = "東京都";
        public bool ShowHotbarWidget { get; set; } = true;

        // 旧設定との互換用。現在は各スロットの X/Y を使います。
        public double HotbarX { get; set; } = 20;
        public double HotbarY { get; set; } = 260;

        public int HotbarItemCount { get; set; } = 8;
        public bool LockHotbarMove { get; set; } = false;

        public List<HotbarItemConfig> HotbarItems { get; set; } = new();

        private static string ConfigDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "DesktopClock");
            }
        }

        private static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    return CreateDefault();
                }

                string json = File.ReadAllText(ConfigPath);
                AppConfig? config = JsonSerializer.Deserialize<AppConfig>(json);

                if (config == null)
                {
                    return CreateDefault();
                }

                config.EnsureDefaults();
                return config;
            }
            catch
            {
                return CreateDefault();
            }
        }

        public void Save()
        {
            try
            {
                EnsureDefaults();

                Directory.CreateDirectory(ConfigDirectory);

                string json = JsonSerializer.Serialize(
                    this,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // 保存失敗してもアプリは止めない
            }
        }

        public void EnsureDefaults()
        {
            HotbarItems ??= new List<HotbarItemConfig>();

            HotbarItemCount = Math.Clamp(HotbarItemCount, 1, 8);

            while (HotbarItems.Count < 8)
            {
                int number = HotbarItems.Count + 1;

                HotbarItems.Add(new HotbarItemConfig
                {
                    Name = $"Slot {number}",
                    IconPath = "",
                    HotKey = "",
                    AppPath = "",
                    Arguments = "",
                    Url = "",
                    X = 20 + ((number - 1) * 54),
                    Y = 260
                });
            }

            bool allHotbarsAtZero = true;

            for (int i = 0; i < HotbarItems.Count; i++)
            {
                HotbarItemConfig item = HotbarItems[i];

                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    item.Name = $"Slot {i + 1}";
                }

                if (item.X != 0 || item.Y != 0)
                {
                    allHotbarsAtZero = false;
                }
            }

            if (allHotbarsAtZero)
            {
                for (int i = 0; i < HotbarItems.Count; i++)
                {
                    HotbarItems[i].X = 20 + (i * 54);
                    HotbarItems[i].Y = 260;
                }
            }

            if (WindowWidth < 260)
            {
                WindowWidth = 420;
            }

            if (WindowHeight < 160)
            {
                WindowHeight = 220;
            }
        }

        private static AppConfig CreateDefault()
        {
            AppConfig config = new AppConfig();
            config.EnsureDefaults();
            return config;
        }
    }

    public sealed class HotbarItemConfig
    {
        public string Name { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string HotKey { get; set; } = "";
        public string AppPath { get; set; } = "";
        public string Arguments { get; set; } = "";

        public string Url { get; set; } = "";

        public double X { get; set; } = 20;
        public double Y { get; set; } = 260;
    }
}