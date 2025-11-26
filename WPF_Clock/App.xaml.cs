using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media;

namespace WPF_Clock
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // 确保应用程序使用深色主题
            ApplyDarkThemeToApp();
        }

        private void ApplyDarkThemeToApp()
        {
            // 在应用启动时设置深色主题资源
            Resources.MergedDictionaries.Clear();

            // 创建深色主题资源字典
            ResourceDictionary darkTheme = new ResourceDictionary();

            // 设置窗口背景为深色
            SolidColorBrush darkBrush = new SolidColorBrush(Color.FromRgb(26, 26, 26));
            darkTheme.Add("WindowBackgroundBrush", darkBrush);

            // 确保窗口文本为白色
            SolidColorBrush whiteBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            darkTheme.Add("WindowTextBrush", whiteBrush);

            // 添加到应用资源
            Resources.MergedDictionaries.Add(darkTheme);
        }
    }
}