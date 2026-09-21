using System.Windows;
using DialuxToRevit.Addin.ViewModels;

namespace DialuxToRevit.Addin.Views
{
    /// <summary>The mapping dialog.</summary>
    public partial class MappingWindow : Window
    {
        public MappingWindow(MappingViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        public MappingViewModel ViewModel { get; }

        private void OnPlaceClicked(object sender, RoutedEventArgs e)
        {
            // Commit an offset the user is still editing; without this, a value
            // typed into the last cell is silently dropped when the grid never
            // loses focus before the dialog closes.
            PlaceButton.Focus();

            DialogResult = true;
        }
    }
}
