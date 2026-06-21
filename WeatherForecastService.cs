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
        private static readonly Dictionary<string, List<WeatherLocation>> LocationsByPrefecture = new()
        {
            ["北海道"] = new List<WeatherLocation>
            {
                new WeatherLocation("北海道", 43.0642, 141.3469),
                new WeatherLocation("札幌市", 43.0618, 141.3545),
                new WeatherLocation("函館市", 41.7687, 140.7288),
                new WeatherLocation("旭川市", 43.7706, 142.3649)
            },
            ["青森県"] = new List<WeatherLocation>
            {
                new WeatherLocation("青森県", 40.8244, 140.7400),
                new WeatherLocation("青森市", 40.8244, 140.7400),
                new WeatherLocation("弘前市", 40.6031, 140.4641),
                new WeatherLocation("八戸市", 40.5123, 141.4884)
            },
            ["岩手県"] = new List<WeatherLocation>
            {
                new WeatherLocation("岩手県", 39.7036, 141.1527),
                new WeatherLocation("盛岡市", 39.7036, 141.1527),
                new WeatherLocation("一関市", 38.9347, 141.1266)
            },
            ["宮城県"] = new List<WeatherLocation>
            {
                new WeatherLocation("宮城県", 38.2688, 140.8721),
                new WeatherLocation("仙台市", 38.2688, 140.8721),
                new WeatherLocation("石巻市", 38.4345, 141.3029)
            },
            ["秋田県"] = new List<WeatherLocation>
            {
                new WeatherLocation("秋田県", 39.7186, 140.1024),
                new WeatherLocation("秋田市", 39.7186, 140.1024),
                new WeatherLocation("横手市", 39.3136, 140.5666)
            },
            ["山形県"] = new List<WeatherLocation>
            {
                new WeatherLocation("山形県", 38.2404, 140.3633),
                new WeatherLocation("山形市", 38.2404, 140.3633),
                new WeatherLocation("米沢市", 37.9222, 140.1167)
            },
            ["福島県"] = new List<WeatherLocation>
            {
                new WeatherLocation("福島県", 37.7503, 140.4675),
                new WeatherLocation("福島市", 37.7503, 140.4675),
                new WeatherLocation("郡山市", 37.4004, 140.3597),
                new WeatherLocation("いわき市", 37.0505, 140.8877)
            },

            ["茨城県"] = new List<WeatherLocation>
            {
                new WeatherLocation("茨城県", 36.3418, 140.4468),
                new WeatherLocation("水戸市", 36.3658, 140.4712),
                new WeatherLocation("つくば市", 36.0835, 140.0764)
            },
            ["栃木県"] = new List<WeatherLocation>
            {
                new WeatherLocation("栃木県", 36.5657, 139.8836),
                new WeatherLocation("宇都宮市", 36.5551, 139.8826),
                new WeatherLocation("小山市", 36.3147, 139.8001)
            },
            ["群馬県"] = new List<WeatherLocation>
            {
                new WeatherLocation("群馬県", 36.3912, 139.0609),
                new WeatherLocation("前橋市", 36.3895, 139.0634),
                new WeatherLocation("高崎市", 36.3219, 139.0033)
            },
            ["埼玉県"] = new List<WeatherLocation>
            {
                new WeatherLocation("埼玉県", 35.8574, 139.6489),
                new WeatherLocation("さいたま市", 35.8617, 139.6455),
                new WeatherLocation("川越市", 35.9251, 139.4858),
                new WeatherLocation("所沢市", 35.7992, 139.4686)
            },
            ["千葉県"] = new List<WeatherLocation>
            {
                new WeatherLocation("千葉県", 35.6051, 140.1233),
                new WeatherLocation("千葉市", 35.6074, 140.1065),
                new WeatherLocation("船橋市", 35.6947, 139.9826),
                new WeatherLocation("柏市", 35.8676, 139.9758)
            },

            ["東京都"] = new List<WeatherLocation>
            {
                new WeatherLocation("東京都", 35.6895, 139.6917),

                new WeatherLocation("千代田区", 35.6940, 139.7536),
                new WeatherLocation("中央区", 35.6707, 139.7720),
                new WeatherLocation("港区", 35.6581, 139.7516),
                new WeatherLocation("新宿区", 35.6938, 139.7034),
                new WeatherLocation("文京区", 35.7080, 139.7520),
                new WeatherLocation("台東区", 35.7126, 139.7802),
                new WeatherLocation("墨田区", 35.7107, 139.8015),
                new WeatherLocation("江東区", 35.6728, 139.8174),
                new WeatherLocation("品川区", 35.6092, 139.7301),
                new WeatherLocation("目黒区", 35.6415, 139.6982),
                new WeatherLocation("大田区", 35.5613, 139.7160),
                new WeatherLocation("世田谷区", 35.6466, 139.6532),
                new WeatherLocation("渋谷区", 35.6618, 139.7041),
                new WeatherLocation("中野区", 35.7074, 139.6638),
                new WeatherLocation("杉並区", 35.6995, 139.6364),
                new WeatherLocation("豊島区", 35.7261, 139.7167),
                new WeatherLocation("北区", 35.7528, 139.7336),
                new WeatherLocation("荒川区", 35.7362, 139.7833),
                new WeatherLocation("板橋区", 35.7513, 139.7093),
                new WeatherLocation("練馬区", 35.7356, 139.6517),
                new WeatherLocation("足立区", 35.7750, 139.8044),
                new WeatherLocation("葛飾区", 35.7434, 139.8472),
                new WeatherLocation("江戸川区", 35.7067, 139.8683),

                new WeatherLocation("八王子市", 35.6664, 139.3160),
                new WeatherLocation("立川市", 35.7138, 139.4078),
                new WeatherLocation("武蔵野市", 35.7177, 139.5661),
                new WeatherLocation("三鷹市", 35.6835, 139.5596),
                new WeatherLocation("青梅市", 35.7880, 139.2758),
                new WeatherLocation("府中市", 35.6689, 139.4777),
                new WeatherLocation("昭島市", 35.7057, 139.3536),
                new WeatherLocation("調布市", 35.6506, 139.5407),
                new WeatherLocation("町田市", 35.5466, 139.4386),
                new WeatherLocation("小金井市", 35.6995, 139.5030),
                new WeatherLocation("小平市", 35.7285, 139.4774),
                new WeatherLocation("日野市", 35.6713, 139.3951),
                new WeatherLocation("東村山市", 35.7546, 139.4685),
                new WeatherLocation("国分寺市", 35.7109, 139.4622),
                new WeatherLocation("国立市", 35.6839, 139.4414),
                new WeatherLocation("福生市", 35.7385, 139.3268),
                new WeatherLocation("狛江市", 35.6348, 139.5787),
                new WeatherLocation("東大和市", 35.7455, 139.4265),
                new WeatherLocation("清瀬市", 35.7857, 139.5264),
                new WeatherLocation("東久留米市", 35.7581, 139.5298),
                new WeatherLocation("武蔵村山市", 35.7548, 139.3875),
                new WeatherLocation("多摩市", 35.6379, 139.4463),
                new WeatherLocation("稲城市", 35.6380, 139.5046),
                new WeatherLocation("羽村市", 35.7672, 139.3111),
                new WeatherLocation("あきる野市", 35.7288, 139.2941),
                new WeatherLocation("西東京市", 35.7256, 139.5383)
            },

            ["神奈川県"] = new List<WeatherLocation>
            {
                new WeatherLocation("神奈川県", 35.4478, 139.6425),
                new WeatherLocation("横浜市", 35.4437, 139.6380),
                new WeatherLocation("川崎市", 35.5308, 139.7036),
                new WeatherLocation("相模原市", 35.5714, 139.3732),
                new WeatherLocation("横須賀市", 35.2813, 139.6722),
                new WeatherLocation("平塚市", 35.3356, 139.3495),
                new WeatherLocation("鎌倉市", 35.3192, 139.5467),
                new WeatherLocation("藤沢市", 35.3392, 139.4900),
                new WeatherLocation("小田原市", 35.2646, 139.1522),
                new WeatherLocation("茅ヶ崎市", 35.3339, 139.4047),
                new WeatherLocation("厚木市", 35.4431, 139.3626),
                new WeatherLocation("大和市", 35.4875, 139.4580)
            },

            ["新潟県"] = new List<WeatherLocation>
            {
                new WeatherLocation("新潟県", 37.9026, 139.0232),
                new WeatherLocation("新潟市", 37.9161, 139.0364),
                new WeatherLocation("長岡市", 37.4463, 138.8512)
            },
            ["富山県"] = new List<WeatherLocation>
            {
                new WeatherLocation("富山県", 36.6953, 137.2113),
                new WeatherLocation("富山市", 36.6959, 137.2137),
                new WeatherLocation("高岡市", 36.7541, 137.0257)
            },
            ["石川県"] = new List<WeatherLocation>
            {
                new WeatherLocation("石川県", 36.5947, 136.6256),
                new WeatherLocation("金沢市", 36.5613, 136.6562),
                new WeatherLocation("小松市", 36.4084, 136.4455)
            },
            ["福井県"] = new List<WeatherLocation>
            {
                new WeatherLocation("福井県", 36.0652, 136.2216),
                new WeatherLocation("福井市", 36.0641, 136.2196),
                new WeatherLocation("敦賀市", 35.6452, 136.0556)
            },
            ["山梨県"] = new List<WeatherLocation>
            {
                new WeatherLocation("山梨県", 35.6642, 138.5684),
                new WeatherLocation("甲府市", 35.6622, 138.5683),
                new WeatherLocation("富士吉田市", 35.4875, 138.8070)
            },
            ["長野県"] = new List<WeatherLocation>
            {
                new WeatherLocation("長野県", 36.6513, 138.1810),
                new WeatherLocation("長野市", 36.6486, 138.1948),
                new WeatherLocation("松本市", 36.2380, 137.9720)
            },
            ["岐阜県"] = new List<WeatherLocation>
            {
                new WeatherLocation("岐阜県", 35.3912, 136.7223),
                new WeatherLocation("岐阜市", 35.4233, 136.7607),
                new WeatherLocation("高山市", 36.1461, 137.2522)
            },
            ["静岡県"] = new List<WeatherLocation>
            {
                new WeatherLocation("静岡県", 34.9769, 138.3831),
                new WeatherLocation("静岡市", 34.9756, 138.3828),
                new WeatherLocation("浜松市", 34.7108, 137.7261),
                new WeatherLocation("沼津市", 35.0956, 138.8635)
            },
            ["愛知県"] = new List<WeatherLocation>
            {
                new WeatherLocation("愛知県", 35.1802, 136.9066),
                new WeatherLocation("名古屋市", 35.1815, 136.9066),
                new WeatherLocation("豊橋市", 34.7692, 137.3915),
                new WeatherLocation("岡崎市", 34.9548, 137.1743)
            },

            ["三重県"] = new List<WeatherLocation>
            {
                new WeatherLocation("三重県", 34.7303, 136.5086),
                new WeatherLocation("津市", 34.7186, 136.5056),
                new WeatherLocation("四日市市", 34.9650, 136.6244)
            },
            ["滋賀県"] = new List<WeatherLocation>
            {
                new WeatherLocation("滋賀県", 35.0045, 135.8686),
                new WeatherLocation("大津市", 35.0179, 135.8546),
                new WeatherLocation("彦根市", 35.2744, 136.2597)
            },
            ["京都府"] = new List<WeatherLocation>
            {
                new WeatherLocation("京都府", 35.0212, 135.7556),
                new WeatherLocation("京都市", 35.0116, 135.7681),
                new WeatherLocation("宇治市", 34.8844, 135.7998)
            },
            ["大阪府"] = new List<WeatherLocation>
            {
                new WeatherLocation("大阪府", 34.6937, 135.5023),
                new WeatherLocation("大阪市", 34.6937, 135.5023),
                new WeatherLocation("堺市", 34.5733, 135.4831),
                new WeatherLocation("豊中市", 34.7813, 135.4697),
                new WeatherLocation("吹田市", 34.7595, 135.5169),
                new WeatherLocation("高槻市", 34.8461, 135.6170),
                new WeatherLocation("枚方市", 34.8143, 135.6507)
            },
            ["兵庫県"] = new List<WeatherLocation>
            {
                new WeatherLocation("兵庫県", 34.6913, 135.1830),
                new WeatherLocation("神戸市", 34.6901, 135.1955),
                new WeatherLocation("姫路市", 34.8151, 134.6853),
                new WeatherLocation("西宮市", 34.7376, 135.3416)
            },
            ["奈良県"] = new List<WeatherLocation>
            {
                new WeatherLocation("奈良県", 34.6851, 135.8048),
                new WeatherLocation("奈良市", 34.6851, 135.8048),
                new WeatherLocation("橿原市", 34.5094, 135.7926)
            },
            ["和歌山県"] = new List<WeatherLocation>
            {
                new WeatherLocation("和歌山県", 34.2260, 135.1675),
                new WeatherLocation("和歌山市", 34.2304, 135.1708),
                new WeatherLocation("田辺市", 33.7280, 135.3778)
            },

            ["鳥取県"] = new List<WeatherLocation>
            {
                new WeatherLocation("鳥取県", 35.5039, 134.2383),
                new WeatherLocation("鳥取市", 35.5011, 134.2351),
                new WeatherLocation("米子市", 35.4281, 133.3309)
            },
            ["島根県"] = new List<WeatherLocation>
            {
                new WeatherLocation("島根県", 35.4723, 133.0505),
                new WeatherLocation("松江市", 35.4681, 133.0484),
                new WeatherLocation("出雲市", 35.3670, 132.7546)
            },
            ["岡山県"] = new List<WeatherLocation>
            {
                new WeatherLocation("岡山県", 34.6618, 133.9344),
                new WeatherLocation("岡山市", 34.6551, 133.9195),
                new WeatherLocation("倉敷市", 34.5850, 133.7722)
            },
            ["広島県"] = new List<WeatherLocation>
            {
                new WeatherLocation("広島県", 34.3963, 132.4594),
                new WeatherLocation("広島市", 34.3853, 132.4553),
                new WeatherLocation("福山市", 34.4859, 133.3623)
            },
            ["山口県"] = new List<WeatherLocation>
            {
                new WeatherLocation("山口県", 34.1859, 131.4714),
                new WeatherLocation("山口市", 34.1785, 131.4737),
                new WeatherLocation("下関市", 33.9578, 130.9413)
            },

            ["徳島県"] = new List<WeatherLocation>
            {
                new WeatherLocation("徳島県", 34.0658, 134.5594),
                new WeatherLocation("徳島市", 34.0703, 134.5548)
            },
            ["香川県"] = new List<WeatherLocation>
            {
                new WeatherLocation("香川県", 34.3401, 134.0434),
                new WeatherLocation("高松市", 34.3428, 134.0466)
            },
            ["愛媛県"] = new List<WeatherLocation>
            {
                new WeatherLocation("愛媛県", 33.8416, 132.7661),
                new WeatherLocation("松山市", 33.8392, 132.7657),
                new WeatherLocation("今治市", 34.0661, 132.9978)
            },
            ["高知県"] = new List<WeatherLocation>
            {
                new WeatherLocation("高知県", 33.5597, 133.5311),
                new WeatherLocation("高知市", 33.5588, 133.5312)
            },

            ["福岡県"] = new List<WeatherLocation>
            {
                new WeatherLocation("福岡県", 33.5902, 130.4017),
                new WeatherLocation("福岡市", 33.5902, 130.4017),
                new WeatherLocation("北九州市", 33.8834, 130.8751),
                new WeatherLocation("久留米市", 33.3193, 130.5084)
            },
            ["佐賀県"] = new List<WeatherLocation>
            {
                new WeatherLocation("佐賀県", 33.2494, 130.2988),
                new WeatherLocation("佐賀市", 33.2635, 130.3009)
            },
            ["長崎県"] = new List<WeatherLocation>
            {
                new WeatherLocation("長崎県", 32.7503, 129.8777),
                new WeatherLocation("長崎市", 32.7503, 129.8777),
                new WeatherLocation("佐世保市", 33.1799, 129.7151)
            },
            ["熊本県"] = new List<WeatherLocation>
            {
                new WeatherLocation("熊本県", 32.7898, 130.7417),
                new WeatherLocation("熊本市", 32.8031, 130.7079),
                new WeatherLocation("八代市", 32.5070, 130.6017)
            },
            ["大分県"] = new List<WeatherLocation>
            {
                new WeatherLocation("大分県", 33.2382, 131.6126),
                new WeatherLocation("大分市", 33.2396, 131.6093),
                new WeatherLocation("別府市", 33.2846, 131.4912)
            },
            ["宮崎県"] = new List<WeatherLocation>
            {
                new WeatherLocation("宮崎県", 31.9111, 131.4239),
                new WeatherLocation("宮崎市", 31.9077, 131.4202),
                new WeatherLocation("都城市", 31.7196, 131.0616)
            },
            ["鹿児島県"] = new List<WeatherLocation>
            {
                new WeatherLocation("鹿児島県", 31.5602, 130.5581),
                new WeatherLocation("鹿児島市", 31.5966, 130.5571),
                new WeatherLocation("霧島市", 31.7400, 130.7631)
            },
            ["沖縄県"] = new List<WeatherLocation>
            {
                new WeatherLocation("沖縄県", 26.2124, 127.6792),
                new WeatherLocation("那覇市", 26.2124, 127.6792),
                new WeatherLocation("沖縄市", 26.3344, 127.8056),
                new WeatherLocation("石垣市", 24.3407, 124.1556)
            }
        };

        public async Task<WeatherForecastResult?> GetForecastAsync(string? prefecture, string? area)
        {
            try
            {
                WeatherLocation location = GetLocation(prefecture, area);

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

        public async Task<WeatherForecastResult?> GetForecastAsync(string? prefecture)
        {
            return await GetForecastAsync(prefecture, prefecture);
        }

        public async Task<WeatherForecastResult?> GetTokyoForecastAsync()
        {
            return await GetForecastAsync("東京都", "東京都");
        }

        public static IReadOnlyList<string> GetPrefectureNames()
        {
            return new List<string>(LocationsByPrefecture.Keys);
        }

        public static IReadOnlyList<string> GetAreaNames(string? prefecture)
        {
            if (string.IsNullOrWhiteSpace(prefecture) ||
                !LocationsByPrefecture.TryGetValue(prefecture, out List<WeatherLocation>? locations))
            {
                locations = LocationsByPrefecture["東京都"];
            }

            List<string> names = new();

            foreach (WeatherLocation location in locations)
            {
                names.Add(location.Name);
            }

            return names;
        }

        private static WeatherLocation GetLocation(string? prefecture, string? area)
        {
            if (string.IsNullOrWhiteSpace(prefecture) ||
                !LocationsByPrefecture.TryGetValue(prefecture, out List<WeatherLocation>? locations))
            {
                locations = LocationsByPrefecture["東京都"];
            }

            if (!string.IsNullOrWhiteSpace(area))
            {
                foreach (WeatherLocation location in locations)
                {
                    if (location.Name == area)
                    {
                        return location;
                    }
                }
            }

            return locations[0];
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