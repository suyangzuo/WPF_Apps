using System;
using System.Globalization;
using System.Windows;

namespace WPF_Typing
{
    public partial class CountdownDialog : Window
    {
        public bool EnableCountdown { get; set; }
        public string HoursText { get; set; }
        public string MinutesText { get; set; }
        public string SecondsText { get; set; }

        public TimeSpan CountdownDuration { get; private set; } = TimeSpan.FromMinutes(1);

        public CountdownDialog(TimeSpan defaultDuration, bool enableCountdown)
        {
            InitializeComponent();

            HoursText = ((int)defaultDuration.TotalHours).ToString(CultureInfo.InvariantCulture);
            MinutesText = defaultDuration.Minutes.ToString(CultureInfo.InvariantCulture);
            SecondsText = defaultDuration.Seconds.ToString(CultureInfo.InvariantCulture);
            EnableCountdown = enableCountdown;

            DataContext = this;

            // 添加输入限制和自动进位处理
            HoursBox.PreviewTextInput += TextBox_PreviewTextInput;
            HoursBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            HoursBox.TextChanged += HoursBox_TextChanged;
            MinutesBox.PreviewTextInput += TextBox_PreviewTextInput;
            MinutesBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            MinutesBox.TextChanged += MinutesBox_TextChanged;
            SecondsBox.PreviewTextInput += TextBox_PreviewTextInput;
            SecondsBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            SecondsBox.TextChanged += SecondsBox_TextChanged;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            // 如果输入框为空，自动设置为0
            if (string.IsNullOrWhiteSpace(HoursText))
            {
                HoursText = "0";
                HoursBox.Text = "0";
            }
            if (string.IsNullOrWhiteSpace(MinutesText))
            {
                MinutesText = "0";
                MinutesBox.Text = "0";
            }
            if (string.IsNullOrWhiteSpace(SecondsText))
            {
                SecondsText = "0";
                SecondsBox.Text = "0";
            }

            // 处理自动进位（确保分和秒都在有效范围内）
            NormalizeTimeInput();

            if (!TryParseTime(out var ts))
            {
                ShowDarkWarning("请输入有效的时、分、秒（非负整数）。");
                return;
            }

            CountdownDuration = ts;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool TryParseTime(out TimeSpan ts)
        {
            ts = TimeSpan.Zero;

            if (!int.TryParse(HoursText ?? "0", out var h) || h < 0) return false;
            if (!int.TryParse(MinutesText ?? "0", out var m) || m < 0) return false;
            if (!int.TryParse(SecondsText ?? "0", out var s) || s < 0) return false;

            ts = new TimeSpan(h, m, s);
            return true;
        }

        // 只允许输入数字
        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // 显式阻止空格和其他非数字字符
            foreach (char c in e.Text)
            {
                if (c == ' ' || !char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        // 允许退格键和其他控制键，但阻止空格键
        private void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 显式阻止空格键
            if (e.Key == System.Windows.Input.Key.Space)
            {
                e.Handled = true;
                return;
            }

            // 允许退格、删除、方向键、Tab等控制键
            if (e.Key == System.Windows.Input.Key.Back ||
                e.Key == System.Windows.Input.Key.Delete ||
                e.Key == System.Windows.Input.Key.Left ||
                e.Key == System.Windows.Input.Key.Right ||
                e.Key == System.Windows.Input.Key.Up ||
                e.Key == System.Windows.Input.Key.Down ||
                e.Key == System.Windows.Input.Key.Tab ||
                e.Key == System.Windows.Input.Key.Home ||
                e.Key == System.Windows.Input.Key.End)
            {
                return; // 允许这些键
            }

            // 允许Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X等组合键
            if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                return;
            }

            // 其他键不做处理，让系统默认处理
        }

        // 处理秒的自动进位
        private void SecondsBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SecondsText)) return;

            if (int.TryParse(SecondsText, out var seconds) && seconds >= 60)
            {
                // 计算需要进位的分钟数
                int additionalMinutes = seconds / 60;
                int remainingSeconds = seconds % 60;

                // 更新秒
                SecondsText = remainingSeconds.ToString(CultureInfo.InvariantCulture);
                SecondsBox.Text = SecondsText;

                // 更新分
                int currentMinutes = 0;
                int.TryParse(MinutesText ?? "0", out currentMinutes);
                MinutesText = (currentMinutes + additionalMinutes).ToString(CultureInfo.InvariantCulture);
                MinutesBox.Text = MinutesText;

                // 检查分是否需要进一步进位到小时
                NormalizeMinutes();
            }
        }

        // 处理分的自动进位
        private void MinutesBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            NormalizeMinutes();
        }

        // 规范化分钟（如果 >= 60，进位到小时）
        private void NormalizeMinutes()
        {
            if (string.IsNullOrWhiteSpace(MinutesText)) return;

            if (int.TryParse(MinutesText, out var minutes) && minutes >= 60)
            {
                // 计算需要进位的小时数
                int additionalHours = minutes / 60;
                int remainingMinutes = minutes % 60;

                // 更新分
                MinutesText = remainingMinutes.ToString(CultureInfo.InvariantCulture);
                MinutesBox.Text = MinutesText;

                // 更新小时
                int currentHours = 0;
                int.TryParse(HoursText ?? "0", out currentHours);
                HoursText = (currentHours + additionalHours).ToString(CultureInfo.InvariantCulture);
                HoursBox.Text = HoursText;
            }
        }

        // 处理小时的文本变化（不需要进位，但需要更新绑定）
        private void HoursBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // 小时不需要进位，只需要更新绑定
        }

        // 规范化时间输入（确保分和秒都在有效范围内）
        private void NormalizeTimeInput()
        {
            int hours = 0;
            int minutes = 0;
            int seconds = 0;

            int.TryParse(HoursText ?? "0", out hours);
            int.TryParse(MinutesText ?? "0", out minutes);
            int.TryParse(SecondsText ?? "0", out seconds);

            // 处理秒的进位
            if (seconds >= 60)
            {
                minutes += seconds / 60;
                seconds = seconds % 60;
            }

            // 处理分的进位
            if (minutes >= 60)
            {
                hours += minutes / 60;
                minutes = minutes % 60;
            }

            // 更新显示
            HoursText = hours.ToString(CultureInfo.InvariantCulture);
            HoursBox.Text = HoursText;
            MinutesText = minutes.ToString(CultureInfo.InvariantCulture);
            MinutesBox.Text = MinutesText;
            SecondsText = seconds.ToString(CultureInfo.InvariantCulture);
            SecondsBox.Text = SecondsText;
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
            DialogResult = false;
            Close();
        }

        private void ShowDarkWarning(string message)
        {
            var dlg = new DarkDialog
            {
                Owner = this,
                DialogTitle = "提示",
                DialogMessage = message
            };
            dlg.ShowDialog();
        }
    }
}

