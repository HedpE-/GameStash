using Common.Lib.HelperClasses;
using GameStash.Core.Models;
using GameStash.MVVM.GUI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameStash.MVVM.GUI.Views
{
    public partial class ImportGamesView : UserControl
    {
        public ImportGamesView()
        {
            InitializeComponent();
        }

        private void CheckAllBox_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            foreach (var folder in (DataContext as MainViewModel).FoundFolders)
            {
                folder.IsSelected = true;
            }
        }

        private void CheckAllBox_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            foreach (var folder in (DataContext as MainViewModel).FoundFolders)
            {
                folder.IsSelected = false;
            }
        }

        private void ComboBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var viewModel = DataContext as MainViewModel;
                var comboBox = (ComboBox)sender;
                var searchText = comboBox.Text;
                var game = (Game)comboBox.DataContext;
                game.MatchName = searchText;
                game.MatchIgdbGame(viewModel.Igdb_Client_Id, viewModel.Igdb_Secret);
            }
        }

        private void Folder_MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string text = ((Game)((MenuItem)sender).DataContext).Folder;

            if (string.IsNullOrEmpty(text))
                return;

            bool copied = Tools.TryCopyToClipboard(text);
            if (!copied)
                MessageBox.Show("Failed to copy the text to the clipboard. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MatchName_MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string text = ((Game)((MenuItem)sender).DataContext).MatchName;

            if (string.IsNullOrEmpty(text))
                return;

            bool copied = Tools.TryCopyToClipboard(text);
            if (!copied)
                MessageBox.Show("Failed to copy the text to the clipboard. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
