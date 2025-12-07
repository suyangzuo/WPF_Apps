using System;
using System.Windows;

namespace WPF_Typing
{
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; } = string.Empty;

        public InputDialog(string title, string defaultValue = "")
        {
            InitializeComponent();
            TitleText.Text = title;
            PromptTextBlock.Text = "请输入:";
            InputTextBox.Text = defaultValue;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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
    }
}