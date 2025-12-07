using System.Windows;
using System.Windows.Input;

namespace WPF_Typing
{
    public partial class NameDialog : Window
    {
        public NameDialog(string? initialName = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(initialName))
            {
                NameTextBox.Text = initialName;
                NameTextBox.SelectAll();
            }
            NameTextBox.Focus();
        }

        // The trimmed text entered by the user. May be empty or null.
        public string? EnteredName { get; private set; }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            EnteredName = NameTextBox.Text?.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;
            try { DragMove(); } catch { }
        }
    }
}

