using System.Windows;

namespace TestAutomationUI
{
    public partial class StartWindow : Window
    {
        public StartWindow()
        {
            InitializeComponent();
        }

        private void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            // Példa: megnyitja a főablakot (ahol a tesztelés történik)
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            // Bezárja ezt a kezdőablakot
            this.Close();
        }
    }
}
