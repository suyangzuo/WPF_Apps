using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell; // for WindowChrome
using System.ComponentModel; // for DesignerProperties
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace WPF_Typing
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private readonly string _stateFilePath;
        private readonly string _textsRoot;

        // Custom maximize state: when true the window is "maximized" with an outer margin
        private bool _isCustomMaximized = false;
        private Rect _restoreBounds = Rect.Empty; // stores normal window bounds to restore to
        private readonly double _outerMargin = 0; // DIP margin to leave from screen edges

        // pending drag state when clicking on titlebar while maximized
        private bool _isPendingDrag = false;
        private POINT _pendingDragCursor;

        public MainWindow()
        {
            InitializeComponent();

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

                                if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var entry in prop.Value.EnumerateArray())
                                    {
                                        if (entry.ValueKind != JsonValueKind.Object) continue;

                                        string? fileName = null;
                                        string charsText = string.Empty;

                                        if (entry.TryGetProperty("文件名", out var fn) && fn.ValueKind == JsonValueKind.String)
                                            fileName = fn.GetString();
                                        else if (entry.TryGetProperty("filename", out var fn2) && fn2.ValueKind == JsonValueKind.String)
                                            fileName = fn2.GetString();

                                        if (entry.TryGetProperty("字符数", out var cs))
                                        {
                                            if (cs.ValueKind == JsonValueKind.Number && cs.TryGetInt32(out var n)) charsText = n.ToString();
                                            else if (cs.ValueKind == JsonValueKind.String) charsText = cs.GetString() ?? string.Empty;
                                        }
                                        else if (entry.TryGetProperty("chars", out var cs2))
                                        {
                                            if (cs2.ValueKind == JsonValueKind.Number && cs2.TryGetInt32(out var n2)) charsText = n2.ToString();
                                            else if (cs2.ValueKind == JsonValueKind.String) charsText = cs2.GetString() ?? string.Empty;
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
                                        catch { }

                                        // create header as horizontal panel with colored parts
                                        var panel = new StackPanel { Orientation = Orientation.Horizontal };

                                        if (!string.IsNullOrEmpty(seq))
                                        {
                                            var seqTb = new TextBlock
                                            {
                                                Text = seq,
                                                Foreground = (SolidColorBrush)(new BrushConverter().ConvertFromString("#999999")),
                                                Margin = new Thickness(0, 0, 10, 0)
                                            };
                                            panel.Children.Add(seqTb);
                                        }

                                        var titleTb = new TextBlock
                                        {
                                            Text = title,
                                            Foreground = (SolidColorBrush)(new BrushConverter().ConvertFromString("#FFFFFF"))
                                        };
                                        panel.Children.Add(titleTb);

                                        if (!string.IsNullOrEmpty(charsText))
                                        {
                                            var charsTb = new TextBlock
                                            {
                                                Text = charsText,
                                                Foreground = (SolidColorBrush)(new BrushConverter().ConvertFromString("#9DCBFF")),
                                                Margin = new Thickness(16, 0, 0, 0)
                                            };
                                            panel.Children.Add(charsTb);
                                        }

                                        var child = new MenuItem { Tag = Path.Combine(_textsRoot, fileName) };
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
                var dlg = new DarkDialog { Owner = this };
                dlg.DialogTitle = "文本选择";
                dlg.DialogMessage = $"已选择文本: {mi.Header}\n路径：{path}";
                dlg.ShowDialog();
            }
        }

        // Titlebar drag
        private void WindowTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If click source is a button (or inside a button), ignore - buttons handle their own clicks
            if (e.OriginalSource is DependencyObject dobj && FindAncestor<Button>(dobj) != null)
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                // Double-click toggles maximize/restore immediately
                ToggleMaximizeRestore();
                return;
            }

            // If window is maximized (custom or real), start pending drag only — actual restore occurs after mouse moves beyond threshold
            if ((_isCustomMaximized || this.WindowState == WindowState.Maximized) && e.LeftButton == MouseButtonState.Pressed)
            {
                // record initial cursor position (screen pixels)
                if (GetCursorPos(out var cursor))
                {
                    _pendingDragCursor = cursor;
                    _isPendingDrag = true;

                    // capture mouse on the title bar so we receive move/up events
                    try
                    {
                        TitleBarGrid.CaptureMouse();
                        TitleBarGrid.MouseMove += TitleBarGrid_MouseMove;
                        TitleBarGrid.MouseLeftButtonUp += TitleBarGrid_MouseLeftButtonUp;
                    }
                    catch
                    {
                        // ignore
                        _isPendingDrag = false;
                    }
                }

                return;
            }

            // default behavior when not maximized
            try
            {
                DragMove();
            }
            catch
            {
                // ignore
            }
        }

        private void TitleBarGrid_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_isPendingDrag) return;

            // get current cursor
            if (!GetCursorPos(out var cursor)) return;

            // compute pixel distance
            double dx = cursor.X - _pendingDragCursor.X;
            double dy = cursor.Y - _pendingDragCursor.Y;
            double distSq = dx * dx + dy * dy;

            // threshold using system minimum drag distance (convert to pixels using DPI)
            var dpi = VisualTreeHelper.GetDpi(this);
            double minDrag = Math.Max(SystemParameters.MinimumHorizontalDragDistance, SystemParameters.MinimumVerticalDragDistance);
            double minDragPx = minDrag * dpi.DpiScaleX;

            if (distSq >= minDragPx * minDragPx)
            {
                // enough movement -> perform restore+begin drag
                try
                {
                    // release pending state and handlers before performing restoration/drag
                    EndPendingDrag();

                    // call existing logic to restore and begin dragging using current cursor
                    PerformRestoreAndBeginDrag(cursor);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void TitleBarGrid_MouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
        {
            // mouse released without sufficient movement -> cancel pending drag
            if (_isPendingDrag)
            {
                EndPendingDrag();
            }
        }

        private void EndPendingDrag()
        {
            _isPendingDrag = false;
            try
            {
                TitleBarGrid.MouseMove -= TitleBarGrid_MouseMove;
                TitleBarGrid.MouseLeftButtonUp -= TitleBarGrid_MouseLeftButtonUp;
                if (TitleBarGrid.IsMouseCaptured) TitleBarGrid.ReleaseMouseCapture();
            }
            catch { }
        }

        private void PerformRestoreAndBeginDrag(POINT cursor)
        {
            // convert cursor pixels to DIP
            var dpi = VisualTreeHelper.GetDpi(this);
            double cursorDipX = cursor.X / dpi.DpiScaleX;
            double cursorDipY = cursor.Y / dpi.DpiScaleY;

            // determine restore width/height
            double restoreWidth = this.MinWidth;
            double restoreHeight = this.MinHeight;

            if (_restoreBounds != Rect.Empty)
            {
                restoreWidth = Math.Max(_restoreBounds.Width, this.MinWidth);
                restoreHeight = Math.Max(_restoreBounds.Height, this.MinHeight);
            }
            else
            {
                try
                {
                    restoreWidth = Math.Max(this.RestoreBounds.Width, this.MinWidth);
                    restoreHeight = Math.Max(this.RestoreBounds.Height, this.MinHeight);
                }
                catch
                {
                    restoreWidth = Math.Max(this.Width, this.MinWidth);
                    restoreHeight = Math.Max(this.Height, this.MinHeight);
                }
            }

            // get mouse position relative to current window (maximized area)
            var posRelative = Mouse.GetPosition(this);
            double ratioX = (this.ActualWidth > 0) ? (posRelative.X / this.ActualWidth) : 0.5;

            double newLeft = cursorDipX - restoreWidth * ratioX;

            double clickOffsetY = 0;
            if (this.TitleBarGrid != null)
            {
                var posInTitle = Mouse.GetPosition(this.TitleBarGrid);
                clickOffsetY = posInTitle.Y;
            }
            else
            {
                clickOffsetY = posRelative.Y;
            }

            double newTop = cursorDipY - clickOffsetY;

            var work = SystemParameters.WorkArea;
            if (newLeft < work.Left) newLeft = work.Left;
            if (newTop < work.Top) newTop = work.Top;
            if (newLeft + restoreWidth > work.Right) newLeft = work.Right - restoreWidth;
            if (newTop + restoreHeight > work.Bottom) newTop = work.Bottom - restoreHeight;

            // Restore to normal bounds: set size first then position
            this.WindowState = WindowState.Normal;
            this.Width = restoreWidth;
            this.Height = restoreHeight;
            this.Left = newLeft;
            this.Top = newTop;

            _isCustomMaximized = false;

            // Begin dragging asynchronously
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { this.DragMove(); } catch { }
            }));
        }

        private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void ApplyCustomMaximize()
        {
            // Save current bounds if not already saved
            if (!_isCustomMaximized)
            {
                // If the window is currently OS-maximized, prefer RestoreBounds which contains pre-max bounds
                if (this.WindowState == WindowState.Maximized)
                {
                    try
                    {
                        var rb = this.RestoreBounds;
                        if (rb.Width > 0 && rb.Height > 0)
                        {
                            _restoreBounds = new Rect(rb.Left, rb.Top, rb.Width, rb.Height);
                        }
                        else
                        {
                            _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                        }
                    }
                    catch
                    {
                        _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                    }
                }
                else
                {
                    _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                }
            }

            // Compute inset bounds based on work area and outer margin
            var work = SystemParameters.WorkArea;
            double left = work.Left + _outerMargin;
            double top = work.Top + _outerMargin;
            double width = Math.Max(work.Width - 2 * _outerMargin, this.MinWidth);
            double height = Math.Max(work.Height - 2 * _outerMargin, this.MinHeight);

            // Apply
            _isCustomMaximized = true;
            this.WindowState = WindowState.Normal; // ensure normal state so we can set bounds
            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;
        }

        private void RestoreFromCustomMaximize()
        {
            if (_isCustomMaximized && _restoreBounds != Rect.Empty)
            {
                this.WindowState = WindowState.Normal;
                this.Left = _restoreBounds.Left;
                this.Top = _restoreBounds.Top;
                this.Width = _restoreBounds.Width;
                this.Height = _restoreBounds.Height;
                _isCustomMaximized = false;
            }
        }

        private void ToggleMaximizeRestore()
        {
            if (_isCustomMaximized)
            {
                RestoreFromCustomMaximize();
                return;
            }

            // If OS-level maximized, capture RestoreBounds and convert to custom maximize
            if (this.WindowState == WindowState.Maximized)
            {
                // Capture normal bounds from RestoreBounds and apply custom maximize
                try
                {
                    var rb = this.RestoreBounds;
                    if (rb.Width > 0 && rb.Height > 0)
                    {
                        _restoreBounds = new Rect(rb.Left, rb.Top, rb.Width, rb.Height);
                    }
                }
                catch
                {
                    // ignore
                }

                ApplyCustomMaximize();
                return;
            }

            // Otherwise apply custom maximize
            ApplyCustomMaximize();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();
        private void TimingMenuItem_Click(object sender, RoutedEventArgs e) { var dlg = new DarkDialog { Owner = this }; dlg.DialogTitle = "计时"; dlg.DialogMessage = "计时设置（未实现）"; dlg.ShowDialog(); }
        private void NameMenuItem_Click(object sender, RoutedEventArgs e) { var dlg = new DarkDialog { Owner = this }; dlg.DialogTitle = "姓名"; dlg.DialogMessage = "姓名设置（未实现）"; dlg.ShowDialog(); }
        private void AnalysisMenuItem_Click(object sender, RoutedEventArgs e) { var dlg = new DarkDialog { Owner = this }; dlg.DialogTitle = "分析"; dlg.DialogMessage = "分析选项（未实现）"; dlg.ShowDialog(); }
        private void HelpMenuItem_Click(object sender, RoutedEventArgs e) { var dlg = new DarkDialog { Owner = this }; dlg.DialogTitle = "使用说明"; dlg.DialogMessage = "使用说明：\n1. 在 菜单 -> 文本选择 中选择练习文本。\n2. 开始练习后将显示计时和统计。"; dlg.ShowDialog(); }
        private void AboutMenuItem_Click(object sender, RoutedEventArgs e) { var dlg = new DarkDialog { Owner = this }; dlg.DialogTitle = "关于"; dlg.DialogMessage = "打字练习 - 版本 0.1"; dlg.ShowDialog(); }

        private class WindowStateInfo { public double Left { get; set; } public double Top { get; set; } public double Width { get; set; } public double Height { get; set; } public bool IsMaximized { get; set; } }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            // Ignore in designer
            if (DesignerProperties.GetIsInDesignMode(this)) return;

            // If system maximized the window, convert to custom maximized so we can keep outer margin
            if (this.WindowState == WindowState.Maximized && !_isCustomMaximized)
            {
                try
                {
                    var rb = this.RestoreBounds;
                    if (rb.Width > 0 && rb.Height > 0)
                    {
                        _restoreBounds = new Rect(rb.Left, rb.Top, rb.Width, rb.Height);
                    }
                }
                catch
                {
                    // ignore
                }

                ApplyCustomMaximize();
            }
        }
    }
}