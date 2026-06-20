using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DesktopClock
{
    public sealed class WeatherForecastService
    {
        private static readonly Dictionary<string, WeatherLocation> Locations = new()
        {
            ["北海道"] = new WeatherLocation("北海道", 43.0642, 141.3469),
            ["青森県"] = new WeatherLocation("青森県", 40.8244, 140.7400),
            ["岩手県"] = new WeatherLocation("岩手県", 39.7036, 141.1527),
            ["宮城県"] = new WeatherLocation("宮城県", 38.2688, 140.8721),
            ["秋田県"] = new WeatherLocation("秋田県", 39.7186, 140.1024),
            ["山形県"] = new WeatherLocation("山形県", 38.2404, 140.3633),
            ["福島県"] = new WeatherLocation("福島県", 37.7503, 140.4675),

            ["茨城県"] = new WeatherLocation("茨城県", 36.3418, 140.4468),
            ["栃木県"] = new WeatherLocation("栃木県", 36.5657, 139.8836),
            ["群馬県"] = new WeatherLocation("群馬県", 36.3912, 139.0609),
            ["埼玉県"] = new WeatherLocation("埼玉県", 35.8574, 139.6489),
            ["千葉県"] = new WeatherLocation("千葉県", 35.6051, 140.1233),
            ["東京都"] = new WeatherLocation("東京都", 35.6895, 139.6917),
            ["神奈川県"] = new WeatherLocation("神奈川県", 35.4478, 139.6425),

            ["新潟県"] = new WeatherLocation("新潟県", 37.9026, 139.0232),
            ["富山県"] = new WeatherLocation("富山県", 36.6953, 137.2113),
            ["石川県"] = new WeatherLocation("石川県", 36.5947, 136.6256),
            ["福井県"] = new WeatherLocation("福井県", 36.0652, 136.2216),
            ["山梨県"] = new WeatherLocation("山梨県", 35.6642, 138.5684),
            ["長野県"] = new WeatherLocation("長野県", 36.6513, 138.1810),
            ["岐阜県"] = new WeatherLocation("岐阜県", 35.3912, 136.7223),
            ["静岡県"] = new WeatherLocation("静岡県", 34.9769, 138.3831),
            ["愛知県"] = new WeatherLocation("愛知県", 35.1802, 136.9066),

            ["三重県"] = new WeatherLocation("三重県", 34.7303, 136.5086),
            ["滋賀県"] = new WeatherLocation("滋賀県", 35.0045, 135.8686),
            ["京都府"] = new WeatherLocation("京都府", 35.0212, 135.7556),
            ["大阪府"] = new WeatherLocation("大阪府", 34.6937, 135.5023),
            ["兵庫県"] = new WeatherLocation("兵庫県", 34.6913, 135.1830),
            ["奈良県"] = new WeatherLocation("奈良県", 34.6851, 135.8048),
            ["和歌山県"] = new WeatherLocation("和歌山県", 34.2260, 135.1675),

            ["鳥取県"] = new WeatherLocation("鳥取県", 35.5039, 134.2383),
            ["島根県"] = new WeatherLocation("島根県", 35.4723, 133.0505),
            ["岡山県"] = new WeatherLocation("岡山県", 34.6618, 133.9344),
            ["広島県"] = new WeatherLocation("広島県", 34.3963, 132.4594),
            ["山口県"] = new WeatherLocation("山口県", 34.1859, 131.4714),

            ["徳島県"] = new WeatherLocation("徳島県", 34.0658, 134.5594),
            ["香川県"] = new WeatherLocation("香川県", 34.3401, 134.0434),
            ["愛媛県"] = new WeatherLocation("愛媛県", 33.8416, 132.7661),
            ["高知県"] = new WeatherLocation("高知県", 33.5597, 133.5311),

            ["福岡県"] = new WeatherLocation("福岡県", 33.5902, 130.4017),
            ["佐賀県"] = new WeatherLocation("佐賀県", 33.2494, 130.2988),
            ["長崎県"] = new WeatherLocation("長崎県", 32.7503, 129.8777),
            ["熊本県"] = new WeatherLocation("熊本県", 32.7898, 130.7417),
            ["大分県"] = new WeatherLocation("大分県", 33.2382, 131.6126),
            ["宮崎県"] = new WeatherLocation("宮崎県", 31.9111, 131.4239),
            ["鹿児島県"] = new WeatherLocation("鹿児島県", 31.5602, 130.5581),
            ["沖縄県"] = new WeatherLocation("沖縄県", 26.2124, 127.6792)
        };

        public async Task<WeatherForecastResult?> GetForecastAsync(string? prefecture)
        {
            try
            {
                WeatherLocation location = GetLocation(prefecture);

                string latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
                string longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);

                string url =
                    "https://api.open-meteo.com/v1/forecast" +
                    $"?latitude={latitude}" +
                    $"&longitude={longitude}" +
                    "&current=temperature_2m,weather_code" +
                    "&daily=weather_code,temperature_2m_max,temperature_2m_min" +
                    "&timezone=Asia%2FTokyo";

                using HttpClient client = new HttpClient();
                string json = await client.GetStringAsync(url);

                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                JsonElement current = root.GetProperty("current");
                JsonElement daily = root.GetProperty("daily");

                double currentTemperature = current.GetProperty("temperature_2m").GetDouble();
                int currentWeatherCode = current.GetProperty("weather_code").GetInt32();

                JsonElement dailyWeatherCodes = daily.GetProperty("weather_code");
                JsonElement maxTemperatures = daily.GetProperty("temperature_2m_max");
                JsonElement minTemperatures = daily.GetProperty("temperature_2m_min");

                int todayWeatherCode = dailyWeatherCodes.GetArrayLength() > 0
                    ? dailyWeatherCodes[0].GetInt32()
                    : currentWeatherCode;

                int tomorrowWeatherCode = dailyWeatherCodes.GetArrayLength() > 1
                    ? dailyWeatherCodes[1].GetInt32()
                    : todayWeatherCode;

                double todayMax = maxTemperatures.GetArrayLength() > 0
                    ? maxTemperatures[0].GetDouble()
                    : 0;

                double todayMin = minTemperatures.GetArrayLength() > 0
                    ? minTemperatures[0].GetDouble()
                    : 0;

                double tomorrowMax = maxTemperatures.GetArrayLength() > 1
                    ? maxTemperatures[1].GetDouble()
                    : 0;

                double tomorrowMin = minTemperatures.GetArrayLength() > 1
                    ? minTemperatures[1].GetDouble()
                    : 0;

                return new WeatherForecastResult
                {
                    AreaName = location.Name,
                    CurrentWeather = ConvertWeatherCodeToJapanese(currentWeatherCode),
                    TodayWeather = ConvertWeatherCodeToJapanese(todayWeatherCode),
                    TomorrowWeather = ConvertWeatherCodeToJapanese(tomorrowWeatherCode),
                    CurrentTemperature = currentTemperature,
                    TodayMaxTemperature = todayMax,
                    TodayMinTemperature = todayMin,
                    TomorrowMaxTemperature = tomorrowMax,
                    TomorrowMinTemperature = tomorrowMin
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<WeatherForecastResult?> GetTokyoForecastAsync()
        {
            return await GetForecastAsync("東京都");
        }

        public static IReadOnlyList<string> GetPrefectureNames()
        {
            return new List<string>(Locations.Keys);
        }

        private static WeatherLocation GetLocation(string? prefecture)
        {
            if (!string.IsNullOrWhiteSpace(prefecture) &&
                Locations.TryGetValue(prefecture, out WeatherLocation? location))
            {
                return location;
            }

            return Locations["東京都"];
        }

        private static string ConvertWeatherCodeToJapanese(int code)
        {
            return code switch
            {
                0 => "快晴",
                1 => "晴れ",
                2 => "一部くもり",
                3 => "くもり",

                45 => "霧",
                48 => "霧氷",

                51 => "弱い霧雨",
                53 => "霧雨",
                55 => "強い霧雨",

                56 => "弱い凍結霧雨",
                57 => "強い凍結霧雨",

                61 => "弱い雨",
                63 => "雨",
                65 => "強い雨",

                66 => "弱い凍結雨",
                67 => "強い凍結雨",

                71 => "弱い雪",
                73 => "雪",
                75 => "強い雪",
                77 => "雪粒",

                80 => "弱いにわか雨",
                81 => "にわか雨",
                82 => "強いにわか雨",

                85 => "弱いにわか雪",
                86 => "強いにわか雪",

                95 => "雷雨",
                96 => "ひょうを伴う雷雨",
                99 => "強いひょうを伴う雷雨",

                _ => "不明"
            };
        }
    }

    public sealed class WeatherForecastResult
    {
        public string AreaName { get; set; } = "東京都";

        public string CurrentWeather { get; set; } = "";
        public string TodayWeather { get; set; } = "";
        public string TomorrowWeather { get; set; } = "";

        public double CurrentTemperature { get; set; }
        public double TodayMaxTemperature { get; set; }
        public double TodayMinTemperature { get; set; }
        public double TomorrowMaxTemperature { get; set; }
        public double TomorrowMinTemperature { get; set; }
    }

    public sealed class WeatherLocation
    {
        public WeatherLocation(string name, double latitude, double longitude)
        {
            Name = name;
            Latitude = latitude;
            Longitude = longitude;
        }

        public string Name { get; }
        public double Latitude { get; }
        public double Longitude { get; }
    }
}