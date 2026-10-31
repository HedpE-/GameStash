using Common.Lib.HelperClasses;
using MaterialDesignExtensions.Controls;
using GameStash.MVVM.GUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GameStash.MVVM.GUI.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        private string? _lastUsedPath;

        public SettingsView()
        {
            InitializeComponent();
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenDirectoryDialogArguments dialogArgs = new OpenDirectoryDialogArguments()
            {
                Width = 700,
                Height = 600,
                CreateNewDirectoryEnabled = false,
                PathPartsAsButtons = true,
                CurrentDirectory = _lastUsedPath
            };

            OpenDirectoryDialogResult result = await OpenDirectoryDialog.ShowDialogAsync("RootDialog", dialogArgs);

            if (result.Confirmed)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.FolderPath = result.DirectoryInfo.FullName;
                    _lastUsedPath = result.DirectoryInfo.FullName;
                }
            }
        }

        private void TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is MainViewModel viewModel && viewModel.AddFolderCommand.CanExecute(null))
                    viewModel.AddFolderCommand.Execute(null);
            }
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
            => Tools.OpenInBrowser(e.Uri.AbsoluteUri);
    }
}
