using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPF_Typing
{
    public partial class AnalysisDialog : Window
    {
        public AnalysisDialog(
            string testerName,
            string? articlePath,
            DateTime? testStartTime,
            DateTime? testEndTime,
            double completionRate,
            TimeSpan elapsedTime,
            double accuracy,
            int backspaceCount,
            int completedChars,
            int totalChars,
            int speed)
        {
            InitializeComponent();

            BuildStatisticsSection(testerName, articlePath, testStartTime, testEndTime, completionRate, elapsedTime, accuracy, backspaceCount, completedChars, totalChars, speed);
        }

        private void BuildStatisticsSection(string testerName, string? articlePath, DateTime? testStartTime, DateTime? testEndTime, 
            double completionRate, TimeSpan elapsedTime, double accuracy, int backspaceCount, int completedChars, int totalChars, int speed)
        {
            StatisticsText.Inlines.Clear();

            // 计算所有标题的最大宽度
            string[] labels = { "姓名", "练习文章", "测试起始时间", "测试结束时间", "完成字符数", "完成率", "用时", "正确率", "退格次数", "速度" };
            double maxLabelWidth = CalculateMaxLabelWidth(labels);

            AddStatisticLine("姓名", testerName ?? "未知", "#4D8BCF", maxLabelWidth);
            AddArticlePathLine("练习文章", articlePath, "#3BA", maxLabelWidth);
            AddTimeStatisticLine("测试起始时间", FormatDateTime(testStartTime), "#9DCBFF", maxLabelWidth);
            AddTimeStatisticLine("测试结束时间", FormatDateTime(testEndTime), "#9DCBFF", maxLabelWidth);
            AddCompletedCharsLine("完成字符数", completedChars, totalChars, maxLabelWidth);
            AddPercentageStatisticLine("完成率", completionRate, "#FFA500", maxLabelWidth);
            AddTimeStatisticLine("用时", FormatTimeSpan(elapsedTime), "#c68", maxLabelWidth);
            AddPercentageStatisticLine("正确率", accuracy, "#ADFF2F", maxLabelWidth);
            AddStatisticLine("退格次数", backspaceCount.ToString(), "#32CD32", maxLabelWidth);
            AddSpeedLine("速度", speed, "#c68", maxLabelWidth);
        }

        private double CalculateMaxLabelWidth(string[] labels)
        {
            double maxWidth = 0;
            var typeface = new Typeface(StatisticsText.FontFamily, StatisticsText.FontStyle, StatisticsText.FontWeight, StatisticsText.FontStretch);
            double fontSize = StatisticsText.FontSize;

            foreach (string label in labels)
            {
                var formattedText = new FormattedText(
                    label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                maxWidth = Math.Max(maxWidth, formattedText.Width);
            }

            return maxWidth;
        }

        private void AddLabel(string label, double labelWidth)
        {
            var labelContainer = new InlineUIContainer
            {
                BaselineAlignment = BaselineAlignment.Baseline
            };
            var labelTextBlock = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDD")),
                Width = labelWidth,
                TextAlignment = TextAlignment.Right
            };
            labelContainer.Child = labelTextBlock;
            StatisticsText.Inlines.Add(labelContainer);
        }

        private void AddStatisticLine(string label, string value, string valueColor, double labelWidth)
        {
            AddLabel(label, labelWidth);
            StatisticsText.Inlines.Add(new Run(": ") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
            StatisticsText.Inlines.Add(new Run(value) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(valueColor)) });
            StatisticsText.Inlines.Add(new LineBreak());
        }

        private void AddTimeStatisticLine(string label, string timeValue, string numberColor, double labelWidth)
        {
            AddLabel(label, labelWidth);
            StatisticsText.Inlines.Add(new Run(": ") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
            
            if (timeValue == "未记录")
            {
                StatisticsText.Inlines.Add(new Run(timeValue) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(numberColor)) });
            }
            else
            {
                // 时间格式：HH:MM:SS，将数字和冒号分开设置颜色
                string[] parts = timeValue.Split(':');
                for (int i = 0; i < parts.Length; i++)
                {
                    StatisticsText.Inlines.Add(new Run(parts[i]) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(numberColor)) });
                    if (i < parts.Length - 1)
                    {
                        StatisticsText.Inlines.Add(new Run(":") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
                    }
                }
            }
            
            StatisticsText.Inlines.Add(new LineBreak());
        }

        private void AddPercentageStatisticLine(string label, double value, string numberColor, double labelWidth)
        {
            AddLabel(label, labelWidth);
            StatisticsText.Inlines.Add(new Run(": ") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
            string formattedValue = FormatPercentage(value);
            
            // 将数值和小数点分开设置颜色
            if (formattedValue.Contains('.'))
            {
                string[] parts = formattedValue.Split('.');
                // 添加整数部分
                StatisticsText.Inlines.Add(new Run(parts[0]) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(numberColor)) });
                // 添加小数点
                StatisticsText.Inlines.Add(new Run(".") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
                // 添加小数部分（如果存在）
                if (parts.Length > 1)
                {
                    StatisticsText.Inlines.Add(new Run(parts[1]) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(numberColor)) });
                }
            }
            else
            {
                // 如果没有小数点，直接添加整个数值（统一使用测试完毕后的颜色）
                StatisticsText.Inlines.Add(new Run(formattedValue) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(numberColor)) });
            }
            
            // 使用 InlineUIContainer 包装 TextBlock 以设置 Margin
            var percentContainer = new InlineUIContainer
            {
                BaselineAlignment = BaselineAlignment.Baseline
            };
            var percentTextBlock = new TextBlock
            {
                Text = "%",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")),
                Margin = new Thickness(3, 0, 0, 0) // 左边距 3 像素
            };
            percentContainer.Child = percentTextBlock;
            StatisticsText.Inlines.Add(percentContainer);
            
            StatisticsText.Inlines.Add(new LineBreak());
        }

        private void AddArticlePathLine(string label, string? articlePath, string textColor, double labelWidth)
        {
            AddLabel(label, labelWidth);
            StatisticsText.Inlines.Add(new Run(": ") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
            
            if (string.IsNullOrEmpty(articlePath))
            {
                StatisticsText.Inlines.Add(new Run("未选择") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor)) });
            }
            else
            {
                try
                {
                    var fileInfo = new System.IO.FileInfo(articlePath);
                    var folderName = fileInfo.Directory?.Name ?? "未知文件夹";
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(fileInfo.Name);
                    
                    // 文件夹名
                    StatisticsText.Inlines.Add(new Run(folderName) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor)) });
                    // 连字符用不同颜色
                    StatisticsText.Inlines.Add(new Run(" - ") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
                    // 文件名
                    StatisticsText.Inlines.Add(new Run(fileName) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor)) });
                }
                catch
                {
                    StatisticsText.Inlines.Add(new Run("未知") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor)) });
                }
            }
            
            StatisticsText.Inlines.Add(new LineBreak());
        }

        private void AddCompletedCharsLine(string label, int completedChars, int totalChars, double labelWidth)
        {
            AddLabel(label, labelWidth);
            StatisticsText.Inlines.Add(new Run(": ") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
            
            // 完成字符数 - 使用 #FFD700（金色，与正确率互换）
            StatisticsText.Inlines.Add(new Run(completedChars.ToString()) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")) });
            
            // 斜杠，使用 InlineUIContainer 来设置 Margin
            var slashContainer = new InlineUIContainer
            {
                BaselineAlignment = BaselineAlignment.Baseline
            };
            var slashTextBlock = new TextBlock
            {
                Text = "/",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")),
                Margin = new Thickness(2, 0, 2, 0) // 左右各2像素边距
            };
            slashContainer.Child = slashTextBlock;
            StatisticsText.Inlines.Add(slashContainer);
            
            // 总字符数 - 使用 #FFD700（金色，与正确率互换）
            StatisticsText.Inlines.Add(new Run(totalChars.ToString()) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")) });
            
            StatisticsText.Inlines.Add(new LineBreak());
        }

        private void AddSpeedLine(string label, int speed, string numberColor, double labelWidth)
        {
            AddLabel(label, labelWidth);
            StatisticsText.Inlines.Add(new Run(": ") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
            
            // 速度值（整数）
            StatisticsText.Inlines.Add(new Run(speed.ToString()) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7E")) });
            
            // 单位文本"字符/分钟"整体左边距，使用空的 InlineUIContainer 来设置
            var leftMarginContainer = new InlineUIContainer
            {
                BaselineAlignment = BaselineAlignment.Baseline
            };
            var leftMarginTextBlock = new TextBlock
            {
                Margin = new Thickness(4, 0, 0, 0) // 左边距 2 像素
            };
            leftMarginContainer.Child = leftMarginTextBlock;
            StatisticsText.Inlines.Add(leftMarginContainer);
            
            // 添加"字符"
            StatisticsText.Inlines.Add(new Run("字符") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
            
            // 斜杠，使用 InlineUIContainer 来设置左右边距和更深的灰色
            var slashContainer = new InlineUIContainer
            {
                BaselineAlignment = BaselineAlignment.Baseline
            };
            var slashTextBlock = new TextBlock
            {
                Text = "/",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")), // 更深的灰色
                Margin = new Thickness(2, 0, 2, 0) // 左右各2像素边距
            };
            slashContainer.Child = slashTextBlock;
            StatisticsText.Inlines.Add(slashContainer);
            
            // 添加"分钟"
            StatisticsText.Inlines.Add(new Run("分钟") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")) });
            
            StatisticsText.Inlines.Add(new LineBreak());
        }

        private string FormatDateTime(DateTime? dt)
        {
            if (!dt.HasValue) return "未记录";
            return $"{dt.Value.Hour:D2}:{dt.Value.Minute:D2}:{dt.Value.Second:D2}";
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private string FormatPercentage(double value)
        {
            // 先格式化为2位小数
            string formatted = value.ToString("F2");
            // 去掉末尾的0
            formatted = formatted.TrimEnd('0');
            // 如果最后是小数点，也去掉
            formatted = formatted.TrimEnd('.');
            return formatted;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
