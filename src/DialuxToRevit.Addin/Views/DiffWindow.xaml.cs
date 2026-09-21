using System.Windows;
using DialuxToRevit.Addin.ViewModels;

namespace DialuxToRevit.Addin.Views
{
    /// <summary>Shows what a re-import would change, before it changes it.</summary>
    public partial class DiffWindow : Window
    {
        public DiffWindow(DiffViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
