using System.Windows;


namespace MainWindow
{
    public partial class CustomMessageBox : Window
    {
        public bool? Result { get; private set; }

        private CustomMessageBox(string message, string title, bool showCancel)
        {
            InitializeComponent();
            MessageText.Text = message;
            Title = title;
            if (showCancel)
                CancelButton.Visibility = Visibility.Visible;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }

        public static bool? Show(string message, string title = "Információ", bool showCancel = false, Window owner = null)
        {
            var box = new CustomMessageBox(message, title, showCancel);
            if (owner != null)
                box.Owner = owner;
            box.ShowDialog();
            return box.Result;
        }
    }
}
