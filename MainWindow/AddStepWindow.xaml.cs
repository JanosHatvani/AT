using MainWindow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TestAutomationUI
{
    public partial class AddStepWindow : Window
    {
        public AddStepWindow()
        {
            InitializeComponent();
        }

        public string StepName => stepNameTextBox.Text;
        public string Action => ((ComboBoxItem)actionComboBox.SelectedItem)?.Content?.ToString();
        public string Target => targetTextBox.Text;
        public string Property => ((ComboBoxItem)propertyTypeComboBox.SelectedItem)?.Content?.ToString();
        public string Parameter => parametersTextBox.Text;
        public string TimeoutText => timeoutTextBox.Text;
        public string TestName => testNameTextBox.Text;

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {

            DialogResult = true;
            CustomMessageBox.Show("Lépés hozzáadva!", "Sikeres hozzáadás");
            Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
