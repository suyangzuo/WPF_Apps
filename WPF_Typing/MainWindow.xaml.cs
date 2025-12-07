using System;
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

namespace WPF_Typing
{
    // 窗口状态信息类，用于保存和恢复窗口位置、大小和状态
    public class WindowStateInfo
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
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

        // 手动滚动控制
        private bool _isManualScroll = false;
        private double _manualScrollOffset = 0;

        // Current tester name (default)
        private string _testerName = "江湖人士";

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
        // private bool _isPendingDrag = false; // 暂时注释掉未使用的字段
        // private NativePoint _pendingDragCursor; // 暂时注释掉未使用的字段

        // per-character state: 0 = untouched, 1 = correct, -1 = wrong
        // private readonly Dictionary<string, int> _charStates = new(); // 注释掉原始定义

        // map visible (line:char) to Run for quick caret placement
        private readonly Dictionary<string, System.Windows.Documents.Run> _runMap = new();

        // current caret run key ("line:char") tracked so we can clear its background when it moves
        private string? _currentCaretKey = null;

        // vertical linear gradient brush used to highlight the current character background
        private readonly LinearGradientBrush _caretGradientBrush;

        // timing helpers used by the typing logic
        private System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private bool _timerRunning = false;
        
        // 计时相关变量
        private bool _isTimingEnabled = false;
        private DateTime _startTime;
        private System.Windows.Threading.DispatcherTimer _timer = new System.Windows.Threading.DispatcherTimer();
        
        // 初始化计时器
        private void InitializeTimer()
        {
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }
        
        // 计时器事件处理
        private void Timer_Tick(object? sender, EventArgs e)
        {
            // 更新显示的时间
            if (_isTimingEnabled)
            {
                var elapsed = DateTime.Now - _startTime;
                // 这里可以更新UI显示经过的时间
            }
        }
        
        // 显示分析结果
        private void ShowAnalysis()
        {
            // 计算统计数据
            int totalChars = _charStates.Count;
            int correctChars = _charStates.Values.Count(s => s == CharState.Correct);
            int incorrectChars = _charStates.Values.Count(s => s == CharState.Incorrect);
            double accuracy = totalChars > 0 ? (double)correctChars / totalChars * 100 : 0;
            
            // 计算时间
            TimeSpan elapsedTime;
            if (_isTimingEnabled)
            {
                elapsedTime = DateTime.Now - _startTime;
            }
            else
            {
                elapsedTime = _stopwatch.Elapsed;
            }
            
            // 计算速度（字符/分钟）
            double speed = 0;
            if (elapsedTime.TotalMinutes > 0)
            {
                speed = correctChars / elapsedTime.TotalMinutes;
            }
            
            // 显示结果
            string message = $"练习统计:\n\n" +
                            $"总字符数: {totalChars}\n" +
                            $"正确字符: {correctChars}\n" +
                            $"错误字符: {incorrectChars}\n" +
                            $"准确率: {accuracy:F1}%\n" +
                            $"用时: {elapsedTime.Minutes}分{elapsedTime.Seconds}秒\n" +
                            $"速度: {speed:F1}字符/分钟";
            
            var analysisDialog = new AnalysisDialog(message);
            analysisDialog.Owner = this;
            analysisDialog.ShowDialog();
        }

        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化计时器
            InitializeTimer();

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
            TesterName = "江湖人士";

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

