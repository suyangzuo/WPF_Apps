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
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
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

            if (!int.TryParse(HoursText, out var h) || h < 0) return false;
            if (!int.TryParse(MinutesText, out var m) || m < 0 || m > 59) return false;
            if (!int.TryParse(SecondsText, out var s) || s < 0 || s > 59) return false;

            ts = new TimeSpan(h, m, s);
            return true;
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

