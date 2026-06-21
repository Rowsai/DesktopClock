using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DesktopClock
{
    public partial class MainWindow : Window
    {
        private const string StartupRegistryKey =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const string StartupAppName = "DesktopClock";

        private const double HotbarButtonWidth = 48;
        private const double HotbarButtonHeight = 48;

        private readonly DispatcherTimer timer;
        private readonly AppConfig config;
        private readonly JapaneseHolidayService holidayService = new();
        private readonly WeatherForecastService weatherService = new();
        private readonly UpdateService updateService = new();

        private bool isLoading;
        private bool isUpdatingSizeText;
        private bool isCheckingUpdate;

        private bool isHotbarDragCandidate;
        private bool isDraggingHotbar;
        private bool suppressNextHotbarClick;

        private Point hotbarDragStartMousePoint;
        private double hotbarDragStartX;
        private double hotbarDragStartY;

        private HotbarItemConfig? draggingHotbarItem;
        private Button? draggingHotbarButton;

        private bool isWeatherDragCandidate;
        private bool isDraggingWeather;

        private Point weatherDragStartMousePoint;
        private double weatherDragStartX;
        private double weatherDragStartY;

        public MainWindow()
        {
            InitializeComponent();

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);

            isLoading = true;

            config = AppConfig.Load();
            ApplyConfigToWindow();

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            timer.Tick += Timer_Tick;
            timer.Start();

            UpdateClock();
            UpdateVersionStatusText();

            isLoading = false;

            _ = LoadHolidayAsync();
            _ = RefreshWeatherAsync();

            if (config.AutoCheckUpdates)
            {
                _ = CheckUpdateAsync(showNoUpdateMessage: false);
            }
        }

        private void ApplyConfigToWindow()
        {
            config.EnsureDefaults();

            Width = Math.Max(config.WindowWidth, MinWidth);
            Height = Math.Max(config.WindowHeight, MinHeight);

            Left = config.WindowLeft;
            Top = config.WindowTop;

            Topmost = config.Topmost;
            TopmostCheckBox.IsChecked = config.Topmost;

            StartupCheckBox.IsChecked = IsStartupEnabled();

            AutoUpdateCheckBox.IsChecked = config.AutoCheckUpdates;

            WidthInput.Text = ((int)Width).ToString();
            HeightInput.Text = ((int)Height).ToString();

            BackgroundPathTextBox.Text = config.BackgroundImagePath ?? "";

            if (!string.IsNullOrWhiteSpace(config.BackgroundImagePath) &&
                File.Exists(config.BackgroundImagePath))
            {
                SetBackgroundImage(config.BackgroundImagePath);
            }
            else
            {
                RootGrid.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            }

            ShowWeatherCheckBox.IsChecked = config.ShowWeatherWidget;
            WeatherXInput.Text = ((int)config.WeatherX).ToString();
            WeatherYInput.Text = ((int)config.WeatherY).ToString();

            SetupWeatherPrefectureComboBox();

            LockWeatherMoveCheckBox.IsChecked = config.LockWeatherMove;
            WeatherWidget.Cursor = config.LockWeatherMove
                ? Cursors.Arrow
                : Cursors.SizeAll;

            ShowHotbarCheckBox.IsChecked = config.ShowHotbarWidget;

            config.HotbarItemCount = Math.Clamp(config.HotbarItemCount, 1, 8);
            HotbarItemCountComboBox.SelectedIndex = config.HotbarItemCount - 1;

            LockHotbarMoveCheckBox.IsChecked = config.LockHotbarMove;
            HotbarCanvas.Cursor = config.LockHotbarMove
                ? Cursors.Arrow
                : Cursors.SizeAll;

            EnsureDefaultHotbarItems();
            KeepHotbarItemsInsideWindow();
            RefreshHotbarSettingsList();
            RefreshHotbarWidget();
            ApplyWidgetPositionAndVisibility();

            SettingsPanelTransform.X = SettingsPanel.Width;

            EnsureWindowInsideScreen();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateClock();
        }

        private async Task LoadHolidayAsync()
        {
            await holidayService.LoadAsync();
            Dispatcher.Invoke(UpdateClock);
        }

        private void UpdateClock()
        {
            DateTime now = DateTime.Now;

            TimeText.Text = now.ToString("HH:mm:ss");

            string weekday = GetJapaneseWeekday(now.DayOfWeek);
            string? holidayName = holidayService.GetHolidayName(now);

            bool isHoliday = !string.IsNullOrWhiteSpace(holidayName);
            bool isSaturday = now.DayOfWeek == DayOfWeek.Saturday;
            bool isSunday = now.DayOfWeek == DayOfWeek.Sunday;

            DatePrefixText.Text = now.ToString("yyyy/MM/dd");
            WeekdayText.Text = $"（{weekday}）";

            if (isHoliday || isSunday)
            {
                WeekdayText.Fill = new SolidColorBrush(Color.FromRgb(255, 70, 70));
                WeekdayText.Stroke = Brushes.White;
                WeekdayText.StrokeThickness = 1.4;
                WeekdayText.FontWeight = FontWeights.Bold;
            }
            else if (isSaturday)
            {
                WeekdayText.Fill = new SolidColorBrush(Color.FromRgb(80, 170, 255));
                WeekdayText.Stroke = Brushes.White;
                WeekdayText.StrokeThickness = 1.4;
                WeekdayText.FontWeight = FontWeights.Bold;
            }
            else
            {
                WeekdayText.Fill = Brushes.White;
                WeekdayText.Stroke = Brushes.Transparent;
                WeekdayText.StrokeThickness = 0;
                WeekdayText.FontWeight = FontWeights.Normal;
            }

            if (isHoliday)
            {
                HolidayNameText.Text = holidayName ?? "";
                HolidayNameText.Visibility = Visibility.Visible;
            }
            else
            {
                HolidayNameText.Text = "";
                HolidayNameText.Visibility = Visibility.Collapsed;
            }
        }

        private static string GetJapaneseWeekday(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Sunday => "日",
                DayOfWeek.Monday => "月",
                DayOfWeek.Tuesday => "火",
                DayOfWeek.Wednesday => "水",
                DayOfWeek.Thursday => "木",
                DayOfWeek.Friday => "金",
                DayOfWeek.Saturday => "土",
                _ => ""
            };
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                CloseSettingsPanelWithAnimation();
            }
            else
            {
                OpenSettingsPanelWithAnimation();
            }
        }

        private void OpenSettingsPanelWithAnimation()
        {
            SettingsPanel.Visibility = Visibility.Visible;

            double panelWidth = SettingsPanel.ActualWidth > 0
                ? SettingsPanel.ActualWidth
                : SettingsPanel.Width;

            DoubleAnimation animation = new DoubleAnimation
            {
                From = panelWidth,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            SettingsPanelTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void CloseSettingsPanelWithAnimation()
        {
            double panelWidth = SettingsPanel.ActualWidth > 0
                ? SettingsPanel.ActualWidth
                : SettingsPanel.Width;

            DoubleAnimation animation = new DoubleAnimation
            {
                From = 0,
                To = panelWidth,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };

            animation.Completed += (_, _) =>
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
            };

            SettingsPanelTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(WidthInput.Text, out double newWidth))
            {
                MessageBox.Show(
                    "幅には数値を入力してください。",
                    "入力エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(HeightInput.Text, out double newHeight))
            {
                MessageBox.Show(
                    "高さには数値を入力してください。",
                    "入力エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (newWidth < MinWidth)
            {
                newWidth = MinWidth;
            }

            if (newHeight < MinHeight)
            {
                newHeight = MinHeight;
            }

            Width = newWidth;
            Height = newHeight;

            Topmost = TopmostCheckBox.IsChecked == true;

            config.AutoCheckUpdates = AutoUpdateCheckBox.IsChecked == true;

            SetStartupEnabled(StartupCheckBox.IsChecked == true);

            KeepHotbarItemsInsideWindow();
            RefreshHotbarWidget();
            RefreshHotbarSettingsList();

            SaveCurrentConfig();
        }

        private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsPanelWithAnimation();
        }

        private void SelectBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "背景画像を選択",
                Filter = "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif|すべてのファイル|*.*"
            };

            bool? result = dialog.ShowDialog();

            if (result != true)
            {
                return;
            }

            string selectedPath = dialog.FileName;

            if (!File.Exists(selectedPath))
            {
                MessageBox.Show(
                    "選択した画像ファイルが存在しません。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            SetBackgroundImage(selectedPath);

            BackgroundPathTextBox.Text = selectedPath;
            config.BackgroundImagePath = selectedPath;

            SaveCurrentConfig();
        }

        private void ClearBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            RootGrid.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));

            BackgroundPathTextBox.Text = "";
            config.BackgroundImagePath = null;

            SaveCurrentConfig();
        }

        private void SetBackgroundImage(string imagePath)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                bitmap.Freeze();

                ImageBrush brush = new ImageBrush
                {
                    ImageSource = bitmap,
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
                RenderOptions.SetBitmapScalingMode(RootGrid, BitmapScalingMode.HighQuality);
                RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
                RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);

                RootGrid.Background = brush;
            }
            catch
            {
                MessageBox.Show(
                    "背景画像の読み込みに失敗しました。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                RootGrid.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            }
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            ClockArea.Visibility = Visibility.Collapsed;
            WeatherWidget.Visibility = Visibility.Collapsed;
            HotbarCanvas.Visibility = Visibility.Collapsed;

            SettingsButton.Visibility = Visibility.Collapsed;
            HideButton.Visibility = Visibility.Collapsed;

            SettingsPanelTransform.X = SettingsPanel.ActualWidth > 0
                ? SettingsPanel.ActualWidth
                : SettingsPanel.Width;

            SettingsPanel.Visibility = Visibility.Collapsed;

            ShowButton.Visibility = Visibility.Visible;
        }

        private void ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ClockArea.Visibility = Visibility.Visible;

            SettingsButton.Visibility = Visibility.Visible;
            HideButton.Visibility = Visibility.Visible;

            ShowButton.Visibility = Visibility.Collapsed;

            KeepHotbarItemsInsideWindow();
            ApplyWidgetPositionAndVisibility();
            RefreshHotbarWidget();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                return;
            }

            if (IsDescendantOf(e.OriginalSource as DependencyObject, HotbarCanvas))
            {
                return;
            }

            if (IsDescendantOf(e.OriginalSource as DependencyObject, WeatherWidget))
            {
                return;
            }

            try
            {
                DragMove();
            }
            catch
            {
                // DragMove中の例外は無視
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (isLoading || isUpdatingSizeText)
            {
                return;
            }

            isUpdatingSizeText = true;

            WidthInput.Text = ((int)ActualWidth).ToString();
            HeightInput.Text = ((int)ActualHeight).ToString();

            isUpdatingSizeText = false;

            bool hotbarPositionChanged = KeepHotbarItemsInsideWindow();

            if (hotbarPositionChanged)
            {
                RefreshHotbarWidget();
                RefreshHotbarSettingsList();
            }

            SaveCurrentConfig();
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            if (isLoading)
            {
                return;
            }

            SaveCurrentConfig();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            KeepHotbarItemsInsideWindow();
            SaveCurrentConfig();
        }

        private void EnsureDefaultHotbarItems()
        {
            config.EnsureDefaults();
        }

        private void RefreshHotbarSettingsList()
        {
            HotbarSettingsList.Items.Clear();

            config.HotbarItemCount = Math.Clamp(config.HotbarItemCount, 1, 8);

            int settingCount = Math.Min(config.HotbarItemCount, config.HotbarItems.Count);

            for (int i = 0; i < settingCount; i++)
            {
                int slotIndex = i;
                HotbarItemConfig item = config.HotbarItems[slotIndex];

                Expander expander = new Expander
                {
                    Header = $"スロット {slotIndex + 1}：{(string.IsNullOrWhiteSpace(item.Name) ? "未設定" : item.Name)}",
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(120, 30, 30, 30)),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(8),
                    IsExpanded = false
                };

                StackPanel panel = new StackPanel
                {
                    Margin = new Thickness(4, 8, 4, 4)
                };

                TextBox nameBox = new TextBox
                {
                    Text = item.Name,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                TextBox iconBox = new TextBox
                {
                    Text = item.IconPath,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                Button iconButton = new Button
                {
                    Content = "アイコン選択",
                    Margin = new Thickness(0, 0, 0, 4),
                    Style = (Style)FindResource("SettingsButtonStyle")
                };

                TextBox appBox = new TextBox
                {
                    Text = item.AppPath,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                Button appButton = new Button
                {
                    Content = "アプリ選択",
                    Margin = new Thickness(0, 0, 0, 4),
                    Style = (Style)FindResource("SettingsButtonStyle")
                };

                TextBox argsBox = new TextBox
                {
                    Text = item.Arguments,
                    Margin = new Thickness(0, 0, 0, 4),
                    ToolTip = "起動引数がある場合のみ入力"
                };

                TextBox urlBox = new TextBox
                {
                    Text = item.Url,
                    Margin = new Thickness(0, 0, 0, 4),
                    ToolTip = "例: steam://rungameid/1172470 または https://example.com"
                };

                TextBlock positionText = new TextBlock
                {
                    Text = $"現在位置: X={(int)item.X}, Y={(int)item.Y}",
                    Foreground = Brushes.LightGray,
                    Margin = new Thickness(0, 4, 0, 4)
                };

                nameBox.TextChanged += (_, _) =>
                {
                    item.Name = nameBox.Text;
                    expander.Header = $"スロット {slotIndex + 1}：{(string.IsNullOrWhiteSpace(item.Name) ? "未設定" : item.Name)}";
                    SaveCurrentConfig();
                    RefreshHotbarWidget();
                };

                iconBox.TextChanged += (_, _) =>
                {
                    item.IconPath = iconBox.Text;
                    SaveCurrentConfig();
                    RefreshHotbarWidget();
                };

                appBox.TextChanged += (_, _) =>
                {
                    item.AppPath = appBox.Text;
                    SaveCurrentConfig();
                    RefreshHotbarWidget();
                };

                argsBox.TextChanged += (_, _) =>
                {
                    item.Arguments = argsBox.Text;
                    SaveCurrentConfig();
                };

                urlBox.TextChanged += (_, _) =>
                {
                    item.Url = urlBox.Text;
                    SaveCurrentConfig();
                    RefreshHotbarWidget();
                };

                iconButton.Click += (_, _) =>
                {
                    OpenFileDialog dialog = new OpenFileDialog
                    {
                        Title = "アイコン画像を選択",
                        Filter = "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.ico|すべてのファイル|*.*"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        iconBox.Text = dialog.FileName;
                        item.IconPath = dialog.FileName;

                        SaveCurrentConfig();
                        RefreshHotbarWidget();
                    }
                };

                appButton.Click += (_, _) =>
                {
                    OpenFileDialog dialog = new OpenFileDialog
                    {
                        Title = "起動するアプリを選択",
                        Filter = "実行ファイル|*.exe;*.bat;*.cmd;*.lnk|すべてのファイル|*.*"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        appBox.Text = dialog.FileName;
                        item.AppPath = dialog.FileName;

                        SaveCurrentConfig();
                        RefreshHotbarWidget();
                    }
                };

                panel.Children.Add(CreateSettingsText("表示名"));
                panel.Children.Add(nameBox);

                panel.Children.Add(CreateSettingsText("手動アイコン画像"));
                panel.Children.Add(iconBox);
                panel.Children.Add(iconButton);

                panel.Children.Add(CreateSettingsText("起動アプリ"));
                panel.Children.Add(appBox);
                panel.Children.Add(appButton);

                panel.Children.Add(CreateSettingsText("起動引数"));
                panel.Children.Add(argsBox);

                panel.Children.Add(CreateSettingsText("起動URL"));
                panel.Children.Add(urlBox);

                panel.Children.Add(positionText);

                expander.Content = panel;

                HotbarSettingsList.Items.Add(expander);
            }
        }

        private TextBlock CreateSettingsText(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 6, 0, 2)
            };
        }

        private async void ApplyWidgetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            config.ShowWeatherWidget = ShowWeatherCheckBox.IsChecked == true;
            config.ShowHotbarWidget = ShowHotbarCheckBox.IsChecked == true;

            config.LockWeatherMove = LockWeatherMoveCheckBox.IsChecked == true;
            config.LockHotbarMove = LockHotbarMoveCheckBox.IsChecked == true;

            config.WeatherPrefecture = GetSelectedWeatherPrefecture();
            config.WeatherArea = GetSelectedWeatherArea();

            if (double.TryParse(WeatherXInput.Text, out double weatherX))
            {
                config.WeatherX = weatherX;
            }

            if (double.TryParse(WeatherYInput.Text, out double weatherY))
            {
                config.WeatherY = weatherY;
            }

            config.HotbarItemCount = GetSelectedHotbarItemCount();

            WeatherWidget.Cursor = config.LockWeatherMove
                ? Cursors.Arrow
                : Cursors.SizeAll;

            HotbarCanvas.Cursor = config.LockHotbarMove
                ? Cursors.Arrow
                : Cursors.SizeAll;

            KeepHotbarItemsInsideWindow();
            RefreshHotbarSettingsList();
            RefreshHotbarWidget();
            ApplyWidgetPositionAndVisibility();

            SaveCurrentConfig();

            await RefreshWeatherAsync();
        }

        private void ApplyWidgetPositionAndVisibility()
        {
            WeatherWidget.Margin = new Thickness(config.WeatherX, config.WeatherY, 0, 0);

            WeatherWidget.Visibility = config.ShowWeatherWidget
                ? Visibility.Visible
                : Visibility.Collapsed;

            HotbarCanvas.Visibility = config.ShowHotbarWidget
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void RefreshWeatherButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshWeatherAsync();
        }

        private async Task RefreshWeatherAsync()
        {
            WeatherCurrentText.Text = "現在：取得中";
            WeatherTodayText.Text = "今日：取得中";
            WeatherTomorrowText.Text = "明日：取得中";

            string selectedPrefecture = GetSelectedWeatherPrefecture();
            string selectedArea = GetSelectedWeatherArea();

            WeatherForecastResult? forecast =
                await weatherService.GetForecastAsync(selectedPrefecture, selectedArea);

            if (forecast == null)
            {
                WeatherAreaText.Text = selectedArea;
                WeatherCurrentText.Text = "現在：取得失敗";
                WeatherTodayText.Text = "今日：取得失敗";
                WeatherTomorrowText.Text = "明日：取得失敗";
                return;
            }

            WeatherAreaText.Text = forecast.AreaName;

            WeatherCurrentText.Text =
                $"現在：{forecast.CurrentWeather} / {forecast.CurrentTemperature:0.#}℃";

            WeatherTodayText.Text =
                $"今日：{forecast.TodayWeather} / 最高 {forecast.TodayMaxTemperature:0.#}℃ / 最低 {forecast.TodayMinTemperature:0.#}℃";

            WeatherTomorrowText.Text =
                $"明日：{forecast.TomorrowWeather} / 最高 {forecast.TomorrowMaxTemperature:0.#}℃ / 最低 {forecast.TomorrowMinTemperature:0.#}℃";
        }

        private void SetupWeatherPrefectureComboBox()
        {
            WeatherPrefectureComboBox.Items.Clear();

            foreach (string prefecture in WeatherForecastService.GetPrefectureNames())
            {
                WeatherPrefectureComboBox.Items.Add(prefecture);
            }

            if (!string.IsNullOrWhiteSpace(config.WeatherPrefecture) &&
                WeatherPrefectureComboBox.Items.Contains(config.WeatherPrefecture))
            {
                WeatherPrefectureComboBox.SelectedItem = config.WeatherPrefecture;
            }
            else
            {
                WeatherPrefectureComboBox.SelectedItem = "東京都";
                config.WeatherPrefecture = "東京都";
            }

            SetupWeatherAreaComboBox();
        }

        private void SetupWeatherAreaComboBox()
        {
            string prefecture = GetSelectedWeatherPrefecture();

            WeatherAreaComboBox.Items.Clear();

            foreach (string area in WeatherForecastService.GetAreaNames(prefecture))
            {
                WeatherAreaComboBox.Items.Add(area);
            }

            if (!string.IsNullOrWhiteSpace(config.WeatherArea) &&
                WeatherAreaComboBox.Items.Contains(config.WeatherArea))
            {
                WeatherAreaComboBox.SelectedItem = config.WeatherArea;
            }
            else
            {
                if (WeatherAreaComboBox.Items.Count > 0)
                {
                    WeatherAreaComboBox.SelectedIndex = 0;

                    if (WeatherAreaComboBox.SelectedItem is string selectedArea)
                    {
                        config.WeatherArea = selectedArea;
                    }
                }
            }
        }

        private void WeatherPrefectureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoading)
            {
                return;
            }

            config.WeatherPrefecture = GetSelectedWeatherPrefecture();

            config.WeatherArea = "";

            SetupWeatherAreaComboBox();

            SaveCurrentConfig();
        }

        private string GetSelectedWeatherPrefecture()
        {
            if (WeatherPrefectureComboBox.SelectedItem is string prefecture &&
                !string.IsNullOrWhiteSpace(prefecture))
            {
                return prefecture;
            }

            if (!string.IsNullOrWhiteSpace(config.WeatherPrefecture))
            {
                return config.WeatherPrefecture;
            }

            return "東京都";
        }

        private string GetSelectedWeatherArea()
        {
            if (WeatherAreaComboBox.SelectedItem is string area &&
                !string.IsNullOrWhiteSpace(area))
            {
                return area;
            }

            if (!string.IsNullOrWhiteSpace(config.WeatherArea))
            {
                return config.WeatherArea;
            }

            return GetSelectedWeatherPrefecture();
        }

        private void HotbarItemCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoading)
            {
                return;
            }

            config.HotbarItemCount = GetSelectedHotbarItemCount();

            KeepHotbarItemsInsideWindow();
            RefreshHotbarSettingsList();
            RefreshHotbarWidget();

            SaveCurrentConfig();
        }

        private void RefreshHotbarWidget()
        {
            HotbarCanvas.Children.Clear();

            KeepHotbarItemsInsideWindow();

            config.HotbarItemCount = Math.Clamp(config.HotbarItemCount, 1, 8);

            int displayCount = Math.Min(config.HotbarItemCount, config.HotbarItems.Count);

            for (int i = 0; i < displayCount; i++)
            {
                int index = i;
                HotbarItemConfig item = config.HotbarItems[i];

                Button button = new Button
                {
                    Width = HotbarButtonWidth,
                    Height = HotbarButtonHeight,
                    ToolTip = BuildHotbarToolTip(item, index),
                    Cursor = config.LockHotbarMove
                        ? Cursors.Hand
                        : Cursors.SizeAll
                };

                FrameworkElement content = CreateHotbarButtonContent(item, index);
                button.Content = content;

                button.PreviewMouseLeftButtonDown += (_, e) =>
                    HotbarButton_PreviewMouseLeftButtonDown(button, item, e);

                button.PreviewMouseMove += (_, e) =>
                    HotbarButton_PreviewMouseMove(button, item, e);

                button.PreviewMouseLeftButtonUp += (_, e) =>
                    HotbarButton_PreviewMouseLeftButtonUp(button, item, e);

                button.Click += (_, _) =>
                {
                    if (suppressNextHotbarClick)
                    {
                        suppressNextHotbarClick = false;
                        return;
                    }

                    ExecuteHotbarItem(item);
                };

                Canvas.SetLeft(button, item.X);
                Canvas.SetTop(button, item.Y);

                HotbarCanvas.Children.Add(button);
            }
        }

        private bool KeepHotbarItemsInsideWindow()
        {
            bool changed = false;

            if (config.HotbarItems == null || config.HotbarItems.Count == 0)
            {
                return false;
            }

            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : Height;

            double maxX = Math.Max(0, width - HotbarButtonWidth);
            double maxY = Math.Max(0, height - HotbarButtonHeight);

            foreach (HotbarItemConfig item in config.HotbarItems)
            {
                double oldX = item.X;
                double oldY = item.Y;

                item.X = Math.Max(0, Math.Min(item.X, maxX));
                item.Y = Math.Max(0, Math.Min(item.Y, maxY));

                if (Math.Abs(oldX - item.X) > 0.1 ||
                    Math.Abs(oldY - item.Y) > 0.1)
                {
                    changed = true;
                }
            }

            return changed;
        }

        private FrameworkElement CreateHotbarButtonContent(HotbarItemConfig item, int index)
        {
            bool hasApp = !string.IsNullOrWhiteSpace(item.AppPath);
            bool hasValidApp = hasApp && File.Exists(item.AppPath);
            bool hasUrl = !string.IsNullOrWhiteSpace(item.Url);
            bool hasManualIcon =
                !string.IsNullOrWhiteSpace(item.IconPath) &&
                File.Exists(item.IconPath);

            if (hasValidApp)
            {
                ImageSource? appIcon = TryGetAssociatedIconImageSource(item.AppPath);

                if (appIcon != null)
                {
                    Image image = new Image
                    {
                        Source = appIcon,
                        Width = 32,
                        Height = 32,
                        Stretch = Stretch.Uniform
                    };

                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                    return image;
                }

                return CreateHotbarTextBlock(Path.GetFileNameWithoutExtension(item.AppPath));
            }

            if (hasUrl && hasManualIcon)
            {
                return CreateManualHotbarIcon(item.IconPath);
            }

            if (hasUrl)
            {
                if (IsSteamUrl(item.Url))
                {
                    return CreateHotbarTextBlock("Steam");
                }

                if (IsWebUrl(item.Url))
                {
                    return CreateHotbarTextBlock("WEB");
                }

                return CreateHotbarTextBlock("URL");
            }

            if (hasManualIcon)
            {
                return CreateManualHotbarIcon(item.IconPath);
            }

            string text = string.IsNullOrWhiteSpace(item.Name)
                ? $"{index + 1}"
                : item.Name;

            return CreateHotbarTextBlock(text);
        }

        private FrameworkElement CreateManualHotbarIcon(string iconPath)
        {
            BitmapImage bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.DecodePixelWidth = 96;
            bitmap.EndInit();
            bitmap.Freeze();

            Image image = new Image
            {
                Stretch = Stretch.UniformToFill,
                Source = bitmap
            };

            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            return image;
        }

        private TextBlock CreateHotbarTextBlock(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private string BuildHotbarToolTip(HotbarItemConfig item, int index)
        {
            string name = string.IsNullOrWhiteSpace(item.Name)
                ? $"Slot {index + 1}"
                : item.Name;

            bool hasApp = !string.IsNullOrWhiteSpace(item.AppPath);
            bool hasUrl = !string.IsNullOrWhiteSpace(item.Url);

            string tooltip = name;

            if (hasApp)
            {
                tooltip += $"\n起動アプリ：{item.AppPath}";
            }

            if (hasUrl)
            {
                tooltip += $"\n起動URL：{item.Url}";
            }

            if (hasApp)
            {
                tooltip += "\n実行優先：起動アプリ";
            }
            else if (hasUrl)
            {
                tooltip += "\n実行優先：URL";
            }

            tooltip += $"\n位置：X={(int)item.X}, Y={(int)item.Y}";

            return tooltip;
        }

        private ImageSource? TryGetAssociatedIconImageSource(string appPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(appPath) || !File.Exists(appPath))
                {
                    return null;
                }

                using System.Drawing.Icon? icon = System.Drawing.Icon.ExtractAssociatedIcon(appPath);

                if (icon == null)
                {
                    return null;
                }

                BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(32, 32));

                source.Freeze();

                return source;
            }
            catch
            {
                return null;
            }
        }

        private void ExecuteHotbarItem(HotbarItemConfig item)
        {
            bool hasApp = !string.IsNullOrWhiteSpace(item.AppPath);
            bool hasValidApp = hasApp && File.Exists(item.AppPath);
            bool hasUrl = !string.IsNullOrWhiteSpace(item.Url);

            if (hasValidApp)
            {
                LaunchHotbarItem(item);
                return;
            }

            if (hasApp && !hasValidApp)
            {
                MessageBox.Show(
                    "設定された起動アプリが見つかりません。",
                    "ホットバー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (hasUrl)
            {
                LaunchHotbarUrl(item.Url);
                return;
            }

            MessageBox.Show(
                "起動アプリまたはURLが設定されていません。",
                "ホットバー",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void LaunchHotbarItem(HotbarItemConfig item)
        {
            if (string.IsNullOrWhiteSpace(item.AppPath))
            {
                MessageBox.Show(
                    "起動アプリが設定されていません。",
                    "ホットバー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!File.Exists(item.AppPath))
            {
                MessageBox.Show(
                    "設定されたアプリが見つかりません。",
                    "ホットバー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = item.AppPath,
                    Arguments = item.Arguments ?? "",
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"アプリの起動に失敗しました。\n{ex.Message}",
                    "ホットバー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LaunchHotbarUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(
                    "URLが設定されていません。",
                    "ホットバー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string trimmedUrl = url.Trim();

            try
            {
                if (IsWebUrl(trimmedUrl))
                {
                    LaunchWebUrlWithChrome(trimmedUrl);
                    return;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = trimmedUrl,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"URLの起動に失敗しました。\n{trimmedUrl}\n\n{ex.Message}",
                    "ホットバー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LaunchWebUrlWithChrome(string url)
        {
            string? chromePath = FindChromePath();

            try
            {
                if (!string.IsNullOrWhiteSpace(chromePath) &&
                    File.Exists(chromePath))
                {
                    ProcessStartInfo chromeStartInfo = new ProcessStartInfo
                    {
                        FileName = chromePath,
                        Arguments = $"\"{url}\"",
                        UseShellExecute = true
                    };

                    Process.Start(chromeStartInfo);
                    return;
                }

                ProcessStartInfo fallbackStartInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };

                Process.Start(fallbackStartInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"URLをブラウザで開けませんでした。\n{url}\n\n{ex.Message}",
                    "ホットバー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string? FindChromePath()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string[] candidates =
            {
                Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsWebUrl(string url)
        {
            return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSteamUrl(string url)
        {
            return url.StartsWith("steam://", StringComparison.OrdinalIgnoreCase);
        }

        private int GetSelectedHotbarItemCount()
        {
            if (HotbarItemCountComboBox.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Content is string content &&
                int.TryParse(content, out int count))
            {
                return Math.Clamp(count, 1, 8);
            }

            return Math.Clamp(config.HotbarItemCount, 1, 8);
        }

        private void HotbarButton_PreviewMouseLeftButtonDown(
            Button button,
            HotbarItemConfig item,
            MouseButtonEventArgs e)
        {
            if (config.LockHotbarMove || LockHotbarMoveCheckBox.IsChecked == true)
            {
                return;
            }

            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                return;
            }

            isHotbarDragCandidate = true;
            isDraggingHotbar = false;
            suppressNextHotbarClick = false;

            draggingHotbarButton = button;
            draggingHotbarItem = item;

            hotbarDragStartMousePoint = e.GetPosition(RootGrid);
            hotbarDragStartX = item.X;
            hotbarDragStartY = item.Y;
        }

        private void HotbarButton_PreviewMouseMove(
            Button button,
            HotbarItemConfig item,
            MouseEventArgs e)
        {
            if (!isHotbarDragCandidate && !isDraggingHotbar)
            {
                return;
            }

            if (draggingHotbarButton != button || draggingHotbarItem != item)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndHotbarButtonDrag(save: false);
                return;
            }

            Point currentPoint = e.GetPosition(RootGrid);

            double diffX = currentPoint.X - hotbarDragStartMousePoint.X;
            double diffY = currentPoint.Y - hotbarDragStartMousePoint.Y;

            double distance = Math.Sqrt((diffX * diffX) + (diffY * diffY));

            if (!isDraggingHotbar && distance < 4)
            {
                return;
            }

            if (!isDraggingHotbar)
            {
                isDraggingHotbar = true;
                suppressNextHotbarClick = true;
                button.CaptureMouse();
            }

            double newX = hotbarDragStartX + diffX;
            double newY = hotbarDragStartY + diffY;

            double buttonWidth = button.ActualWidth > 0
                ? button.ActualWidth
                : HotbarButtonWidth;

            double buttonHeight = button.ActualHeight > 0
                ? button.ActualHeight
                : HotbarButtonHeight;

            newX = Math.Max(0, Math.Min(newX, Math.Max(0, ActualWidth - buttonWidth)));
            newY = Math.Max(0, Math.Min(newY, Math.Max(0, ActualHeight - buttonHeight)));

            item.X = newX;
            item.Y = newY;

            Canvas.SetLeft(button, newX);
            Canvas.SetTop(button, newY);

            e.Handled = true;
        }

        private void HotbarButton_PreviewMouseLeftButtonUp(
            Button button,
            HotbarItemConfig item,
            MouseButtonEventArgs e)
        {
            if (!isHotbarDragCandidate && !isDraggingHotbar)
            {
                return;
            }

            if (draggingHotbarButton != button || draggingHotbarItem != item)
            {
                return;
            }

            bool moved = isDraggingHotbar;

            EndHotbarButtonDrag(save: moved);

            if (moved)
            {
                e.Handled = true;
            }
        }

        private void EndHotbarButtonDrag(bool save)
        {
            if (draggingHotbarButton != null && draggingHotbarButton.IsMouseCaptured)
            {
                draggingHotbarButton.ReleaseMouseCapture();
            }

            isHotbarDragCandidate = false;
            isDraggingHotbar = false;

            draggingHotbarButton = null;
            draggingHotbarItem = null;

            if (save)
            {
                KeepHotbarItemsInsideWindow();
                SaveCurrentConfig();
                RefreshHotbarSettingsList();
            }
        }

        private void WeatherWidget_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (config.LockWeatherMove || LockWeatherMoveCheckBox.IsChecked == true)
            {
                return;
            }

            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                return;
            }

            isWeatherDragCandidate = true;
            isDraggingWeather = false;

            weatherDragStartMousePoint = e.GetPosition(RootGrid);
            weatherDragStartX = config.WeatherX;
            weatherDragStartY = config.WeatherY;
        }

        private void WeatherWidget_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!isWeatherDragCandidate && !isDraggingWeather)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndWeatherDrag(save: false);
                return;
            }

            Point currentPoint = e.GetPosition(RootGrid);

            double diffX = currentPoint.X - weatherDragStartMousePoint.X;
            double diffY = currentPoint.Y - weatherDragStartMousePoint.Y;

            double distance = Math.Sqrt((diffX * diffX) + (diffY * diffY));

            if (!isDraggingWeather && distance < 4)
            {
                return;
            }

            if (!isDraggingWeather)
            {
                isDraggingWeather = true;
                WeatherWidget.CaptureMouse();
            }

            double newX = weatherDragStartX + diffX;
            double newY = weatherDragStartY + diffY;

            newX = Math.Max(0, Math.Min(newX, Math.Max(0, ActualWidth - WeatherWidget.ActualWidth)));
            newY = Math.Max(0, Math.Min(newY, Math.Max(0, ActualHeight - WeatherWidget.ActualHeight)));

            config.WeatherX = newX;
            config.WeatherY = newY;

            WeatherXInput.Text = ((int)newX).ToString();
            WeatherYInput.Text = ((int)newY).ToString();

            ApplyWidgetPositionAndVisibility();

            e.Handled = true;
        }

        private void WeatherWidget_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isWeatherDragCandidate && !isDraggingWeather)
            {
                return;
            }

            bool moved = isDraggingWeather;

            EndWeatherDrag(save: moved);

            if (moved)
            {
                e.Handled = true;
            }
        }

        private void EndWeatherDrag(bool save)
        {
            if (WeatherWidget.IsMouseCaptured)
            {
                WeatherWidget.ReleaseMouseCapture();
            }

            isWeatherDragCandidate = false;
            isDraggingWeather = false;

            if (save)
            {
                SaveCurrentConfig();
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckUpdateAsync(showNoUpdateMessage: true);
        }

        private async Task CheckUpdateAsync(bool showNoUpdateMessage)
        {
            if (isCheckingUpdate)
            {
                return;
            }

            isCheckingUpdate = true;
            CheckUpdateButton.IsEnabled = false;
            UpdateStatusText.Text = "アップデート確認中...";

            try
            {
                UpdateCheckResult result = await updateService.CheckLatestAsync();

                if (!result.Success)
                {
                    UpdateStatusText.Text =
                        $"アップデート確認失敗：{result.Message}";

                    if (showNoUpdateMessage)
                    {
                        MessageBox.Show(
                            $"アップデート確認に失敗しました。\n\n{result.Message}",
                            "アップデート",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    return;
                }

                if (!result.IsUpdateAvailable)
                {
                    UpdateStatusText.Text =
                        $"最新です。現在のバージョン：{result.CurrentVersion}";

                    if (showNoUpdateMessage)
                    {
                        MessageBox.Show(
                            $"現在のバージョンは最新です。\n\n現在：{result.CurrentVersion}\n最新：{result.LatestVersion}",
                            "アップデート",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }

                    return;
                }

                UpdateStatusText.Text =
                    $"新しいバージョンがあります。現在：{result.CurrentVersion} / 最新：{result.LatestVersion}";

                MessageBoxResult answer = MessageBox.Show(
                    $"新しいバージョンがあります。\n\n現在：{result.CurrentVersion}\n最新：{result.LatestVersion}\n\nアップデートしますか？\n\n{result.AssetName}",
                    "アップデート",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }

                UpdateStatusText.Text = "アップデートをダウンロード中...";

                await updateService.DownloadAndInstallAsync(result.DownloadUrl);

                SaveCurrentConfig();

                MessageBox.Show(
                    "アップデートを開始します。\nアプリを一度終了し、更新後に自動で再起動します。",
                    "アップデート",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Close();
            }
            finally
            {
                isCheckingUpdate = false;

                if (CheckUpdateButton != null)
                {
                    CheckUpdateButton.IsEnabled = true;
                }
            }
        }

        private void UpdateVersionStatusText()
        {
            string version = GetCurrentVersionText();

            UpdateStatusText.Text = $"現在のバージョン：{version}";
            AppVersionText.Text = $"Version {version}";
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

        private void SaveCurrentConfig()
        {
            if (isLoading)
            {
                return;
            }

            config.EnsureDefaults();

            config.WindowWidth = ActualWidth;
            config.WindowHeight = ActualHeight;
            config.WindowLeft = Left;
            config.WindowTop = Top;
            config.Topmost = Topmost;
            config.BackgroundImagePath = string.IsNullOrWhiteSpace(BackgroundPathTextBox.Text)
                ? null
                : BackgroundPathTextBox.Text;

            config.AutoCheckUpdates = AutoUpdateCheckBox.IsChecked == true;

            config.ShowWeatherWidget = ShowWeatherCheckBox.IsChecked == true;
            config.ShowHotbarWidget = ShowHotbarCheckBox.IsChecked == true;

            config.LockWeatherMove = LockWeatherMoveCheckBox.IsChecked == true;
            config.LockHotbarMove = LockHotbarMoveCheckBox.IsChecked == true;

            config.WeatherPrefecture = GetSelectedWeatherPrefecture();
            config.WeatherArea = GetSelectedWeatherArea();
            config.HotbarItemCount = GetSelectedHotbarItemCount();

            if (double.TryParse(WeatherXInput.Text, out double weatherX))
            {
                config.WeatherX = weatherX;
            }

            if (double.TryParse(WeatherYInput.Text, out double weatherY))
            {
                config.WeatherY = weatherY;
            }

            KeepHotbarItemsInsideWindow();

            config.Save();
        }

        private void EnsureWindowInsideScreen()
        {
            double screenWidth = SystemParameters.VirtualScreenWidth;
            double screenHeight = SystemParameters.VirtualScreenHeight;
            double screenLeft = SystemParameters.VirtualScreenLeft;
            double screenTop = SystemParameters.VirtualScreenTop;

            if (Left < screenLeft)
            {
                Left = screenLeft;
            }

            if (Top < screenTop)
            {
                Top = screenTop;
            }

            if (Left > screenLeft + screenWidth - 100)
            {
                Left = screenLeft + 100;
            }

            if (Top > screenTop + screenHeight - 100)
            {
                Top = screenTop + 100;
            }
        }

        private void SetStartupEnabled(bool enabled)
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                StartupRegistryKey,
                writable: true);

            if (key == null)
            {
                return;
            }

            if (enabled)
            {
                string exePath =
                    Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule?.FileName
                    ?? "";

                if (string.IsNullOrWhiteSpace(exePath))
                {
                    return;
                }

                key.SetValue(StartupAppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(StartupAppName, throwOnMissingValue: false);
            }
        }

        private bool IsStartupEnabled()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                StartupRegistryKey,
                writable: false);

            if (key == null)
            {
                return false;
            }

            object? value = key.GetValue(StartupAppName);
            return value != null;
        }

        private static bool IsDescendantOf(DependencyObject? child, DependencyObject parent)
        {
            while (child != null)
            {
                if (child == parent)
                {
                    return true;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return false;
        }
    }
}