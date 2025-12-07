using System.Windows;
using System.Windows.Documents;

namespace WPF_Typing
{
    public partial class AnalysisDialog : Window
    {
        public AnalysisDialog(string analysisText)
        {
            InitializeComponent();
            
            // 清除现有内容
            AnalysisText.Inlines.Clear();
            
            // 将文本按行分割
            string[] lines = analysisText.Split('\n');
            
            // 添加每一行
            for (int i = 0; i < lines.Length; i++)
            {
                AnalysisText.Inlines.Add(new Run(lines[i]));
                
                // 如果不是最后一行，添加换行
                if (i < lines.Length - 1)
                {
                    AnalysisText.Inlines.Add(new LineBreak());
                }
            }
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