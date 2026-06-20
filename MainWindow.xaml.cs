using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DesktopClock
{
    public partial class MainWindow : Window
    {
        private const string StartupRegistryKey =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const string StartupAppName = "DesktopClock";

        private readonly DispatcherTimer timer;
        private readonly AppConfig config;
        private readonly JapaneseHolidayService holidayService = new();
        private readonly WeatherForecastService weatherService = new();

        private bool isLoading;
        private bool isUpdatingSizeText;

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

            isLoading = false;

            _ = LoadHolidayAsync();
            _ = RefreshWeatherAsync();
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
            RefreshHotbarSettingsList();
            RefreshHotbarWidget();
            ApplyWidgetPositionAndVisibility();

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
            SettingsPanel.Visibility =
                SettingsPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
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

            SetStartupEnabled(StartupCheckBox.IsChecked == true);

            SaveCurrentConfig();
        }

        private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
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
            SettingsPanel.Visibility = Visibility.Collapsed;

            ShowButton.Visibility = Visibility.Visible;
        }

        private void ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ClockArea.Visibility = Visibility.Visible;

            SettingsButton.Visibility = Visibility.Visible;
            HideButton.Visibility = Visibility.Visible;

            ShowButton.Visibility = Visibility.Collapsed;

            ApplyWidgetPositionAndVisibility();
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
            SaveCurrentConfig();
        }

        private void EnsureDefaultHotbarItems()
        {
            config.EnsureDefaults();
        }

        private void RefreshHotbarSettingsList()
        {
            HotbarSettingsList.Items.Clear();

            for (int i = 0; i < config.HotbarItems.Count; i++)
            {
                HotbarItemConfig item = config.HotbarItems[i];

                Border border = new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 0, 0, 8),
                    Background = new SolidColorBrush(Color.FromArgb(120, 30, 30, 30))
                };

                StackPanel panel = new StackPanel();

                TextBlock title = new TextBlock
                {
                    Text = $"スロット {i + 1}",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 6)
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
                    Margin = new Thickness(0, 0, 0, 4)
                };

                TextBox hotKeyBox = new TextBox
                {
                    Text = item.HotKey,
                    Margin = new Thickness(0, 0, 0, 4),
                    ToolTip = "例: Ctrl+Alt+1"
                };

                TextBox appBox = new TextBox
                {
                    Text = item.AppPath,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                Button appButton = new Button
                {
                    Content = "アプリ選択",
                    Margin = new Thickness(0, 0, 0, 4)
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
                    SaveCurrentConfig();
                    RefreshHotbarWidget();
                };

                iconBox.TextChanged += (_, _) =>
                {
                    item.IconPath = iconBox.Text;
                    SaveCurrentConfig();
                    RefreshHotbarWidget();
                };

                hotKeyBox.TextChanged += (_, _) =>
                {
                    item.HotKey = hotKeyBox.Text;
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

                panel.Children.Add(title);

                panel.Children.Add(new TextBlock
                {
                    Text = "表示名",
                    Foreground = Brushes.White
                });
                panel.Children.Add(nameBox);

                panel.Children.Add(new TextBlock
                {
                    Text = "手動アイコン画像",
                    Foreground = Brushes.White
                });
                panel.Children.Add(iconBox);
                panel.Children.Add(iconButton);

                panel.Children.Add(new TextBlock
                {
                    Text = "ホットキー",
                    Foreground = Brushes.White
                });
                panel.Children.Add(hotKeyBox);

                panel.Children.Add(new TextBlock
                {
                    Text = "起動アプリ",
                    Foreground = Brushes.White
                });
                panel.Children.Add(appBox);
                panel.Children.Add(appButton);

                panel.Children.Add(new TextBlock
                {
                    Text = "起動引数",
                    Foreground = Brushes.White
                });
                panel.Children.Add(argsBox);
                
                panel.Children.Add(new TextBlock
                {
                    Text = "起動URL",
                    Foreground = Brushes.White
                });
                panel.Children.Add(urlBox);

                panel.Children.Add(positionText);

                border.Child = panel;
                HotbarSettingsList.Items.Add(border);
            }
        }

        private async void ApplyWidgetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            config.ShowWeatherWidget = ShowWeatherCheckBox.IsChecked == true;
            config.ShowHotbarWidget = ShowHotbarCheckBox.IsChecked == true;

            config.LockWeatherMove = LockWeatherMoveCheckBox.IsChecked == true;
            config.LockHotbarMove = LockHotbarMoveCheckBox.IsChecked == true;

            config.WeatherPrefecture = GetSelectedWeatherPrefecture();

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

            WeatherForecastResult? forecast =
                await weatherService.GetForecastAsync(selectedPrefecture);

            if (forecast == null)
            {
                WeatherAreaText.Text = selectedPrefecture;
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

        private void RefreshHotbarWidget()
        {
            HotbarCanvas.Children.Clear();

            config.HotbarItemCount = Math.Clamp(config.HotbarItemCount, 1, 8);

            int displayCount = Math.Min(config.HotbarItemCount, config.HotbarItems.Count);

            for (int i = 0; i < displayCount; i++)
            {
                int index = i;
                HotbarItemConfig item = config.HotbarItems[i];

                Button button = new Button
                {
                    Width = 48,
                    Height = 48,
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

        private FrameworkElement CreateHotbarButtonContent(HotbarItemConfig item, int index)
        {
            bool hasApp = !string.IsNullOrWhiteSpace(item.AppPath);
            bool hasValidApp = hasApp && File.Exists(item.AppPath);
            bool hasUrl = !string.IsNullOrWhiteSpace(item.Url);
            bool hasHotKey = !string.IsNullOrWhiteSpace(item.HotKey);
            bool hasManualIcon =
                !string.IsNullOrWhiteSpace(item.IconPath) &&
                File.Exists(item.IconPath);

            // 1. 起動アプリが設定されている場合は、起動アプリのアイコンを最優先
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

            // 2. 起動アプリがなく、URLが設定されていて、手動アイコンもある場合
            //    → 手動アイコンを表示する
            if (hasUrl && hasManualIcon)
            {
                return CreateManualHotbarIcon(item.IconPath);
            }

            // 3. 起動アプリがなく、URLだけ設定されている場合
            //    → URL種別の文字を表示する
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

            // 4. URLも起動アプリもなく、ホットキーだけ設定されている場合
            //    → ホットキー文字列を表示
            if (hasHotKey)
            {
                return CreateHotbarTextBlock(item.HotKey);
            }

            // 5. 起動アプリ、URL、ホットキーがなく、手動アイコンがある場合
            //    → 手動アイコンを表示
            if (hasManualIcon)
            {
                return CreateManualHotbarIcon(item.IconPath);
            }

            // 6. 何もなければ表示名、なければ番号
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
            bool hasHotKey = !string.IsNullOrWhiteSpace(item.HotKey);

            string tooltip = name;

            if (hasApp)
            {
                tooltip += $"\n起動アプリ：{item.AppPath}";
            }

            if (hasUrl)
            {
                tooltip += $"\n起動URL：{item.Url}";
            }

            if (hasHotKey)
            {
                tooltip += $"\nホットキー：{item.HotKey}";
            }

            if (hasApp)
            {
                tooltip += "\n実行優先：起動アプリ";
            }
            else if (hasUrl)
            {
                tooltip += "\n実行優先：URL";
            }
            else if (hasHotKey)
            {
                tooltip += "\n実行優先：ホットキー";
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
            bool hasHotKey = !string.IsNullOrWhiteSpace(item.HotKey);

            // 1. 起動アプリがある場合は最優先
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

            // 2. URLがある場合
            if (hasUrl)
            {
                LaunchHotbarUrl(item.Url);
                return;
            }

            // 3. ホットキーがある場合
            if (hasHotKey)
            {
                MessageBox.Show(
                    $"このスロットにはホットキーが設定されています。\n\n{item.HotKey}\n\n現在の実装では、ホットキーは表示用です。",
                    "ホットバー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBox.Show(
                "起動アプリ、URL、ホットキーが設定されていません。",
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

                // steam:// などのカスタムURLスキームは Windows に任せる
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

                // Chromeが見つからない場合は既定ブラウザで開く
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

            newX = Math.Max(0, Math.Min(newX, Math.Max(0, ActualWidth - button.ActualWidth)));
            newY = Math.Max(0, Math.Min(newY, Math.Max(0, ActualHeight - button.ActualHeight)));

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

            config.ShowWeatherWidget = ShowWeatherCheckBox.IsChecked == true;
            config.ShowHotbarWidget = ShowHotbarCheckBox.IsChecked == true;

            config.LockWeatherMove = LockWeatherMoveCheckBox.IsChecked == true;
            config.LockHotbarMove = LockHotbarMoveCheckBox.IsChecked == true;
            config.WeatherPrefecture = GetSelectedWeatherPrefecture();

            config.HotbarItemCount = GetSelectedHotbarItemCount();

            if (double.TryParse(WeatherXInput.Text, out double weatherX))
            {
                config.WeatherX = weatherX;
            }

            if (double.TryParse(WeatherYInput.Text, out double weatherY))
            {
                config.WeatherY = weatherY;
            }

            config.Save();
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