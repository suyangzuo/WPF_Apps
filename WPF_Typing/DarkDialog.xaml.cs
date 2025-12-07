using System.Windows;
using System.Windows.Input;

namespace WPF_Typing
{
    public partial class DarkDialog : Window
    {
        public DarkDialog()
        {
            InitializeComponent();
        }

        // Public properties that wrap the generated TextBlock fields (TitleText, MessageText)
        public string DialogTitle
        {
            get => TitleText?.Text ?? string.Empty;
            set
            {
                if (TitleText != null)
                    TitleText.Text = value ?? string.Empty;
            }
        }

        public string DialogMessage
        {
            get => MessageText?.Text ?? string.Empty;
            set
            {
                if (MessageText != null)
                    MessageText.Text = value ?? string.Empty;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                return;
            }

            try
            {
                DragMove();
            }
            catch
            {
                // ignored
            }
        }
    }
}