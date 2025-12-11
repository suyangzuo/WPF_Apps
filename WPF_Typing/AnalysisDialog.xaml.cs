using System.Windows;
using System.Windows.Documents;

namespace WPF_Typing
{
    public partial class AnalysisDialog : Window
    {
        public AnalysisDialog(string analysisText)
        {
            InitializeComponent();
            
            AnalysisText.Inlines.Clear();
            
            string[] lines = analysisText.Split('\n');
            
            for (int i = 0; i < lines.Length; i++)
            {
                AnalysisText.Inlines.Add(new Run(lines[i]));
                
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