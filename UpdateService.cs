using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DesktopClock
{
    public sealed class UpdateService
    {
        private const string GitHubOwner = "Rowsai";
        private const string GitHubRepo = "DesktopClock";

        private static readonly Uri LatestReleaseApiUri =
            new($"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest");

        public async Task<UpdateCheckResult> CheckLatestAsync()
        {
            try
            {
                string currentVersionText = GetCurrentVersionText();

                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopClock-Updater");

                string json = await client.GetStringAsync(LatestReleaseApiUri);

                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                string latestTag = root.TryGetProperty("tag_name", out JsonElement tagElement)
                    ? tagElement.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(latestTag))
                {
                    return UpdateCheckResult.Failed(
                        currentVersionText,
                        "GitHub Release の tag_name を取得できませんでした。");
                }

                Version currentVersion = ParseVersion(currentVersionText);
                Version latestVersion = ParseVersion(latestTag);

                string? downloadUrl = null;
                string? assetName = null;

                if (root.TryGetProperty("assets", out JsonElement assetsElement) &&
                    assetsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement asset in assetsElement.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out JsonElement nameElement)
                            ? nameElement.GetString() ?? ""
                            : "";

                        string url = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                            ? urlElement.GetString() ?? ""
                            : "";

                        if (string.IsNullOrWhiteSpace(name) ||
                            string.IsNullOrWhiteSpace(url))
                        {
                            continue;
                        }

                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                            name.Contains("DesktopClock", StringComparison.OrdinalIgnoreCase))
                        {
                            assetName = name;
                            downloadUrl = url;
                            break;
                        }

                        if (downloadUrl == null &&
                            name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            assetName = name;
                            downloadUrl = url;
                        }
                    }
                }

                bool isUpdateAvailable = latestVersion > currentVersion;

                if (isUpdateAvailable && string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return UpdateCheckResult.Failed(
                        currentVersionText,
                        "最新リリースは見つかりましたが、配布用 ZIP が見つかりませんでした。");
                }

                return new UpdateCheckResult
                {
                    Success = true,
                    IsUpdateAvailable = isUpdateAvailable,
                    CurrentVersion = currentVersionText,
                    LatestVersion = latestVersion.ToString(),
                    LatestTag = latestTag,
                    DownloadUrl = downloadUrl ?? "",
                    AssetName = assetName ?? "",
                    Message = ""
                };
            }
            catch (Exception ex)
            {
                return UpdateCheckResult.Failed(
                    GetCurrentVersionText(),
                    ex.Message);
            }
        }

        public async Task DownloadAndInstallAsync(string downloadUrl)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidOperationException("ダウンロードURLが空です。");
            }

            string exePath =
                Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? "";

            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                throw new InvalidOperationException("現在の実行ファイルパスを取得できませんでした。");
            }

            string installDirectory = AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            string updateRoot = Path.Combine(
                Path.GetTempPath(),
                "DesktopClock_Update_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(updateRoot);

            string zipPath = Path.Combine(updateRoot, "DesktopClock_Update.zip");
            string extractDirectory = Path.Combine(updateRoot, "extracted");
            string batPath = Path.Combine(updateRoot, "apply_update.bat");

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopClock-Updater");

            byte[] bytes = await client.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipPath, bytes);

            CreateUpdateBatch(
                batPath,
                zipPath,
                extractDirectory,
                installDirectory,
                exePath,
                Environment.ProcessId);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
        }

        private static void CreateUpdateBatch(
            string batPath,
            string zipPath,
            string extractDirectory,
            string installDirectory,
            string exePath,
            int currentProcessId)
        {
            string psCommand =
                $"Expand-Archive -LiteralPath {ToPowerShellQuoted(zipPath)} -DestinationPath {ToPowerShellQuoted(extractDirectory)} -Force";

            string batch = $"""
@echo off
setlocal

set "ZIP_PATH={zipPath}"
set "EXTRACT_DIR={extractDirectory}"
set "INSTALL_DIR={installDirectory}"
set "EXE_PATH={exePath}"
set "TARGET_PID={currentProcessId}"

timeout /t 1 /nobreak >nul

:WAIT_APP
tasklist /FI "PID eq %TARGET_PID%" | find "%TARGET_PID%" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto WAIT_APP
)

if exist "%EXTRACT_DIR%" (
    rmdir /s /q "%EXTRACT_DIR%"
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "{psCommand}"

if errorlevel 1 (
    echo Update extract failed.
    pause
    exit /b 1
)

xcopy "%EXTRACT_DIR%\*" "%INSTALL_DIR%\" /E /Y /I /Q

if errorlevel 1 (
    echo Update copy failed.
    pause
    exit /b 1
)

start "" "%EXE_PATH%"

timeout /t 2 /nobreak >nul

del "%ZIP_PATH%" >nul 2>nul
rmdir /s /q "%EXTRACT_DIR%" >nul 2>nul

del "%~f0" >nul 2>nul
""";

            File.WriteAllText(batPath, batch, Encoding.Default);
        }

        private static string GetCurrentVersionText()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            AssemblyInformationalVersionAttribute? informationalVersion =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            string version = informationalVersion?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0";

            int plusIndex = version.IndexOf('+');

            if (plusIndex >= 0)
            {
                version = version[..plusIndex];
            }

            return version;
        }

        private static Version ParseVersion(string versionText)
        {
            string normalized = versionText.Trim();

            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[1..];
            }

            int hyphenIndex = normalized.IndexOf('-');

            if (hyphenIndex >= 0)
            {
                normalized = normalized[..hyphenIndex];
            }

            int plusIndex = normalized.IndexOf('+');

            if (plusIndex >= 0)
            {
                normalized = normalized[..plusIndex];
            }

            string[] parts = normalized
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(4)
                .ToArray();

            if (parts.Length == 0)
            {
                return new Version(0, 0, 0, 0);
            }

            int major = GetVersionPart(parts, 0);
            int minor = GetVersionPart(parts, 1);
            int build = GetVersionPart(parts, 2);
            int revision = GetVersionPart(parts, 3);

            return new Version(major, minor, build, revision);
        }

        private static int GetVersionPart(string[] parts, int index)
        {
            if (index >= parts.Length)
            {
                return 0;
            }

            return int.TryParse(parts[index], out int value)
                ? value
                : 0;
        }

        private static string ToPowerShellQuoted(string value)
        {
            return "'" + value.Replace("'", "''") + "'";
        }
    }

    public sealed class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool IsUpdateAvailable { get; set; }

        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string LatestTag { get; set; } = "";

        public string DownloadUrl { get; set; } = "";
        public string AssetName { get; set; } = "";

        public string Message { get; set; } = "";

        public static UpdateCheckResult Failed(string currentVersion, string message)
        {
            return new UpdateCheckResult
            {
                Success = false,
                IsUpdateAvailable = false,
                CurrentVersion = currentVersion,
                Message = message
            };
        }
    }
}