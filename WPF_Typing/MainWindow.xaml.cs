using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Threading;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shell; // for WindowChrome
using System.ComponentModel; // for DesignerProperties
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WPF_Typing
{
    public class WindowStateInfo
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
        public string? TesterName { get; set; }
        public TimeSpan CountdownDuration { get; set; } = TimeSpan.FromMinutes(1);
        public bool CountdownEnabled { get; set; } = false;
    }

    public partial class MainWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        private readonly string _stateFilePath;
        private readonly string _textsRoot;

        // Custom maximize state: when true the window is "maximized" with an outer margin
        private bool _isCustomMaximized;
        private Rect _restoreBounds = Rect.Empty; // stores normal window bounds to restore to
        private readonly double _outerMargin = 0; // DIP margin to leave from screen edges
        private bool _isDragging = false;
        private bool _isPotentialDrag = false;
        private Point _dragStartPosition;
        private Point _dragStartScreenPosition;

        private ScrollViewer? _typingScrollViewer = null;
        private DispatcherTimer? _scrollTimer = null;
        private bool _isScrolling = false;
        private DispatcherTimer? _sizeChangedTimer = null;

        // Current tester name (default)
        private string _testerName = "江湖人士";

        // Current selected article path
        private string? _currentArticlePath = null;

        public string TesterName
        {
            get => _testerName;
            set
            {
                if (_testerName != value)
                {
                    _testerName = value;
                    OnPropertyChanged(nameof(TesterName));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propName));

        // pending drag state when clicking on titlebar while maximized
        // private bool _isPendingDrag = false;
        // private NativePoint _pendingDragCursor;

        // per-character state: 0 = untouched, 1 = correct, -1 = wrong
        // private readonly Dictionary<string, int> _charStates = new();

        // map visible (line:char) to Run for quick caret placement
        private readonly Dictionary<string, System.Windows.Documents.Run> _runMap = new();

        // current caret run key ("line:char") tracked so we can clear its background when it moves
        private string? _currentCaretKey = null;

        // vertical linear gradient brush used to highlight the current character background
        private readonly LinearGradientBrush _caretGradientBrush;

        // timing helpers used by the typing logic
        private System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private bool _timerRunning = false;

        private bool _isTimingEnabled = false;
        private DateTime _startTime;
        private System.Windows.Threading.DispatcherTimer _timer = new System.Windows.Threading.DispatcherTimer();

        // Countdown configuration
        private bool _countdownEnabled = false;
        private bool _countdownActive = false;
        private TimeSpan _countdownDuration = TimeSpan.FromMinutes(1);
        private TimeSpan _countdownRemaining = TimeSpan.Zero;
        private DateTime? _countdownEndTime = null;

        // Statistics
        private int _backspaceCount = 0;

        private void StartTimingIfNeeded()
        {
            if (_timerRunning) return;

            _stopwatch.Restart();
            if (_countdownEnabled)
            {
                _countdownRemaining = _countdownDuration;
                _countdownEndTime = DateTime.Now + _countdownDuration;
                _countdownActive = true;
            }

            TestStartTime = DateTime.Now;
            UpdateElapsedDisplay();

            try
            {
                _timer.Start();
            }
            catch
            {
            }

            _timerRunning = true;
            UpdatePlayStopButtonState();
        }

        private void UpdateElapsedDisplay()
        {
            var elapsed = _stopwatch.Elapsed;

            ElapsedHours = elapsed.Hours;
            ElapsedMinutes = elapsed.Minutes;
            ElapsedSeconds = elapsed.Seconds;

            double minutes = elapsed.TotalMinutes;
            if (minutes > 0)
            {
                Speed = Math.Round(TypedCount / minutes, 2);
            }
            else
            {
                Speed = TypedCount;
            }
        }

        // ---- Typing stats properties (bound to UI) ----
        private int _typedCount = 0;

        public int TypedCount
        {
            get => _typedCount;
            private set
            {
                if (_typedCount != value)
                {
                    _typedCount = value;
                    OnPropertyChanged(nameof(TypedCount));
                }
            }
        }

        private int _totalCount = 0;

        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (_totalCount != value)
                {
                    _totalCount = value;
                    OnPropertyChanged(nameof(TotalCount));
                }
            }
        }

        private double _progressPercent = 0.0; // 0..100

        public double ProgressPercent
        {
            get => _progressPercent;
            private set
            {
                if (Math.Abs(_progressPercent - value) > 0.0001)
                {
                    _progressPercent = value;
                    OnPropertyChanged(nameof(ProgressPercent));
                }
            }
        }

        private double _accuracyPercent = 0.0; // 0..100

        public double AccuracyPercent
        {
            get => _accuracyPercent;
            private set
            {
                if (Math.Abs(_accuracyPercent - value) > 0.0001)
                {
                    _accuracyPercent = value;
                    OnPropertyChanged(nameof(AccuracyPercent));
                }
            }
        }

        private double _speed = 0.0; // characters per minute

        public double Speed
        {
            get => _speed;
            private set
            {
                if (Math.Abs(_speed - value) > 0.0001)
                {
                    _speed = value;
                    OnPropertyChanged(nameof(Speed));
                }
            }
        }

        private int _elapsedHours = 0;

        public int ElapsedHours
        {
            get => _elapsedHours;
            private set
            {
                if (_elapsedHours != value)
                {
                    _elapsedHours = value;
                    OnPropertyChanged(nameof(ElapsedHours));
                }
            }
        }

        private int _elapsedMinutes = 0;

        public int ElapsedMinutes
        {
            get => _elapsedMinutes;
            private set
            {
                if (_elapsedMinutes != value)
                {
                    _elapsedMinutes = value;
                    OnPropertyChanged(nameof(ElapsedMinutes));
                }
            }
        }

        private int _elapsedSeconds = 0;

        public int ElapsedSeconds
        {
            get => _elapsedSeconds;
            private set
            {
                if (_elapsedSeconds != value)
                {
                    _elapsedSeconds = value;
                    OnPropertyChanged(nameof(ElapsedSeconds));
                }
            }
        }

        // whether the typing run has finished (user typed the very last character)
        private bool _typingFinished = false;

        private DateTime? _testStartTime = null;

        public DateTime? TestStartTime
        {
            get => _testStartTime;
            private set
            {
                if (_testStartTime != value)
                {
                    _testStartTime = value;
                    OnPropertyChanged(nameof(TestStartTime));
                    OnPropertyChanged(nameof(TestStartTimeHours));
                    OnPropertyChanged(nameof(TestStartTimeMinutes));
                    OnPropertyChanged(nameof(TestStartTimeSeconds));
                }
            }
        }

        public int TestStartTimeHours => _testStartTime?.Hour ?? 0;
        public int TestStartTimeMinutes => _testStartTime?.Minute ?? 0;
        public int TestStartTimeSeconds => _testStartTime?.Second ?? 0;

        private DateTime? _testEndTime = null;

        public DateTime? TestEndTime
        {
            get => _testEndTime;
            private set
            {
                if (_testEndTime != value)
                {
                    _testEndTime = value;
                    OnPropertyChanged(nameof(TestEndTime));
                    OnPropertyChanged(nameof(TestEndTimeHours));
                    OnPropertyChanged(nameof(TestEndTimeMinutes));
                    OnPropertyChanged(nameof(TestEndTimeSeconds));
                }
            }
        }

        public int TestEndTimeHours => _testEndTime?.Hour ?? 0;
        public int TestEndTimeMinutes => _testEndTime?.Minute ?? 0;
        public int TestEndTimeSeconds => _testEndTime?.Second ?? 0;

        private void InitializeTimer()
        {
            // Use a short tick to keep UI updates responsive while the actual timing comes from Stopwatch.
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_timerRunning) return;
            try
            {
                if (_countdownActive)
                {
                    if (_countdownEndTime.HasValue)
                    {
                        var remaining = _countdownEndTime.Value - DateTime.Now;
                        if (remaining <= TimeSpan.Zero)
                        {
                            _countdownRemaining = TimeSpan.Zero;
                            HandleCountdownFinished();
                            return;
                        }
                        else
                        {
                            _countdownRemaining = remaining;
                        }
                    }
                    else
                    {
                        // fallback to tick-based decrement if end time missing
                        _countdownRemaining = _countdownRemaining - TimeSpan.FromSeconds(1);
                        if (_countdownRemaining <= TimeSpan.Zero)
                        {
                            _countdownRemaining = TimeSpan.Zero;
                            HandleCountdownFinished();
                            return;
                        }
                    }
                }

                var elapsed = _stopwatch.Elapsed; // use stopwatch for higher precision

                ElapsedHours = elapsed.Hours;
                ElapsedMinutes = elapsed.Minutes;
                ElapsedSeconds = elapsed.Seconds;

                double minutes = elapsed.TotalMinutes;
                if (minutes > 0)
                {
                    Speed = Math.Round(TypedCount / minutes, 2);
                }
                else
                {
                    Speed = TypedCount; // instantaneous (before 1 second) show raw count
                }
            }
            catch
            {
                // ignore transient update errors
            }
        }

        private void HandleCountdownFinished()
        {
            _countdownActive = false;
            _typingFinished = true;

            try
            {
                _timer.Stop();
            }
            catch
            {
            }

            try
            {
                _stopwatch.Stop();
            }
            catch
            {
            }

            _timerRunning = false;
            UpdatePlayStopButtonState();

            // 固定显示倒计时全长作为用时
            var elapsed = _countdownDuration;
            ElapsedHours = elapsed.Hours;
            ElapsedMinutes = elapsed.Minutes;
            ElapsedSeconds = elapsed.Seconds;
            double fm = elapsed.TotalMinutes;
            if (fm > 0) Speed = Math.Round(TypedCount / fm, 2);
            _countdownEndTime = null;
            TestEndTime = DateTime.Now;

            // 倒计时结束，立即保存数据，然后显示统计对话框
            SaveTestResultIfNeeded();
            
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowAnalysis();
            }));
        }

        private void ShowAnalysis()
        {
            int totalChars = _charStates.Count;
            int correctChars = _charStates.Values.Count(s => s == CharState.Correct);
            int incorrectChars = _charStates.Values.Count(s => s == CharState.Incorrect);
            double accuracy = totalChars > 0 ? (double)correctChars / totalChars * 100 : 0;

            TimeSpan elapsedTime;
            if (_isTimingEnabled && _countdownEnabled)
            {
                // 倒计时模式：使用设置的倒计时时长，而不是实际经过的时间
                // 这样可以与主窗体显示的用时保持一致
                elapsedTime = _countdownDuration;
            }
            else if (_isTimingEnabled)
            {
                elapsedTime = DateTime.Now - _startTime;
            }
            else
            {
                elapsedTime = _stopwatch.Elapsed;
            }

            // Calculate completion rate (typed characters / total characters)
            double completionRate = TotalCount > 0 ? (double)totalChars / TotalCount * 100 : 0;

            var analysisDialog = new AnalysisDialog(
                testerName: TesterName,
                articlePath: _currentArticlePath,
                testStartTime: TestStartTime,
                testEndTime: TestEndTime,
                completionRate: completionRate,
                elapsedTime: elapsedTime,
                accuracy: accuracy,
                backspaceCount: _backspaceCount);
            analysisDialog.Owner = this;
            analysisDialog.ShowDialog();
        }
        
        /// <summary>
        /// 在测试结束时保存数据到数据库（如果有实际输入字符）
        /// </summary>
        private void SaveTestResultIfNeeded()
        {
            int totalChars = _charStates.Count;
            // 只有在实际测试结束（有输入字符）时才保存到数据库
            if (totalChars > 0)
            {
                int correctChars = _charStates.Values.Count(s => s == CharState.Correct);
                int incorrectChars = _charStates.Values.Count(s => s == CharState.Incorrect);
                double accuracy = totalChars > 0 ? (double)correctChars / totalChars * 100 : 0;
                double completionRate = TotalCount > 0 ? (double)totalChars / TotalCount * 100 : 0;
                
                SaveTestResultToDatabase(totalChars, correctChars, incorrectChars, completionRate, accuracy, Speed);
            }
        }
        
        /// <summary>
        /// 保存测试结果到数据库
        /// </summary>
        private void SaveTestResultToDatabase(int totalChars, int correctChars, int incorrectChars, 
            double completionRate, double accuracy, double speed)
        {
            try
            {
                // 提取文件夹名称和文件名
                string folderName = string.Empty;
                string fileName = string.Empty;
                
                if (!string.IsNullOrEmpty(_currentArticlePath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(_currentArticlePath);
                        folderName = fileInfo.Directory?.Name ?? string.Empty;
                        fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    }
                    catch
                    {
                        // 如果解析失败，尝试从路径中提取
                        var parts = _currentArticlePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (parts.Length >= 2)
                        {
                            folderName = parts[parts.Length - 2];
                            fileName = Path.GetFileNameWithoutExtension(parts[parts.Length - 1]);
                        }
                    }
                }
                
                // 收集错误字符信息
                var errorCharsList = _errorChars.Values.ToList();
                
                // 创建测试结果对象
                var testResult = new TestResult
                {
                    TesterName = TesterName ?? "江湖人士",
                    FolderName = folderName,
                    FileName = fileName,
                    CompletionRate = completionRate,
                    TotalChars = TotalCount,
                    CorrectChars = correctChars,
                    IncorrectChars = incorrectChars,
                    ErrorChars = errorCharsList,
                    Accuracy = accuracy,
                    Speed = speed,
                    BackspaceCount = _backspaceCount,
                    TestStartTime = TestStartTime ?? DateTime.Now,
                    TestEndTime = TestEndTime ?? DateTime.Now,
                    TestTime = DateTime.Now
                };
                
                // 保存到数据库
                DatabaseHelper.SaveTestResult(testResult);
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                System.Diagnostics.Debug.WriteLine($"保存测试结果失败: {ex.Message}");
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            InitializeTimer();

            // manual scroll reset timer setup
            // _manualScrollResetTimer.Interval = TimeSpan.FromMilliseconds(600);
            // _manualScrollResetTimer.Tick += (s, e) =>
            // {
            //     try
            //     {
            //         _manualScrollResetTimer.Stop();
            //         _isManualScroll = false;
            //     }
            //     catch
            //     {
            //         // ignored
            //     }
            // };

            // prepare caret gradient brush (transparent -> white -> transparent vertically)
            _caretGradientBrush = new LinearGradientBrush()
            {
                StartPoint = new System.Windows.Point(0.5, -0.5),
                EndPoint = new System.Windows.Point(0.5, 1.5),
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };
            _caretGradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, -0.5));
            _caretGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(100, 155, 200, 255), 0.4));
            _caretGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(100, 155, 200, 255), 0.6));
            _caretGradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1.5));

            // Use the window itself as DataContext so bindings like {Binding TesterName} work
            this.DataContext = this;

            // initialize default tester name
            // prepare paths
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "WPF_Typing");
            Directory.CreateDirectory(folder);
            _stateFilePath = Path.Combine(folder, "window.state.json");

            // Texts folder relative to executable or project output
            _textsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Texts");

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;

            // also handle typing area focus changes (if control exists at runtime, handlers are added in Loaded)
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized && !_isCustomMaximized)
            {
                if (_restoreBounds.IsEmpty)
                {
                    _restoreBounds = new Rect(Left, Top, Width, Height);
                }

                WindowState = WindowState.Normal;
                ApplyCustomMaximize();
            }
            else if (WindowState == WindowState.Normal && _isCustomMaximized)
            {
                RestoreFromCustomMaximize();
            }
        }

        private void ApplyCustomMaximize()
        {
            if (!_isCustomMaximized)
            {
                if (_restoreBounds.IsEmpty)
                {
                    _restoreBounds = new Rect(Left, Top, Width, Height);
                }

                _isCustomMaximized = true;

                var workArea = SystemParameters.WorkArea;

                Left = workArea.Left + _outerMargin;
                Top = workArea.Top + _outerMargin;
                Width = workArea.Width - 2 * _outerMargin;
                Height = workArea.Height - 2 * _outerMargin;
            }
        }

        private void RestoreFromCustomMaximize()
        {
            if (_isCustomMaximized)
            {
                _isCustomMaximized = false;

                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
            }
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // Restore window position and size if available
            TryRestoreWindowState();

            // Initialize play/stop button state
            UpdatePlayStopButtonState();

            // Populate text selection menu
            PopulateTextSelectionMenu();

            // Apply WindowChrome hit test settings at runtime only (designer cannot parse attached attributes)
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                if (TitleText != null)
                    WindowChrome.SetIsHitTestVisibleInChrome(TitleText, false);
                if (MinimizeButton != null)
                    WindowChrome.SetIsHitTestVisibleInChrome(MinimizeButton, true);
                if (MaximizeButton != null)
                    WindowChrome.SetIsHitTestVisibleInChrome(MaximizeButton, true);
                if (CloseButton != null)
                    WindowChrome.SetIsHitTestVisibleInChrome(CloseButton, true);
            }

            // Keep typing area layout in sync: when its size changes (or font changes), re-render lines so LineHeight / PageWidth stay correct
            try
            {
                if (TypingDisplay != null)
                {
                    TypingDisplay.SizeChanged += TypingDisplay_SizeChanged;
                    TypingDisplay.GotFocus += (s, ev) => UpdateCaretBackgrounds(_currentLine, _currentChar);
                    TypingDisplay.LostFocus += (s, ev) => UpdateCaretBackgrounds(null, null);

                    TypingDisplay.RequestBringIntoView += (sender, e) => { e.Handled = true; };

                    if (TypingDisplayScrollViewer != null)
                    {
                        _typingScrollViewer = TypingDisplayScrollViewer;

                        _typingScrollViewer.PreviewMouseWheel += (ss, ee) => ee.Handled = true;

                        _typingScrollViewer.ScrollChanged += TypingDisplay_ScrollChanged;
                    }
                }
            }
            catch
            {
            }
        }

        private void TypingDisplay_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            try
            {
                if (_sizeChangedTimer != null)
                {
                    _sizeChangedTimer.Stop();
                    _sizeChangedTimer = null;
                }

                _sizeChangedTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(150)
                };

                _sizeChangedTimer.Tick += (s, args) =>
                {
                    _sizeChangedTimer?.Stop();
                    _sizeChangedTimer = null;

                    try
                    {
                        _needsFullRebuild = true;
                        RenderVisibleLines();
                    }
                    catch
                    {
                    }
                };

                _sizeChangedTimer.Start();
            }
            catch
            {
            }
        }

        private void TryRestoreWindowState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath, Encoding.UTF8);
                    var state = JsonSerializer.Deserialize<WindowStateInfo>(json);
                    if (state != null)
                    {
                        // Validate sizes to avoid off-screen or invalid values
                        if (state.Width > 0 && state.Height > 0)
                        {
                            Width = state.Width;
                            Height = state.Height;
                        }

                        Left = state.Left;
                        Top = state.Top;

                        if (state.IsMaximized)
                        {
                            // saved as maximized: restore the saved normal bounds into _restoreBounds then apply custom maximize
                            _restoreBounds = new Rect(state.Left, state.Top, state.Width, state.Height);
                            ApplyCustomMaximize();
                        }

                        if (!string.IsNullOrWhiteSpace(state.TesterName))
                        {
                            TesterName = state.TesterName;
                        }
                        
                        // 恢复计时设置
                        _countdownDuration = state.CountdownDuration;
                        _countdownEnabled = state.CountdownEnabled;
                    }
                    else
                    {
                        // no saved state -> use default size 1280x800 centered
                        Width = 1280;
                        Height = 800;
                        WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                }
                else
                {
                    // no saved file -> use default size 1280x800 centered
                    Width = 1280;
                    Height = 800;
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
            catch
            {
                // ignore and fallback to default size
                Width = 1280;
                Height = 800;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // Ensure the window is within visible bounds; if not, center on screen
            var virtualScreenWidth = SystemParameters.VirtualScreenWidth;
            var virtualScreenHeight = SystemParameters.VirtualScreenHeight;
            if (Left < 0 || Top < 0 || Left > virtualScreenWidth || Top > virtualScreenHeight)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            TrySaveWindowState();
        }

        private void TrySaveWindowState()
        {
            try
            {
                double left, top, width, height;
                bool isMax;

                if (_isCustomMaximized)
                {
                    // save the pre-maximized bounds so we can restore them
                    left = _restoreBounds.Left;
                    top = _restoreBounds.Top;
                    width = _restoreBounds.Width;
                    height = _restoreBounds.Height;
                    isMax = true;
                }
                else
                {
                    left = this.Left;
                    top = this.Top;
                    width = this.Width;
                    height = this.Height;
                    isMax = this.WindowState == WindowState.Maximized;
                }

                var state = new WindowStateInfo
                {
                    Left = left,
                    Top = top,
                    Width = width,
                    Height = height,
                    IsMaximized = isMax,
                    TesterName = TesterName,
                    CountdownDuration = _countdownDuration,
                    CountdownEnabled = _countdownEnabled
                };

                var json = JsonSerializer.Serialize(state);
                File.WriteAllText(_stateFilePath, json, Encoding.UTF8);
            }
            catch
            {
                // ignore
            }
        }

        private void PopulateTextSelectionMenu()
        {
            TextSelectionMenu.Items.Clear();

            try
            {
                if (!Directory.Exists(_textsRoot))
                {
                    var item = new MenuItem { Header = "未找到 Texts 文件夹", IsEnabled = false };
                    item.Style = (Style)FindResource("DarkSubMenuItemStyle");
                    TextSelectionMenu.Items.Add(item);
                    return;
                }

                var listFile = Path.Combine(_textsRoot, "file-list.json");
                if (File.Exists(listFile))
                {
                    try
                    {
                        var json = File.ReadAllText(listFile, Encoding.UTF8);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in root.EnumerateObject())
                            {
                                var category = prop.Name;
                                var parent = new MenuItem { Header = category };
                                parent.Style = (Style)FindResource("SubMenuHeaderStyle");

                                try
                                {
                                    var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets",
                                        "Images", "PNG", $"{category}.png");
                                    if (File.Exists(iconPath))
                                    {
                                        var panel = new StackPanel { Orientation = Orientation.Horizontal };

                                        var icon = new Image
                                        {
                                            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath)),
                                            Width = 16,
                                            Height = 16,
                                            Margin = new Thickness(0, 0, 12, 0),
                                            VerticalAlignment = VerticalAlignment.Center
                                        };
                                        panel.Children.Add(icon);

                                        var textBlock = new TextBlock
                                        {
                                            Text = category,
                                            VerticalAlignment = VerticalAlignment.Center
                                        };
                                        panel.Children.Add(textBlock);

                                        parent.Header = panel;
                                    }
                                }
                                catch
                                {
                                }

                                if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var entry in prop.Value.EnumerateArray())
                                    {
                                        if (entry.ValueKind != JsonValueKind.Object) continue;

                                        string? fileName = null;
                                        string charsText = string.Empty;

                                        if (entry.TryGetProperty("文件名", out var fn) &&
                                            fn.ValueKind == JsonValueKind.String)
                                            fileName = fn.GetString() ?? string.Empty;
                                        else if (entry.TryGetProperty("filename", out var fn2) &&
                                                 fn2.ValueKind == JsonValueKind.String)
                                            fileName = fn2.GetString() ?? string.Empty;

                                        if (entry.TryGetProperty("字符数", out var cs))
                                        {
                                            if (cs.ValueKind == JsonValueKind.Number && cs.TryGetInt32(out var n))
                                                charsText = n.ToString();
                                            else if (cs.ValueKind == JsonValueKind.String)
                                                charsText = cs.GetString() ?? string.Empty;
                                        }
                                        else if (entry.TryGetProperty("chars", out var cs2))
                                        {
                                            if (cs2.ValueKind == JsonValueKind.Number && cs2.TryGetInt32(out var n2))
                                                charsText = n2.ToString();
                                            else if (cs2.ValueKind == JsonValueKind.String)
                                                charsText = cs2.GetString() ?? string.Empty;
                                        }

                                        if (string.IsNullOrEmpty(fileName)) continue;

                                        // parse sequence and title from filename using first underscore
                                        string seq = string.Empty;
                                        string title = fileName;
                                        int us = fileName.IndexOf('_');
                                        if (us >= 0)
                                        {
                                            seq = fileName.Substring(0, us);
                                            title = fileName.Substring(us + 1);
                                        }

                                        // remove file extension from displayed title
                                        try
                                        {
                                            title = Path.GetFileNameWithoutExtension(title);
                                        }
                                        catch
                                        {
                                        }

                                        // create header as horizontal panel with colored parts
                                        var panel = new StackPanel { Orientation = Orientation.Horizontal };

                                        if (!string.IsNullOrEmpty(seq))
                                        {
                                            var seqTb = new TextBlock
                                            {
                                                Text = seq,
                                                Foreground =
                                                    (SolidColorBrush)(new BrushConverter()
                                                        .ConvertFromString("#999999") ?? Brushes.Gray),
                                                Margin = new Thickness(0, 0, 10, 0),
                                                VerticalAlignment = VerticalAlignment.Center
                                            };
                                            panel.Children.Add(seqTb);
                                        }

                                        var titleTb = new TextBlock
                                        {
                                            Text = title,
                                            Foreground =
                                                (SolidColorBrush)(new BrushConverter().ConvertFromString("#FFFFFF") ??
                                                                  Brushes.White),
                                            VerticalAlignment = VerticalAlignment.Center
                                        };
                                        panel.Children.Add(titleTb);

                                        if (!string.IsNullOrEmpty(charsText))
                                        {
                                            var charsTb = new TextBlock
                                            {
                                                Text = charsText,
                                                Foreground =
                                                    (SolidColorBrush)(new BrushConverter()
                                                        .ConvertFromString("#9DCBFF") ?? Brushes.LightBlue),
                                                Margin = new Thickness(16, 0, 0, 0),
                                                VerticalAlignment = VerticalAlignment.Center
                                            };
                                            panel.Children.Add(charsTb);
                                        }

                                        var child = new MenuItem { Tag = Path.Combine(_textsRoot, category, fileName) };
                                        child.Header = panel;
                                        child.Style = (Style)FindResource("DarkSubMenuItemStyle");
                                        child.Click += TextChoiceMenuItem_Click;
                                        parent.Items.Add(child);
                                    }
                                }

                                // only add parent category if it contains at least one child item
                                if (parent.Items.Count > 0)
                                {
                                    TextSelectionMenu.Items.Add(parent);
                                }
                            }

                            return;
                        }
                    }
                    catch
                    {
                        // fall through to directory scan on parse error
                    }
                }

                // fallback: enumerate directories under Texts as second-level items (no third-level)
                var dirs = Directory.GetDirectories(_textsRoot);
                if (dirs.Length == 0)
                {
                    var item = new MenuItem { Header = "无可用文本", IsEnabled = false };
                    item.Style = (Style)FindResource("DarkSubMenuItemStyle");
                    TextSelectionMenu.Items.Add(item);
                    return;
                }

                foreach (var d in dirs)
                {
                    // skip empty directories
                    var entries = Directory.GetFileSystemEntries(d);
                    if (entries.Length == 0) continue;

                    var name = Path.GetFileName(d);
                    var mi = new MenuItem { Header = name, Tag = d };
                    mi.Style = (Style)FindResource("SubMenuHeaderStyle");
                    mi.Click += TextChoiceMenuItem_Click;
                    TextSelectionMenu.Items.Add(mi);
                }
            }
            catch (Exception ex)
            {
                var item = new MenuItem { Header = "加载文本失败: " + ex.Message, IsEnabled = false };
                item.Style = (Style)FindResource("DarkSubMenuItemStyle");
                TextSelectionMenu.Items.Add(item);
            }
        }

        private void TextChoiceMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string path)
            {
                // save current article path
                _currentArticlePath = path;

                // load the text file into typing area
                LoadTextFromFile(path);

                // update menu selection visuals: mark this item and its parents as selected (change background)
                ClearTextSelectionChecks();
                
                // 高亮当前选中的三级菜单项（文本文件名）
                var highlightColor = (Color)ColorConverter.ConvertFromString("#395579"); // 蓝色高亮
                mi.Background = new SolidColorBrush(highlightColor);
                
                // 高亮父菜单项（二级菜单，文件夹名）
                if (mi.Parent is MenuItem parent)
                {
                    parent.Background = new SolidColorBrush(highlightColor);
                }

                // update play/stop button state
                UpdatePlayStopButtonState();
            }
        }

        // Typing area state
        private string[] _allLines = Array.Empty<string>();
        private int _visibleStartLine = 0; // index of first line shown
        private const int VisibleLineCount = 5;
        private int _currentLine = 0; // absolute index in _allLines
        private int _currentChar = 0; // index within the current line

        private void ClearTextSelectionChecks()
        {
            // reset background for all items under TextSelectionMenu
            foreach (var item in TextSelectionMenu.Items)
            {
                if (item is MenuItem mi)
                {
                    // 重置二级菜单项背景（SubMenuHeaderStyle 默认是 Transparent）
                    mi.ClearValue(MenuItem.BackgroundProperty);
                    // 重置三级菜单项背景（DarkSubMenuItemStyle 默认是 #2D2D2D）
                    foreach (var child in mi.Items)
                    {
                        if (child is MenuItem cmi) 
                        {
                            cmi.ClearValue(MenuItem.BackgroundProperty);
                        }
                    }
                }
            }
        }

        private void LoadTextFromFile(string path)
        {
            try
            {
                string actualPath = path;
                if (!File.Exists(actualPath))
                {
                    // try to find the file in parent folders' Assets/Texts (useful when running from IDE where files are not copied)
                    var fn = Path.GetFileName(path);
                    var found = FindAssetFile(fn);
                    if (!string.IsNullOrEmpty(found)) actualPath = found;
                }

                if (File.Exists(actualPath))
                {
                    var text = File.ReadAllText(actualPath, Encoding.UTF8);
                    text = text.Trim(); // 先去掉首尾空白，避免多余空格影响后续处理
                    text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                    _allLines = new[] { text };
                }
                else if (Directory.Exists(actualPath))
                {
                    // try to find a .txt inside the directory
                    var txts = Directory.GetFiles(actualPath, "*.txt", SearchOption.TopDirectoryOnly);
                    if (txts.Length > 0)
                    {
                        var text = File.ReadAllText(txts[0], Encoding.UTF8);
                        text = text.Trim(); // 先去掉首尾空白，避免多余空格影响后续处理
                        text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                        _allLines = new[] { text };
                    }
                    else
                    {
                        _allLines = new[] { "(目录中无可用的 .txt 文件)" };
                    }
                }
                else
                {
                    _allLines = new[] { "(未找到文本文件)" };
                }

                // reset view & states
                _visibleStartLine = 0;
                _currentLine = 0;
                _currentChar = 0;
                _charStates.Clear();
                _errorChars.Clear();
                _runMap.Clear();
                _changedChars.Clear();
                _needsFullRebuild = true;
                _stopwatch.Reset();
                _timerRunning = false;
                _countdownActive = false;
                _countdownRemaining = _countdownDuration;
                _countdownEndTime = null;
                TestStartTime = null;
                TestEndTime = null;

                // initialize typing stats
                TotalCount = _allLines.Sum(s => (s ?? string.Empty).Length);
                TypedCount = 0;
                ProgressPercent = 0.0;
                AccuracyPercent = 0.0;
                Speed = 0.0;
                ElapsedHours = ElapsedMinutes = ElapsedSeconds = 0;
                _typingFinished = false;

                // reset statistics
                _backspaceCount = 0;

                // stop timer if running
                try
                {
                    _timer.Stop();
                }
                catch
                {
                }

                RenderVisibleLines();

                if (_typingScrollViewer != null)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                        new Action(() => { _typingScrollViewer.ScrollToVerticalOffset(0); }));
                }

                // focus and place caret at the current character (start)
                TypingDisplay.Focus();
                PlaceCaretAtCurrent();

                // update play/stop button state
                UpdatePlayStopButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载文本失败: " + ex.Message);
            }
        }

        private enum CharState
        {
            Untouched = 0,
            Correct = 1,
            Incorrect = -1
        }

        private readonly Dictionary<string, CharState> _charStates = new();
        
        // 错误字符信息记录：key为"line:char"，value为错误信息（期望字符和实际输入字符）
        private readonly Dictionary<string, ErrorCharInfo> _errorChars = new();

        private readonly HashSet<string> _changedChars = new();

        private bool _needsFullRebuild = true;

        private void RenderVisibleLines()
        {
            if (TypingDisplay == null) return;

            if (_needsFullRebuild)
            {
                BuildInitialDocument();
                _needsFullRebuild = false;
            }
            else
            {
                UpdateChangedCharacters();
            }
        }

        private void BuildInitialDocument()
        {
            var doc = new FlowDocument();
            doc.FontFamily = new System.Windows.Media.FontFamily("Google Sans Code, Consolas, HarmonyOS Sans SC, 微软雅黑");
            doc.FontSize = 24;
            doc.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));

            double lineHeight = doc.FontSize * 3;
            doc.LineHeight = lineHeight;
            doc.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;

            doc.PageHeight = 1000000;

            TypingDisplay.UpdateLayout();
            this.UpdateLayout();
            var ctrlPadding = TypingDisplay.Padding;
            var border = TypingDisplay.BorderThickness;
            double contentWidth = TypingDisplay.ActualWidth - ctrlPadding.Left - ctrlPadding.Right - border.Left -
                                  border.Right;
            if (contentWidth <= 0)
            {
                contentWidth = 1250;
            }

            doc.PageWidth = contentWidth;

            _runMap.Clear();

            for (int li = 0; li < _allLines.Length; li++)
            {
                var p = new Paragraph { Margin = new Thickness(0), LineHeight = doc.LineHeight };
                var line = _allLines[li] ?? string.Empty;

                if (line.Length == 0)
                {
                    var zr = new System.Windows.Documents.Run("\u200B")
                    {
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#777"))
                    };
                    p.Inlines.Add(zr);
                    _runMap[$"{li}:0"] = zr;
                }
                else
                {
                    for (int ci = 0; ci < line.Length; ci++)
                    {
                        var ch = line[ci];
                        string key = $"{li}:{ci}";
                        var run = new System.Windows.Documents.Run(ch.ToString());

                        ApplyCharacterStyle(run, li, ci, ch, key);

                        p.Inlines.Add(run);
                        _runMap[key] = run;
                    }
                }

                doc.Blocks.Add(p);
            }

            TypingDisplay.Document = doc;

            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                TypingDisplay.UpdateLayout();
                var ctrlPadding2 = TypingDisplay.Padding;
                var border2 = TypingDisplay.BorderThickness;
                double contentWidth2 = TypingDisplay.ActualWidth - ctrlPadding2.Left - ctrlPadding2.Right -
                                       border2.Left - border2.Right;
                if (contentWidth2 > 0 && Math.Abs(contentWidth2 - contentWidth) > 1.0)
                {
                    doc.PageWidth = contentWidth2;
                    TypingDisplay.UpdateLayout();
                }
            }));

            _changedChars.Clear();

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { SetTypingDisplayHeight(); }));
        }

        private void SetTypingDisplayHeight()
        {
            try
            {
                if (TypingDisplay?.Document == null || _typingScrollViewer == null) return;

                var doc = TypingDisplay.Document;
                double documentLineHeight = GetDocumentLineHeight();
                if (documentLineHeight <= 0) return;

                double docPadding = doc.PagePadding.Top + doc.PagePadding.Bottom;
                var ctrlPadding = TypingDisplay.Padding;
                double ctrlPad = ctrlPadding.Top + ctrlPadding.Bottom;
                var border = TypingDisplay.BorderThickness;
                double borderPad = border.Top + border.Bottom;

                double desiredHeight = documentLineHeight * VisibleLineCount + docPadding + ctrlPad + borderPad + 2.0;
                _typingScrollViewer.Height = desiredHeight;

                TypingDisplay.UpdateLayout();

                double contentWidth = TypingDisplay.ActualWidth - ctrlPadding.Left - ctrlPadding.Right - border.Left -
                                      border.Right;
                if (contentWidth > 0)
                {
                    doc.PageWidth = contentWidth;
                    doc.PageHeight = 1000000;
                    TypingDisplay.UpdateLayout();
                }
            }
            catch
            {
            }
        }

        private void ApplyCharacterStyle(System.Windows.Documents.Run run, int li, int ci, char ch, string key)
        {
            run.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#777"));
            run.Background = null;

            if (_charStates.TryGetValue(key, out var state))
            {
                if (state == CharState.Correct)
                {
                    run.Foreground = Brushes.LightGreen;
                }
                else if (state == CharState.Incorrect)
                {
                    if (ch == ' ')
                    {
                        run.Background = Brushes.IndianRed;
                        run.Foreground = Brushes.White;
                    }
                    else
                    {
                        run.Foreground = Brushes.IndianRed;
                    }
                }
            }

            if (li == _currentLine && ci == _currentChar)
            {
                run.Foreground = Brushes.White;
                if (TypingDisplay != null && TypingDisplay.IsFocused)
                {
                    try
                    {
                        run.Background = _caretGradientBrush;
                    }
                    catch
                    {
                    }

                    _currentCaretKey = key;
                }
                else
                {
                    run.Background = null;
                    _currentCaretKey = null;
                }
            }
        }

        private void UpdateChangedCharacters()
        {
            if (_changedChars.Count == 0) return;

            foreach (var key in _changedChars)
            {
                if (_runMap.TryGetValue(key, out var run))
                {
                    var parts = key.Split(':');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out var li) &&
                        int.TryParse(parts[1], out var ci))
                    {
                        ApplyCharacterStyle(run, li, ci, _allLines[li][ci], key);
                    }
                }
            }

            _changedChars.Clear();
        }

        private void UpdateCaretPosition()
        {
            if (TypingDisplay == null || _typingScrollViewer == null) return;

            if (_isScrolling) return;

            try
            {
                var caretPos = TypingDisplay.CaretPosition;
                if (caretPos == null) return;

                var rect = caretPos.GetCharacterRect(LogicalDirection.Forward);

                double lineTop = rect.Top;
                double lineBottom = rect.Bottom;

                double currentOffset = _typingScrollViewer.VerticalOffset;
                double visibleHeight = _typingScrollViewer.ViewportHeight;

                double documentLineHeight = GetDocumentLineHeight();
                if (documentLineHeight <= 0) return;

                double visibleBottom = currentOffset + 5 * documentLineHeight;

                double margin = 20.0;
                double visibleTop = currentOffset + margin;

                if (currentOffset > 0 && lineTop < visibleTop)
                {
                    double targetOffset = lineTop;
                    if (targetOffset < 0) targetOffset = 0;

                    SmoothScrollToOffset(targetOffset);
                }
                else if (lineTop >= currentOffset + 5 * documentLineHeight && lineBottom >= visibleBottom)
                {
                    double scrollDistance = documentLineHeight * 4;
                    double targetOffset = currentOffset + scrollDistance;

                    double maxOffset = _typingScrollViewer.ExtentHeight - visibleHeight;
                    if (targetOffset > maxOffset) targetOffset = maxOffset;
                    if (targetOffset < 0) targetOffset = 0;

                    SmoothScrollToOffset(targetOffset);

                    TypingDisplay.UpdateLayout();
                }
            }
            catch
            {
            }
        }

        private void SmoothScrollToOffset(double targetOffset)
        {
            if (_typingScrollViewer == null) return;

            try
            {
                if (_scrollTimer != null)
                {
                    _scrollTimer.Stop();
                    _scrollTimer = null;
                }

                double currentOffset = _typingScrollViewer.VerticalOffset;

                if (Math.Abs(currentOffset - targetOffset) < 0.1)
                {
                    _typingScrollViewer.ScrollToVerticalOffset(targetOffset);
                    return;
                }

                const int duration = 300;
                double distance = targetOffset - currentOffset;
                double startOffset = currentOffset;
                DateTime startTime = DateTime.Now;

                _isScrolling = true;

                _scrollTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };

                _scrollTimer.Tick += (s, e) =>
                {
                    double elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    double progress = Math.Min(elapsed / duration, 1.0);

                    if (progress >= 1.0)
                    {
                        _scrollTimer?.Stop();
                        _scrollTimer = null;
                        _typingScrollViewer.ScrollToVerticalOffset(targetOffset);
                        _isScrolling = false;
                    }
                    else
                    {
                        double easedProgress = 1 - Math.Pow(1 - progress, 3);
                        double scrollOffset = startOffset + distance * easedProgress;
                        _typingScrollViewer.ScrollToVerticalOffset(scrollOffset);
                    }
                };

                _scrollTimer.Start();
            }
            catch
            {
                _typingScrollViewer?.ScrollToVerticalOffset(targetOffset);
                _isScrolling = false;
            }
        }

        private void UpdateCaretBackgrounds(int? line, int? charIndex)
        {
            if (line.HasValue && charIndex.HasValue)
            {
                string key = $"{line.Value}:{charIndex.Value}";
                _changedChars.Add(key);

                if (!string.IsNullOrEmpty(_currentCaretKey)) _changedChars.Add(_currentCaretKey);

                RenderVisibleLines();
            }
            else
            {
                if (!string.IsNullOrEmpty(_currentCaretKey))
                {
                    _changedChars.Add(_currentCaretKey);
                    _currentCaretKey = null;
                    RenderVisibleLines();
                }
            }
        }

        private double GetDocumentLineHeight()
        {
            // Prefer the FlowDocument's configured LineHeight when available; fallback to a reasonable estimate.
            if (TypingDisplay == null || TypingDisplay.Document == null) return 0;

            try
            {
                var doc = TypingDisplay.Document;

                // FlowDocument.LineHeight may be NaN or 0 when not explicitly set; check for a usable value.
                if (!double.IsNaN(doc.LineHeight) && doc.LineHeight > 0)
                {
                    return doc.LineHeight;
                }

                // If FlowDocument doesn't provide LineHeight, prefer the document's FontSize if set.
                double fontSize = (doc.FontSize > 0) ? doc.FontSize : TypingDisplay.FontSize;

                // Use a conservative multiplier (1.5) as a fallback estimate for line height.
                return Math.Max(1.0, fontSize * 1.5);
            }
            catch
            {
                // Fallback to TypingDisplay.FontSize-based estimate if anything goes wrong
                double fontSize = (TypingDisplay?.FontSize > 0) ? TypingDisplay.FontSize : 12.0;
                return Math.Max(1.0, fontSize * 1.5);
            }
        }

        private void PlaceCaretAtCurrent()
        {
            if (TypingDisplay == null || _runMap.Count == 0) return;

            try
            {
                var key = $"{_currentLine}:{_currentChar}";
                if (_runMap.TryGetValue(key, out var run))
                {
                    var tp = run.ContentStart;
                    TypingDisplay.CaretPosition = tp;
                    // Avoid BringIntoView or UpdateLayout here; they can cause a forced reflow and visual flicker when the user has manually scrolled.
                }
            }
            catch
            {
                // ignore any layout issues
            }
        }

        // Handle ScrollChanged from internal ScrollViewer to detect manual scrolling
        private void TypingDisplay_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            try
            {
            }
            catch
            {
                // ignore
            }
        }

        private IEnumerable<DependencyObject> FindVisualChildren(DependencyObject parent)
        {
            if (parent == null) yield break;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null)
                {
                    yield return child;

                    foreach (var subChild in FindVisualChildren(child))
                    {
                        yield return subChild;
                    }
                }
            }
        }

        private TWindowType? FindParentWindow<TWindowType>(DependencyObject child) where TWindowType : Window
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is TWindowType window)
                {
                    return window;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private Style FindResourceStyle(string key)
        {
            var style = Application.Current.Resources[key] as Style;
            if (style != null) return style;

            return this.Resources[key] as Style ?? new Style();
        }

        private string FindAssetFile(string fileName)
        {
            try
            {
                var dir = AppDomain.CurrentDomain.BaseDirectory;
                while (!string.IsNullOrEmpty(dir) && dir != Path.GetPathRoot(dir))
                {
                    var assetDir = Path.Combine(dir, "Assets", "Texts");
                    var filePath = Path.Combine(assetDir, fileName);
                    if (File.Exists(filePath))
                    {
                        return filePath;
                    }

                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private void TypingDisplay_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;
            if (!_timerRunning)
            {
                _stopwatch.Restart();
                if (_countdownEnabled)
                {
                    _countdownRemaining = _countdownDuration;
                    _countdownEndTime = DateTime.Now + _countdownDuration;
                    _countdownActive = true;
                }

                TestStartTime = DateTime.Now;
                try
                {
                    _timer.Start();
                }
                catch
                {
                }

                _timerRunning = true;
                UpdatePlayStopButtonState();
            }

            if (_allLines.Length == 0) return;
            // Only process first character of composition
            var typedChar = e.Text[0];
            ProcessTypedChar(typedChar);
        }

        private void TypingDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle space here because in some cases it may be translated differently; swallow the key so RichTextBox won't insert it
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                ProcessTypedChar(' ');
                return;
            }

            if (e.Key == Key.Back)
            {
                e.Handled = true;
                // if at start of document and nothing to delete, ignore
                if (_currentLine == 0 && _currentChar == 0) return;

                // Record backspace count
                _backspaceCount++;

                // determine position of the character to delete (the one before current caret)
                int delLine = _currentLine;
                int delChar = _currentChar - 1;
                if (delChar < 0)
                {
                    // move to previous line's last char
                    delLine = Math.Max(0, _currentLine - 1);
                    var prevLine = (delLine < _allLines.Length) ? (_allLines[delLine] ?? string.Empty) : string.Empty;
                    delChar = Math.Max(0, prevLine.Length - 1);
                }

                string delKey = $"{delLine}:{delChar}";
                if (_charStates.ContainsKey(delKey)) _charStates.Remove(delKey);
                // 同时清除错误字符记录
                _errorChars.Remove(delKey);
                _changedChars.Add(delKey);

                // Recompute typing stats after deletion
                TypedCount = _charStates.Count;
                int correctCountAfterDel = _charStates.Values.Count(s => s == CharState.Correct);
                if (TotalCount > 0)
                {
                    ProgressPercent = Math.Round((double)TypedCount / TotalCount * 100.0, 2);
                }
                else
                {
                    ProgressPercent = 0.0;
                }

                if (TypedCount > 0)
                {
                    AccuracyPercent = Math.Round((double)correctCountAfterDel / TypedCount * 100.0, 2);
                }
                else
                {
                    AccuracyPercent = 0.0;
                }

                double elapsedMinutesAfterDel = _stopwatch.Elapsed.TotalMinutes;
                if (elapsedMinutesAfterDel > 0)
                {
                    Speed = Math.Round(TypedCount / elapsedMinutesAfterDel, 2);
                }
                else
                {
                    Speed = TypedCount;
                }

                // clear previous caret background
                if (!string.IsNullOrEmpty(_currentCaretKey)) _changedChars.Add(_currentCaretKey);

                // move caret back
                _currentLine = delLine;
                _currentChar = delChar;

                // adjust visible start line if needed
                if (_currentLine < _visibleStartLine)
                {
                    int newStartLine = Math.Max(0, _visibleStartLine - 1);
                    if (newStartLine != _visibleStartLine)
                    {
                        _visibleStartLine = newStartLine;
                        // Let ScrollViewer manage visual scroll; we only update internal visible-line index
                    }
                }

                // mark new caret position for update
                string newKey = $"{_currentLine}:{_currentChar}";
                _changedChars.Add(newKey);

                RenderVisibleLines();
                PlaceCaretAtCurrent();

                Dispatcher.BeginInvoke(() => { UpdateCaretPosition(); });
            }
        }

        // Shared routine to process a typed character (compare, color the run if present, advance caret, handle scrolling)
        private void ProcessTypedChar(char typed)
        {
            if (_typingFinished) return;

            if (!_timerRunning)
            {
                _stopwatch.Restart();
                if (_countdownEnabled)
                {
                    _countdownRemaining = _countdownDuration;
                    _countdownEndTime = DateTime.Now + _countdownDuration;
                    _countdownActive = true;
                }

                TestStartTime = DateTime.Now;
                try
                {
                    _timer.Start();
                }
                catch
                {
                }

                _timerRunning = true;
                UpdatePlayStopButtonState();
            }

            if (_allLines.Length == 0) return;
            if (_currentLine >= _allLines.Length) return;

            var line = _allLines[_currentLine] ?? string.Empty;
            if (_currentChar >= line.Length) return; // nothing to type at this position

            char expected = line[_currentChar];
            string key = $"{_currentLine}:{_currentChar}";

            bool isCorrect = typed == expected;
            _charStates[key] = isCorrect ? CharState.Correct : CharState.Incorrect;
            
            // 记录错误字符信息
            if (!isCorrect)
            {
                _errorChars[key] = new ErrorCharInfo
                {
                    ExpectedChar = expected,
                    ActualChar = typed
                };
            }
            else
            {
                // 如果之前有错误记录，清除它
                _errorChars.Remove(key);
            }
            
            _changedChars.Add(key);

            TypedCount = _charStates.Count;
            int correctCount = _charStates.Values.Count(s => s == CharState.Correct);
            if (TotalCount > 0)
            {
                ProgressPercent = Math.Round((double)TypedCount / TotalCount * 100.0, 2);
            }
            else
            {
                ProgressPercent = 0.0;
            }

            if (TypedCount > 0)
            {
                AccuracyPercent = Math.Round((double)correctCount / TypedCount * 100.0, 2);
            }
            else
            {
                AccuracyPercent = 0.0;
            }

            double elapsedMinutes = _stopwatch.Elapsed.TotalMinutes;
            UpdateElapsedDisplay();

            // detect last char of document
            bool isLastCharOfDocument = (_currentLine == _allLines.Length - 1 && _currentChar == line.Length - 1);

            // advance caret
            AdvancePosition();

            if (isLastCharOfDocument)
            {
                var finalElapsed = _stopwatch.Elapsed;
                ElapsedHours = finalElapsed.Hours;
                ElapsedMinutes = finalElapsed.Minutes;
                ElapsedSeconds = finalElapsed.Seconds;
                double fm = finalElapsed.TotalMinutes;
                if (fm > 0) Speed = Math.Round(TypedCount / fm, 2);

                TestEndTime = DateTime.Now;
                _countdownActive = false;

                try
                {
                    _timer.Stop();
                }
                catch
                {
                    // ignore
                }

                try
                {
                    _stopwatch.Stop();
                }
                catch
                {
                    // ignore
                }

                _timerRunning = false;
                _typingFinished = true;
                UpdatePlayStopButtonState();
                
                // 文章全部输入完毕，立即保存数据，然后显示统计对话框
                SaveTestResultIfNeeded();
                
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ShowAnalysis();
                }));
            }

            var newKey = $"{_currentLine}:{_currentChar}";
            _changedChars.Add(newKey);

            RenderVisibleLines();
            PlaceCaretAtCurrent();

            Dispatcher.BeginInvoke(() => { UpdateCaretPosition(); });
        }

        private void AdvancePosition()
        {
            var line = (_currentLine < _allLines.Length) ? (_allLines[_currentLine] ?? string.Empty) : string.Empty;
            if (_currentChar + 1 < line.Length)
            {
                _currentChar++;
            }
            else
            {
                if (_currentLine + 1 < _allLines.Length)
                {
                    _currentLine++;
                    _currentChar = 0;
                }
                else
                {
                    _currentChar = line.Length;
                }
            }
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutDialog();
            about.Owner = this;
            about.ShowDialog();
        }

        private void WindowTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                    e.Handled = true;
                    return;
                }

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    if (_isCustomMaximized)
                    {
                        _isPotentialDrag = true;
                        _isDragging = false;
                        _dragStartPosition = e.GetPosition((UIElement)sender);
                        _dragStartScreenPosition = PointToScreen(e.GetPosition(this));
                        ((UIElement)sender).CaptureMouse();
                        e.Handled = true;
                    }
                    else
                    {
                        this.DragMove();
                    }
                }
            }
            catch
            {
                // ignore if DragMove fails during design-time
            }
        }

        private void WindowTitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPotentialDrag && _isCustomMaximized && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentScreenPosition = PointToScreen(e.GetPosition(this));
                var moveDistance = Math.Abs(currentScreenPosition.X - _dragStartScreenPosition.X) +
                                   Math.Abs(currentScreenPosition.Y - _dragStartScreenPosition.Y);

                if (moveDistance > 2.0)
                {
                    _isDragging = true;
                    _isPotentialDrag = false;

                    var source = PresentationSource.FromVisual(this);
                    if (source?.CompositionTarget != null)
                    {
                        var transform = source.CompositionTarget.TransformToDevice;
                        var dpiScaleX = transform.M11;
                        var dpiScaleY = transform.M22;

                        var workArea = SystemParameters.WorkArea;
                        var titleBarElement = (UIElement)sender;

                        var maximizedTitleBarWidth = workArea.Width;
                        var workAreaLeftLogical = workArea.Left;
                        var workAreaTopLogical = workArea.Top;

                        var dragStartScreenX = _dragStartScreenPosition.X;
                        var dragStartScreenY = _dragStartScreenPosition.Y;
                        var dragStartLogicalX = dragStartScreenX / dpiScaleX;
                        var dragStartLogicalY = dragStartScreenY / dpiScaleY;

                        var mouseOffsetFromWorkAreaLeft = dragStartLogicalX - workAreaLeftLogical;
                        var horizontalPercent = mouseOffsetFromWorkAreaLeft / maximizedTitleBarWidth;

                        var verticalOffset = dragStartLogicalY - workAreaTopLogical;

                        _isCustomMaximized = false;
                        titleBarElement.ReleaseMouseCapture();

                        Width = _restoreBounds.Width;
                        Height = _restoreBounds.Height;

                        var currentLogicalX = currentScreenPosition.X / dpiScaleX;
                        var currentLogicalY = currentScreenPosition.Y / dpiScaleY;

                        var mouseOffsetInRestoredTitleBar = horizontalPercent * _restoreBounds.Width;
                        Left = currentLogicalX - mouseOffsetInRestoredTitleBar;
                        Top = currentLogicalY - verticalOffset;

                        if (Left < workAreaLeftLogical) Left = workAreaLeftLogical;
                        if (Top < workAreaTopLogical) Top = workAreaTopLogical;
                        if (Left + Width > workArea.Right) Left = workArea.Right - Width;
                        if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;

                        _isDragging = false;
                        this.DragMove();
                    }
                }
            }
        }

        private void WindowTitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPotentialDrag || _isDragging)
            {
                var titleBarElement = (UIElement)sender;
                titleBarElement.ReleaseMouseCapture();
                _isPotentialDrag = false;
                _isDragging = false;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            if (_isCustomMaximized)
            {
                RestoreFromCustomMaximize();
            }
            else
            {
                if (this.WindowState == WindowState.Normal)
                {
                    _restoreBounds = new Rect(Left, Top, Width, Height);
                }

                this.WindowState = WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void NameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new NameDialog(TesterName);
                dlg.Owner = this;
                if (dlg.ShowDialog() == true)
                {
                    if (!string.IsNullOrWhiteSpace(dlg.EnteredName))
                    {
                        TesterName = dlg.EnteredName!;
                    }
                }
            }
            catch
            {
            }
        }

        private void TimingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CountdownDialog(_countdownDuration, _countdownEnabled)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _countdownDuration = dlg.CountdownDuration;
                _countdownEnabled = dlg.EnableCountdown;
                _countdownRemaining = _countdownDuration;
                _countdownActive = false;

                // 保持计时功能启用，使用 StartTime 记录基准
                _isTimingEnabled = _countdownEnabled;
                _startTime = DateTime.Now;
            }
        }

        private void AnalysisMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowAnalysis();
        }

        private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new HelpDialog();
                dlg.Owner = this;
                dlg.ShowDialog();
            }
            catch
            {
            }
        }

        private void PlayStopButton_Click(object sender, RoutedEventArgs e)
        {
            // 如果未选中任何文章，点击不产生作用
            if (string.IsNullOrEmpty(_currentArticlePath))
            {
                return;
            }

            // 如果测试正在进行，停止测试
            if (_timerRunning)
            {
                StopTest();
            }
            // 如果测试已结束，重新载入当前文章
            else if (_typingFinished)
            {
                LoadTextFromFile(_currentArticlePath);
            }
        }

        private void StopTest()
        {
            _typingFinished = true;
            _timerRunning = false;
            _countdownActive = false;

            try
            {
                _timer.Stop();
            }
            catch
            {
            }

            try
            {
                _stopwatch.Stop();
            }
            catch
            {
            }

            TestEndTime = DateTime.Now;
            UpdatePlayStopButtonState();
            
            // 停止测试，立即保存数据，然后显示统计对话框
            SaveTestResultIfNeeded();
            ShowAnalysis();
        }

        private void UpdatePlayStopButtonState()
        {
            if (PlayStopButton == null) return;

            // 确保在 UI 线程上执行
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdatePlayStopButtonState());
                return;
            }

            try
            {
                if (_timerRunning)
                {
                    // 测试正在进行：红色背景，停止图标
                    var stopStyle = (Style)FindResource("StopButtonStyle");
                    if (stopStyle != null)
                    {
                        PlayStopButton.Style = stopStyle;
                    }
                    PlayStopButton.Content = "■";
                }
                else
                {
                    // 测试未进行：绿色背景，播放图标
                    var playStyle = (Style)FindResource("PlayButtonStyle");
                    if (playStyle != null)
                    {
                        PlayStopButton.Style = playStyle;
                    }
                    PlayStopButton.Content = "▶";
                }
            }
            catch
            {
                // 忽略样式设置错误
            }
        }
    }
}