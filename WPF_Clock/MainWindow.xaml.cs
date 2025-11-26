using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Resources;

namespace WPF_Clock
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer = null!;
        private const double ClockRadius = 135; // 时钟半径，稍小于画布尺寸（300宽）
        private const double CenterX = 150; // 时钟中心X坐标
        private const double CenterY = 150; // 时钟中心Y坐标

        public MainWindow()
        {
            InitializeComponent();
            InitializeClock();
            StartTimer();

            // 确保窗口使用深色主题
            ApplyDarkTheme();
        }

        private void ApplyDarkTheme()
        {
            // 确保窗口文本为白色
            this.Foreground = Brushes.White;
        }

        private void InitializeClock()
        {
            // 生成时钟刻度和数字
            for (int i = 1; i <= 12; i++)
            {
                // 计算角度（弧度）
                double angle = (i - 3) * Math.PI / 6; // 从3点钟位置开始

                // 生成刻度线 - 确保紧贴边框
                double outerRadius = ClockRadius + 12; // 真正紧贴时钟边缘
                double innerRadius = i % 3 == 0 ? ClockRadius - 5 : ClockRadius; // 每3小时的刻度更长，确保刻度明显

                double startX = CenterX + Math.Cos(angle) * innerRadius;
                double startY = CenterY + Math.Sin(angle) * innerRadius;
                double endX = CenterX + Math.Cos(angle) * outerRadius;
                double endY = CenterY + Math.Sin(angle) * outerRadius;

                Line tickLine = new Line
                {
                    X1 = startX,
                    Y1 = startY,
                    X2 = endX,
                    Y2 = endY,
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = i % 3 == 0 ? 3 : 1
                };
                ClockCanvas.Children.Add(tickLine);

                // 生成数字 - 确保所有数字均匀分布
                double textRadius = ClockRadius - 20; // 数字与刻度的距离，确保在刻度内侧

                // 设置字体大小，主要数字稍大
                int fontSize = i % 3 == 0 ? 16 : 14; // 3、6、9、12的数字稍大

                // 创建TextBlock
                TextBlock textBlock = new TextBlock
                {
                    Text = i.ToString(),
                    FontSize = fontSize,
                    FontWeight = i % 3 == 0 ? FontWeights.Bold : FontWeights.Normal,
                    FontFamily = new FontFamily("Google Sans Code, Consolas"),
                    Foreground = i % 3 == 0 ? Brushes.LightCyan : Brushes.Silver,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // 计算精确的X,Y位置
                double textX = CenterX + Math.Cos(angle) * textRadius;
                double textY = CenterY + Math.Sin(angle) * textRadius;

                // 对于Canvas中的元素，我们需要使用Canvas.Left和Canvas.Top属性而不是Margin
                ClockCanvas.Children.Add(textBlock);

                // 使用更精确的方法计算文本中心位置
                // 测量文本的实际大小，然后根据实际大小进行居中
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double textWidth = textBlock.DesiredSize.Width;
                double textHeight = textBlock.DesiredSize.Height;

                // 根据实际文本大小设置位置，确保文本中心对齐
                Canvas.SetLeft(textBlock, textX - textWidth / 2);
                Canvas.SetTop(textBlock, textY - textHeight / 2);
            }

            // 添加每一秒的刻度线（共60个）
            for (int i = 0; i < 60; i++)
            {
                // 跳过已经存在的主刻度线位置（每5个位置一个主刻度）
                if (i % 5 == 0) continue;

                // 计算角度（弧度）- 从12点钟位置开始，顺时针
                double angle = (i - 15) * Math.PI / 30; // -15是为了从12点位置开始

                // 秒刻度线的参数 - 比主刻度线短且细
                double outerRadius = ClockRadius + 8;  // 稍微短一些
                double innerRadius = ClockRadius + 2;  // 更靠近中心

                double startX = CenterX + Math.Cos(angle) * innerRadius;
                double startY = CenterY + Math.Sin(angle) * innerRadius;
                double endX = CenterX + Math.Cos(angle) * outerRadius;
                double endY = CenterY + Math.Sin(angle) * outerRadius;

                Line secondTickLine = new Line
                {
                    X1 = startX,
                    Y1 = startY,
                    X2 = endX,
                    Y2 = endY,
                    Stroke = Brushes.Gray,  // 使用比主刻度线更浅的颜色
                    StrokeThickness = 0.5   // 更细的线
                };
                ClockCanvas.Children.Add(secondTickLine);
            }

            // 设置中心点位置并缩小中心点
            ClockCenter.Width = 10;
            ClockCenter.Height = 10;
            ClockCenter.Margin = new Thickness(CenterX - ClockCenter.Width / 2, CenterY - ClockCenter.Height / 2, 0, 0);
        }

        private void StartTimer()
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;
            timer.Start();

            // 立即更新一次时间
            UpdateTime();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateTime();
        }

        private void UpdateTime()
        {
            DateTime now = DateTime.Now;

            // 更新日期显示：分别设置数字和"年、月、日"的颜色
            string[] weekdays = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];
            YearText.Text = now.Year.ToString();
            MonthText.Text = now.Month.ToString();
            DayText.Text = now.Day.ToString();
            WeekdayText.Text = weekdays[(int)now.DayOfWeek];

            // 更新时间显示：分别设置数字和冒号的颜色
            HourText.Text = now.Hour.ToString("D2");
            MinuteText.Text = now.Minute.ToString("D2");
            SecondText.Text = now.Second.ToString("D2");

            // 更新时针位置
            double hourAngle = (now.Hour % 12) * Math.PI / 6 + now.Minute * Math.PI / 360 - Math.PI / 2;
            UpdateHandPosition(HourHand, hourAngle, ClockRadius * 0.5);

            // 更新分针位置
            double minuteAngle = now.Minute * Math.PI / 30 + now.Second * Math.PI / 1800 - Math.PI / 2;
            UpdateHandPosition(MinuteHand, minuteAngle, ClockRadius * 0.7);

            // 更新秒针位置
            double secondAngle = now.Second * Math.PI / 30 - Math.PI / 2;
            UpdateHandPosition(SecondHand, secondAngle, ClockRadius * 0.8);
        }

        private void UpdateHandPosition(Line hand, double angle, double length)
        {
            // 计算指针终点坐标
            double endX = CenterX + Math.Cos(angle) * length;
            double endY = CenterY + Math.Sin(angle) * length;

            // 设置指针起点和终点
            hand.X1 = CenterX;
            hand.Y1 = CenterY;
            hand.X2 = endX;
            hand.Y2 = endY;
        }

        // 标题栏拖动事件
        private void WindowTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 只有当点击源不是Button或其子元素时才执行拖动操作
            if (e.OriginalSource is not Button &&
                VisualTreeHelper.GetParent(e.OriginalSource as DependencyObject) is not Button)
            {
                DragMove();
            }
        }

        // 关闭按钮点击事件
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // 关闭按钮鼠标悬停事件
        private void CloseButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                // 使用明确的RGB值设置红色背景，而不是Brushes.Red常量
                button.Background = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                // 确保直接设置背景色属性，避免样式覆盖
                button.SetValue(BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 0, 0)));
                // 强制按钮更新视觉状态
                button.InvalidateVisual();
            }
        }

        // 关闭按钮鼠标离开事件
        private void CloseButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = Brushes.Transparent;
            }
        }

        // 最小化按钮事件处理
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MinimizeButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50));
            }
        }

        private void MinimizeButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = Brushes.Transparent;
            }
        }

        // 最大化按钮事件处理
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void MaximizeButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50));
            }
        }

        private void MaximizeButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = Brushes.Transparent;
            }
        }

        // 退出菜单项点击事件
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // 显示时钟菜单项点击事件
        private void ShowClockMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                // 切换时钟的可见性
                ClockCanvas.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // 关于菜单项点击事件
        /// <summary>
        /// 创建默认图标
        /// </summary>
        /// <returns>默认图标的ImageSource</returns>
        private ImageSource CreateDefaultIcon()
        {
            try
            {
                // 创建一个简单的时钟图标作为默认图标
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    // 绘制一个简单的时钟图标
                    Pen pen = new Pen(Brushes.White, 2);
                    Brush brush = Brushes.White;

                    // 绘制圆形表盘
                    drawingContext.DrawEllipse(null, pen, new Point(8, 8), 7, 7);

                    // 绘制时针
                    drawingContext.DrawLine(pen, new Point(8, 8), new Point(8, 4));

                    // 绘制分针
                    drawingContext.DrawLine(pen, new Point(8, 8), new Point(12, 8));
                }

                // 转换为ImageSource
                RenderTargetBitmap bitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(drawingVisual);
                return bitmap;
            }
            catch
            {
                // 如果连默认图标都创建失败，返回一个简单的矩形
                return new DrawingImage(new GeometryDrawing(Brushes.White, null,
                    new RectangleGeometry(new Rect(0, 0, 16, 16))));
            }
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 创建自定义深色模式对话框
            Window aboutDialog = new Window
            {
                Width = 350,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)), // #2D2D2D
                Foreground = Brushes.White,
                WindowStyle = WindowStyle.None, // 移除默认标题栏
                ShowInTaskbar = false,
                AllowsTransparency = false,
                BorderBrush = Brushes.DimGray, // 添加灰色边框
                BorderThickness = new Thickness(1) // 设置边框宽度为1像素
            };

            // 创建主网格布局
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 内容区域

            // 创建自定义标题栏
            Grid titleBar = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)), // #202020 - 深色标题栏
                Height = 32
            };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 图标
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 标题
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 关闭按钮

            // 应用图标
            Image appIcon = new Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // 使用更可靠的方式加载图标，处理中文路径
            try
            {
                // 尝试从资源加载图标
                StreamResourceInfo? sri =
                    Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ICO/Clock.ico",
                        UriKind.Absolute));
                if (sri is { Stream: not null })
                {
                    BitmapImage iconBitmap = new BitmapImage();
                    iconBitmap.BeginInit();
                    iconBitmap.StreamSource = sri.Stream;
                    iconBitmap.EndInit();
                    iconBitmap.Freeze(); // 提高性能
                    appIcon.Source = iconBitmap;
                    System.Diagnostics.Debug.WriteLine("从资源成功加载图标");
                }
                else
                {
                    throw new Exception("资源流为空");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从资源加载图标失败: {ex.Message}");

                // 如果资源加载失败，尝试从文件系统加载
                try
                {
                    string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ICO",
                        "Clock.ico");
                    System.Diagnostics.Debug.WriteLine($"尝试从文件系统加载图标: {iconPath}");

                    if (System.IO.File.Exists(iconPath))
                    {
                        BitmapImage iconBitmap = new BitmapImage();
                        iconBitmap.BeginInit();
                        iconBitmap.UriSource = new Uri(iconPath);
                        iconBitmap.EndInit();
                        iconBitmap.Freeze(); // 提高性能
                        appIcon.Source = iconBitmap;
                        System.Diagnostics.Debug.WriteLine("从文件系统成功加载图标");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("图标文件不存在");
                        // 使用默认图标或简单的图形替代
                        appIcon.Source = CreateDefaultIcon();
                    }
                }
                catch (Exception fileEx)
                {
                    System.Diagnostics.Debug.WriteLine($"从文件系统加载图标也失败: {fileEx.Message}");
                    // 使用默认图标或简单的图形替代
                    appIcon.Source = CreateDefaultIcon();
                }
            }

            titleBar.Children.Add(appIcon);
            Grid.SetColumn(appIcon, 0);

            // 标题文本
            TextBlock titleText = new TextBlock
            {
                Text = "关于",
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };
            titleBar.Children.Add(titleText);
            Grid.SetColumn(titleText, 1);

            // 关闭按钮
            Button closeButton = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = new ControlTemplate(typeof(Button))
            };

            // 关闭按钮模板
            FrameworkElementFactory buttonFactory = new FrameworkElementFactory(typeof(Border));
            buttonFactory.SetValue(Border.BackgroundProperty,
                new System.Windows.TemplateBindingExtension(Button.BackgroundProperty));
            buttonFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));

            FrameworkElementFactory textFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            textFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            buttonFactory.AppendChild(textFactory);
            closeButton.Template = new ControlTemplate(typeof(Button))
            {
                VisualTree = buttonFactory
            };

            // 关闭按钮事件
            closeButton.Click += (obj, args) => aboutDialog.Close();
            closeButton.MouseEnter += (obj, args) =>
            {
                if (obj is Button btn)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35)); // Windows 10 红色
                }
            };
            closeButton.MouseLeave += (obj, args) =>
            {
                if (obj is Button btn)
                {
                    btn.Background = Brushes.Transparent;
                }
            };

            titleBar.Children.Add(closeButton);
            Grid.SetColumn(closeButton, 2);

            // 标题栏拖动功能
            titleBar.MouseLeftButtonDown += (obj, args) => aboutDialog.DragMove();

            // 添加标题栏到主网格
            mainGrid.Children.Add(titleBar);
            Grid.SetRow(titleBar, 0);

            // 创建内容区域
            StackPanel contentPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            TextBlock appTitleText = new TextBlock
            {
                Text = "桌面时钟",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = Brushes.White
            };

            TextBlock versionText = new TextBlock
            {
                Text = "版本 1.0",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = Brushes.LightGray
            };

            TextBlock descriptionText = new TextBlock
            {
                Text = "基于 .NET 10 和 WPF 框架，适用于 Windows 平台",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap
            };

            // 创建按钮容器
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            // 创建确定按钮
            Button okButton = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(68, 68, 68)), // #444444
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                Margin = new Thickness(0) // 移除边距，确保按钮完全可见
            };

            // 添加鼠标悬停事件处理
            okButton.MouseEnter += (obj, args) =>
            {
                if (obj is Button button)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(100, 100, 100)); // #646464 - 悬停时更亮的灰色
                }
            };

            okButton.MouseLeave += (obj, args) =>
            {
                if (obj is Button button)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(68, 68, 68)); // #444444 - 恢复原始颜色
                }
            };

            okButton.Click += (obj, args) => aboutDialog.Close();

            // 添加按钮到按钮容器
            buttonPanel.Children.Add(okButton);

            // 添加控件到内容面板
            contentPanel.Children.Add(appTitleText);
            contentPanel.Children.Add(versionText);
            contentPanel.Children.Add(descriptionText);
            contentPanel.Children.Add(buttonPanel);

            // 添加内容面板到主网格
            mainGrid.Children.Add(contentPanel);
            Grid.SetRow(contentPanel, 1);

            // 设置窗口内容
            aboutDialog.Content = mainGrid;

            // 显示对话框
            aboutDialog.ShowDialog();
        }
    }
}