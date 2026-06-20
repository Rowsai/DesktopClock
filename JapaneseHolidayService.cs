using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DesktopClock
{
    public sealed class JapaneseHolidayService
    {
        private const string HolidayCsvUrl =
            "https://www8.cao.go.jp/chosei/shukujitsu/syukujitsu.csv";

        private readonly Dictionary<DateOnly, string> holidays = new();

        private static string CacheDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "DesktopClock");
            }
        }

        private static string CachePath => Path.Combine(CacheDirectory, "syukujitsu.csv");

        public async Task LoadAsync()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            string? csvText = null;

            try
            {
                using HttpClient client = new HttpClient();
                byte[] bytes = await client.GetByteArrayAsync(HolidayCsvUrl);

                Encoding shiftJis = Encoding.GetEncoding("shift_jis");
                csvText = shiftJis.GetString(bytes);

                Directory.CreateDirectory(CacheDirectory);
                await File.WriteAllTextAsync(CachePath, csvText, Encoding.UTF8);
            }
            catch
            {
                if (File.Exists(CachePath))
                {
                    csvText = await File.ReadAllTextAsync(CachePath, Encoding.UTF8);
                }
            }

            if (string.IsNullOrWhiteSpace(csvText))
            {
                return;
            }

            ParseCsv(csvText);
        }

        public string? GetHolidayName(DateTime dateTime)
        {
            DateOnly date = DateOnly.FromDateTime(dateTime.Date);

            return holidays.TryGetValue(date, out string? holidayName)
                ? holidayName
                : null;
        }

        private void ParseCsv(string csvText)
        {
            holidays.Clear();

            string[] lines = csvText.Replace("\r\n", "\n").Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("国民の祝日"))
                {
                    continue;
                }

                string[] parts = line.Split(',', 2);

                if (parts.Length < 2)
                {
                    continue;
                }

                string dateText = parts[0].Trim();
                string name = parts[1].Trim();

                if (DateOnly.TryParseExact(
                        dateText,
                        "yyyy/M/d",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateOnly date))
                {
                    holidays[date] = name;
                }
            }
        }
    }
}