        // 窗口状态变化事件处理程序
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            // 处理窗口状态变化，例如最大化、最小化等
            if (WindowState == WindowState.Maximized && !_isCustomMaximized)
            {
                // 如果是系统最大化，转换为自定义最大化
                WindowState = WindowState.Normal;
                ApplyCustomMaximize();
            }
            else if (WindowState == WindowState.Normal && _isCustomMaximized)
            {
                // 如果是正常状态但之前是自定义最大化，恢复原始大小
                RestoreFromCustomMaximize();
            }
        }

        // 应用自定义最大化
        private void ApplyCustomMaximize()
        {
            if (!_isCustomMaximized)
            {
                // 保存当前窗口状态
                _restoreBounds = new Rect(Left, Top, Width, Height);
                _isCustomMaximized = true;

                // 设置最大化状态
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;

                // 应用外边距
                Left = _outerMargin;
                Top = _outerMargin;
                Width = screenWidth - 2 * _outerMargin;
                Height = screenHeight - 2 * _outerMargin;
            }
        }

        // 从自定义最大化恢复
        private void RestoreFromCustomMaximize()
        {
            if (_isCustomMaximized)
            {
                _isCustomMaximized = false;

                // 恢复原始窗口大小和位置
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
            }
        }

        // 禁止鼠标滚轮滚动
        private void TypingDisplay_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true; // 阻止默认的滚动行为
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // Restore window position and size if available
            TryRestoreWindowState();

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
                    // 禁止滚轮滚动
                    TypingDisplay.PreviewMouseWheel += TypingDisplay_PreviewMouseWheel;
                }
            }
            catch
            {
            }
        }

        private void TypingDisplay_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            // Recompute visible rendering when the typing area size changes to ensure line wrapping and LineHeight are applied correctly
            try
            {
                _needsFullRebuild = true; // 窗口大小改变时需要完全重建文档
                _needsScroll = true; // 窗口大小改变时需要重新设置滚动位置
                _targetScrollLine = _visibleStartLine;
                RenderVisibleLines();
            }
            catch
            {
                // ignore transient layout exceptions
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
                    IsMaximized = isMax
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

                                // 为二级菜单项添加图标
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
                                    // 如果图标加载失败，忽略错误继续执行
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
                                                (SolidColorBrush)(new BrushConverter().ConvertFromString("#FFFFFF") ?? Brushes.White),
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
                // load the text file into typing area
                LoadTextFromFile(path);

                // update menu selection visuals: mark this item and its parents as selected (change background)
                ClearTextSelectionChecks();
                mi.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333"));
                if (mi.Parent is MenuItem parent)
                {
                    parent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
                }
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
                    mi.Background = Brushes.Transparent;
                    foreach (var child in mi.Items)
                    {
                        if (child is MenuItem cmi) cmi.Background = Brushes.Transparent;
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
                    // 将所有换行符替换为空格，创建连续文本
                    text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                    // 将连续空格归一化为单个空格
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                    // 将整个文本作为一个段落，不再按句子分割
                    _allLines = new[] { text };
                }
                else if (Directory.Exists(actualPath))
                {
                    // try to find a .txt inside the directory
                    var txts = Directory.GetFiles(actualPath, "*.txt", SearchOption.TopDirectoryOnly);
                    if (txts.Length > 0)
                    {
                        var text = File.ReadAllText(txts[0], Encoding.UTF8);
                        // 将所有换行符替换为空格，创建连续文本
                        text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                        // 将连续空格归一化为单个空格
                        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                        // 将整个文本作为一个段落，不再按句子分割
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
                _runMap.Clear();
                _changedChars.Clear();
                _needsFullRebuild = true; // 标记需要完全重建文档
                _needsScroll = false; // 重置滚动标记
                _stopwatch.Reset();
                _timerRunning = false;

                RenderVisibleLines();

                // focus and place caret at the current character (start)
                TypingDisplay.Focus();
                PlaceCaretAtCurrent();
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载文本失败: " + ex.Message);
            }
        }

        // 优化后的字符状态管理：使用枚举代替整数，提高可读性
        private enum CharState
        {
            Untouched = 0,  // 未输入
            Correct = 1,    // 正确输入
            Incorrect = -1  // 错误输入
        }

        // 优化后的字符状态字典，使用枚举类型
        private readonly Dictionary<string, CharState> _charStates = new();

        // 记录哪些字符需要更新，避免全量刷新
        private readonly HashSet<string> _changedChars = new();

        // 标记文档是否需要完全重建
        private bool _needsFullRebuild = true;

        // 记录是否需要滚动
        private bool _needsScroll = false;
        private int _targetScrollLine = 0;

        // 优化后的RenderVisibleLines方法，只在必要时重建文档和滚动
        private void RenderVisibleLines()
        {
            if (TypingDisplay == null) return;

            // 只有在需要完全重建时才重建文档
            if (_needsFullRebuild)
            {
                BuildInitialDocument();
                _needsFullRebuild = false;
            }
            else
            {
                // 只更新已更改的字符
                UpdateChangedCharacters();
            }

            // 更新光标位置
            UpdateCaretPosition();

            // 只在需要滚动时才执行滚动操作
            if (_needsScroll)
            {
                _needsScroll = false;
                try
                {
                    double scrollOffset;
                    if (_isManualScroll)
                    {
                        scrollOffset = _manualScrollOffset;
                        _isManualScroll = false;
                    }
                    else
                    {
                        scrollOffset = _targetScrollLine * GetDocumentLineHeight();
                    }
                    
                    // 使用Dispatcher确保滚动在UI更新后执行
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                    {
                        TypingDisplay.ScrollToVerticalOffset(scrollOffset);
                        // 强制更新布局以确保滚动生效
                        TypingDisplay.UpdateLayout();
                    }));
                }
                catch
                {
                    // ignored
                }
            }
        }

        // 构建初始文档，只在加载新文本或窗口大小改变时调用
        private void BuildInitialDocument()
        {
            var doc = new FlowDocument();
            doc.FontFamily = new System.Windows.Media.FontFamily("Google Sans Code, Consolas, HarmonyOS Sans SC, 微软雅黑");
            doc.FontSize = 24; // 修改字体大小为24，与XAML中设置一致
            doc.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));

            // 设置行距为字体大小的1.5倍
            double lineHeight = doc.FontSize * 3;
            doc.LineHeight = lineHeight;
            doc.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;

            // 清空runMap，但保留已更改的字符记录
            _runMap.Clear();

            // 构建文档
            for (int li = 0; li < _allLines.Length; li++)
            {
                var p = new Paragraph { Margin = new Thickness(0), LineHeight = doc.LineHeight };
                var line = _allLines[li] ?? string.Empty;

                if (line.Length == 0)
                {
                    // 空行处理
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

                        // 应用字符状态
                        ApplyCharacterStyle(run, li, ci, ch, key);

                        p.Inlines.Add(run);
                        _runMap[key] = run;
                    }
                }

                doc.Blocks.Add(p);
            }

            // 设置文档
            TypingDisplay.Document = doc;

            // 设置RichTextBox高度
            SetTypingDisplayHeight();

            // 清空已更改字符集合
            _changedChars.Clear();
        }

        // 应用字符样式
        private void ApplyCharacterStyle(System.Windows.Documents.Run run, int li, int ci, char ch, string key)
        {
            // 默认颜色
            run.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#777"));
            run.Background = null;

            // 应用已输入状态
            if (_charStates.TryGetValue(key, out var state))
            {
                if (state == CharState.Correct)
                {
                    run.Foreground = Brushes.LightGreen;
                }
                else if (state == CharState.Incorrect)
                {
                    // 检查是否是错误输入的空格
                    if (ch == ' ')
                    {
                        // 如果源文本是空格但输入错误，显示红色背景
                        run.Background = Brushes.IndianRed;
                        run.Foreground = Brushes.White;
                    }
                    else
                    {
                        // 其他错误输入，显示红色前景
                        run.Foreground = Brushes.IndianRed;
                    }
                }
            }

            // 当前光标位置
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

        // 只更新已更改的字符
        private void UpdateChangedCharacters()
        {
            if (_changedChars.Count == 0) return;

            foreach (var key in _changedChars)
            {
                if (_runMap.TryGetValue(key, out var run))
                {
                    // 解析行号和字符索引
                    var parts = key.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int li) && int.TryParse(parts[1], out int ci))
                    {
                        // 获取字符
                        if (li < _allLines.Length)
                        {
                            var line = _allLines[li] ?? string.Empty;
                            if (ci < line.Length)
                            {
                                var ch = line[ci];
                                ApplyCharacterStyle(run, li, ci, ch, key);
                            }
                        }
                    }
                }
            }

            // 清空已更改字符集合
            _changedChars.Clear();
        }

        // 更新光标位置
        private void UpdateCaretPosition()
        {
            // 清除之前的光标背景
            if (!string.IsNullOrEmpty(_currentCaretKey) && _runMap.TryGetValue(_currentCaretKey, out var prevRun))
            {
                // 解析之前的光标位置
                var parts = _currentCaretKey.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int li) && int.TryParse(parts[1], out int ci))
                {
                    if (li < _allLines.Length)
                    {
                        var line = _allLines[li] ?? string.Empty;
                        if (ci < line.Length)
                        {
                            var ch = line[ci];
                            ApplyCharacterStyle(prevRun, li, ci, ch, _currentCaretKey);
                        }
                    }
                }
            }

            // 设置新的光标位置
            var newKey = $"{_currentLine}:{_currentChar}";
            if (_runMap.TryGetValue(newKey, out var newRun))
            {
                newRun.Foreground = Brushes.White;
                if (TypingDisplay != null && TypingDisplay.IsFocused)
                {
                    try
                    {
                        newRun.Background = _caretGradientBrush;
                    }
                    catch
                    {
                    }
                    _currentCaretKey = newKey;
                }
                else
                {
                    newRun.Background = null;
                    _currentCaretKey = null;
                }
            }
        }

        // 设置RichTextBox高度
        private void SetTypingDisplayHeight()
        {
            try
            {
                var doc = TypingDisplay.Document;
                double docPadding = doc.PagePadding.Top + doc.PagePadding.Bottom;
                var ctrlPadding = TypingDisplay.Padding;
                double ctrlPad = ctrlPadding.Top + ctrlPadding.Bottom;
                var border = TypingDisplay.BorderThickness;
                double borderPad = border.Top + border.Bottom;

                double desiredHeight = doc.LineHeight * VisibleLineCount + docPadding + ctrlPad + borderPad + 2.0;
                TypingDisplay.Height = desiredHeight;

                // 设置FlowDocument PageWidth以匹配RichTextBox的可用宽度，实现正确的文本换行
                double contentWidth = TypingDisplay.ActualWidth - ctrlPadding.Left - ctrlPadding.Right - border.Left - border.Right;
                doc.PageWidth = contentWidth;
            }
            catch
            {
                // 忽略错误，保持现有高度
            }
        }

        private void TypingDisplay_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;
            if (string.IsNullOrEmpty(e.Text)) return;

            // start timer if not running
            if (!_timerRunning)
            {
                _stopwatch.Restart();
                _timerRunning = true;
            }

            if (_allLines.Length == 0) return;
            // Only process first character of composition
            var typedChar = e.Text[0];
            ProcessTypedChar(typedChar);
        }

        // Shared routine to process a typed character (compare, color the run if present, advance caret, handle scrolling)
        // 优化后的字符处理方法，使用增量更新
        private void ProcessTypedChar(char typed)
        {
            // start timer if not running (some callers may call this directly)
            if (!_timerRunning)
            {
                _stopwatch.Restart();
                _timerRunning = true;
            }

            if (_allLines.Length == 0) return;
            if (_currentLine >= _allLines.Length) return;

            var line = _allLines[_currentLine] ?? string.Empty;
            if (_currentChar >= line.Length) return; // nothing to type at this position

            char expected = line[_currentChar];
            string key = $"{_currentLine}:{_currentChar}";
            
            // 更新字符状态
            _charStates[key] = (typed == expected) ? CharState.Correct : CharState.Incorrect;
            
            // 标记此字符需要更新
            _changedChars.Add(key);

            // 检查是否输入完本页最后一行的最后一个字符
            int lastVisibleLineIndex = _visibleStartLine + VisibleLineCount - 1;
            bool isLastCharOfLastLine = (_currentLine == lastVisibleLineIndex && _currentChar == line.Length - 1);

            // advance caret
            AdvancePosition();

            // handle scrolling: 当输入完本页最后一行的最后一个字符时，向上滚动4行
            if (isLastCharOfLastLine)
            {
                // 精确控制滚动距离：向上滚动4行
                int newStartLine = Math.Min(_visibleStartLine + 4, Math.Max(0, _allLines.Length - VisibleLineCount));
                if (newStartLine != _visibleStartLine)
                {
                    _visibleStartLine = newStartLine;
                    _needsScroll = true;
                    _targetScrollLine = newStartLine;
                }
            }
            else if (_currentLine >= _visibleStartLine + VisibleLineCount)
            {
                // 如果当前行已经超出可见范围，也需要滚动
                // 精确控制滚动距离：向上滚动4行
                int newStartLine = Math.Min(_visibleStartLine + 4, Math.Max(0, _allLines.Length - VisibleLineCount));
                if (newStartLine != _visibleStartLine)
                {
                    _visibleStartLine = newStartLine;
                    _needsScroll = true;
                    _targetScrollLine = newStartLine;
                }
            }

            // 标记新光标位置需要更新
            var newKey = $"{_currentLine}:{_currentChar}";
            _changedChars.Add(newKey);

            // 使用增量更新
            RenderVisibleLines();
            PlaceCaretAtCurrent();
        }

        // 优化后的退格键处理方法，使用增量更新
        private void TypingDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle space here because in some cases it may be translated differently; swallow the key so RichTextBox won't insert it
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                // process space as typed character
                ProcessTypedChar(' ');
                return;
            }

            if (e.Key == Key.Back)
            {
                e.Handled = true;
                // if at start of document and nothing to delete, ignore
                if (_currentLine == 0 && _currentChar == 0) return;

                // 记录当前位置，以便稍后标记为需要更新
                string currentKey = $"{_currentLine}:{_currentChar}";

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
                
                // 标记删除的字符位置需要更新
                _changedChars.Add(delKey);

                // 移动光标前，先清除当前光标位置的背景
                if (!string.IsNullOrEmpty(_currentCaretKey))
                {
                    _changedChars.Add(_currentCaretKey);
                }

                // move caret back
                _currentLine = delLine;
                _currentChar = delChar;

                // if deleted the first char of the first visible line, scroll down one line
                // 如果退格到当前视口的第一个字符之前，向下滚动1行
                if (_currentLine < _visibleStartLine)
                {
                    // 精确控制滚动距离：向下滚动1行
                    int newStartLine = Math.Max(0, _visibleStartLine - 1);
                    if (newStartLine != _visibleStartLine)
                    {
                        _visibleStartLine = newStartLine;
                        _needsScroll = true;
                        _targetScrollLine = newStartLine;
                    }
                }

                // 标记新光标位置需要更新
                string newKey = $"{_currentLine}:{_currentChar}";
                _changedChars.Add(newKey);

                // 使用增量更新
                RenderVisibleLines();
                PlaceCaretAtCurrent();
            }
        }

        private void AdvancePosition()
        {
            // move to next character; if at end of line, move to next line start
            var line = (_currentLine < _allLines.Length) ? (_allLines[_currentLine] ?? string.Empty) : string.Empty;
            if (_currentChar + 1 < line.Length)
            {
                _currentChar++;
            }
            else
            {
                // move to next line
                if (_currentLine + 1 < _allLines.Length)
                {
                    _currentLine++;
                    _currentChar = 0;
                }
                else
                {
                    // reached end of document - keep position at end
                    _currentChar = line.Length;
                }
            }
        }

        // 更新光标背景，与增量更新机制兼容
        private void UpdateCaretBackgrounds(int? line, int? charIndex)
        {
            if (line.HasValue && charIndex.HasValue)
            {
                // 获得焦点时，标记当前光标位置需要更新
                string key = $"{line.Value}:{charIndex.Value}";
                _changedChars.Add(key);
                
                // 如果之前有光标位置，也需要更新
                if (!string.IsNullOrEmpty(_currentCaretKey))
                {
                    _changedChars.Add(_currentCaretKey);
                }
                
                // 使用增量更新
                RenderVisibleLines();
            }
            else
            {
                // 失去焦点时，清除光标背景
                if (!string.IsNullOrEmpty(_currentCaretKey))
                {
                    _changedChars.Add(_currentCaretKey);
                    _currentCaretKey = null;
                    
                    // 使用增量更新
                    RenderVisibleLines();
                }
            }
        }

        // 查找资源文件
        private string FindAssetFile(string fileName)
        {
            try
            {
                // 首先在当前目录查找
                var currentDir = Directory.GetCurrentDirectory();
                var currentPath = Path.Combine(currentDir, fileName);
                if (File.Exists(currentPath))
                    return currentPath;

                // 然后在上级目录的Assets/Texts中查找
                var parentDir = Directory.GetParent(currentDir)?.FullName;
                if (!string.IsNullOrEmpty(parentDir))
                {
                    var parentPath = Path.Combine(parentDir, "Assets", "Texts", fileName);
                    if (File.Exists(parentPath))
                        return parentPath;
                }

                // 最后在项目根目录的Assets/Texts中查找
                var projectRoot = Directory.GetParent(parentDir ?? string.Empty)?.FullName;
                if (!string.IsNullOrEmpty(projectRoot))
                {
                    var projectPath = Path.Combine(projectRoot, "Assets", "Texts", fileName);
                    if (File.Exists(projectPath))
                        return projectPath;
                }
            }
            catch
            {
                // 忽略错误，返回空字符串
            }

            return string.Empty;
        }

        // 将光标定位到当前位置
        private void PlaceCaretAtCurrent()
        {
            if (TypingDisplay == null || _runMap.Count == 0) return;

            try
            {
                var key = $"{_currentLine}:{_currentChar}";
                if (_runMap.TryGetValue(key, out var run))
                {
                    // 使用RichTextBox的内置功能定位光标
                    var textPointer = run.ContentStart;
                    TypingDisplay.CaretPosition = textPointer;
                    
                    // 确保光标可见
                    run.BringIntoView();
                    
                    // 强制更新布局
                    TypingDisplay.UpdateLayout();
                }
            }
            catch
            {
                // 忽略错误
            }
        }

        // 获取文档行高
        private double GetDocumentLineHeight()
        {
            try
            {
                if (TypingDisplay?.Document != null)
                {
                    return TypingDisplay.Document.LineHeight;
                }
            }
            catch
            {
                // 忽略错误
            }

            // 返回默认行高
            return 20.0;
        }

        // 在RichTextBox中滚动到指定行
        private void ScrollToLineInRichTextBox(int lineIndex)
        {
            try
            {
                if (TypingDisplay?.Document != null)
                {
                    // 计算目标位置
                    double lineHeight = GetDocumentLineHeight();
                    double targetOffset = lineIndex * lineHeight;
                    
                    // 获取当前滚动位置
                    double currentOffset = TypingDisplay.VerticalOffset;
                    
                    // 检查滚动距离是否足够大（避免微小滚动）
                    double scrollDistance = Math.Abs(targetOffset - currentOffset);
                    
                    // 只有当滚动距离大于行高的一半时才执行滚动
                    if (scrollDistance > lineHeight / 2)
                    {
                        // 精确控制滚动距离，确保每次滚动指定的行数
                        // 使用ScrollViewer进行滚动
                        if (TypingDisplay.Template.FindName("PART_ContentHost", TypingDisplay) is ScrollViewer scrollViewer)
                        {
                            scrollViewer.ScrollToVerticalOffset(targetOffset);
                        }
                        else
                        {
                            // 备用方法：直接使用RichTextBox的滚动方法
                            TypingDisplay.ScrollToVerticalOffset(targetOffset);
                        }
                        
                        // 强制更新布局以确保滚动生效
                        TypingDisplay.UpdateLayout();
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
        }

        // 窗口按钮事件处理程序
        private void WindowTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // 菜单项事件处理程序
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void NameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 显示输入对话框，让用户输入姓名
            var inputDialog = new InputDialog("设置姓名", TesterName ?? string.Empty);
            if (inputDialog.ShowDialog() == true)
            {
                TesterName = inputDialog.InputText;
                Properties.Settings.Default.TesterName = TesterName;
                Properties.Settings.Default.Save();
            }
        }

        private void TimingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 切换计时状态
            _isTimingEnabled = !_isTimingEnabled;
            
            if (_isTimingEnabled)
            {
                _startTime = DateTime.Now;
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        }

        private void AnalysisMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 显示分析窗口
            ShowAnalysis();
        }

        private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 显示帮助信息
            var helpDialog = new HelpDialog();
            helpDialog.Owner = this;
            helpDialog.ShowDialog();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 显示关于信息
            var aboutDialog = new AboutDialog();
            aboutDialog.Owner = this;
            aboutDialog.ShowDialog();
        }
    }
}