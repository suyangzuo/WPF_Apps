using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WPF_Typing
{
    public partial class HelpDialog : Window
    {
        public HelpDialog()
        {
            InitializeComponent();
            BuildHelpText();
        }

        private void BuildHelpText()
        {
            HelpText.Inlines.Clear();

            // 标题
            HelpText.Inlines.Add(new Run("使用说明:")
            {
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("DarkGray"))
            });
            HelpText.Inlines.Add(new LineBreak());
            HelpText.Inlines.Add(new LineBreak());

            // 第1条
            AddHelpItem("1", "从'文本选择'菜单中选择练习文本");
            HelpText.Inlines.Add(new LineBreak());

            // 第2条（包含子项）
            AddHelpItem("2", "绿色播放按钮功能：");
            HelpText.Inlines.Add(new LineBreak());
            AddHelpSubItem("(1)", "随机已勾选：随机载入文章");
            HelpText.Inlines.Add(new LineBreak());
            AddHelpSubItem("(2)", "随机未勾选：重新载入当前文章");
            HelpText.Inlines.Add(new LineBreak());

            // 第3条
            AddHelpItem("3", "输入第一个字符时，测试自动开始");
            HelpText.Inlines.Add(new LineBreak());

            // 第4条
            AddHelpItem("4", "点击红色停止按钮，可结束测试");
        }

        private void AddHelpItem(string number, string text)
        {
            // 序号颜色
            HelpText.Inlines.Add(new Run(number)
            {
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4D8BCF")) // 蓝色
            });
            // 点号颜色
            HelpText.Inlines.Add(new Run(".")
            {
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("Gray")) // 橙色
            });
            // 文本颜色
            HelpText.Inlines.Add(new Run(" " + text)
            {
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDD")) // 浅灰色
            });
        }

        private void AddHelpSubItem(string subNumber, string text)
        {
            // 子序号颜色（如 "(1)"）
            HelpText.Inlines.Add(new Run("  " + subNumber)
            {
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D77DE")) // 金色
            });
            // 文本颜色
            HelpText.Inlines.Add(new Run(" " + text)
            {
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAA")) // 浅灰色
            });
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