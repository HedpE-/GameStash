using MaterialDesignThemes.Wpf;
using GameStash.MVVM.GUI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GameStash.MVVM.GUI.Views
{
    /// <summary>
    /// Interaction logic for BrowseGamesView.xaml
    /// </summary>
    public partial class BrowseGamesView : UserControl
    {
        public BrowseGamesView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is BrowseGamesViewModel vm)
            {
                vm.ScrollToTopRequested += OnScrollToTopRequested;
            }
        }

        private void OnScrollToTopRequested()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (SearchResultsListBox.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    var border = VisualTreeHelper.GetChild(SearchResultsListBox, 0) as Border;
                    var scrollViewer = VisualTreeHelper.GetChild(border, 0) as ScrollViewer;
                    scrollViewer?.ScrollToTop();
                }
                else
                {
                    SearchResultsListBox.ItemContainerGenerator.StatusChanged += OnStatusChanged;
                }
            });
        }

        private void OnStatusChanged(object sender, EventArgs e)
        {
            if (SearchResultsListBox.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                SearchResultsListBox.ItemContainerGenerator.StatusChanged -= OnStatusChanged;
                OnScrollToTopRequested();
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var viewModel = DataContext as BrowseGamesViewModel;
                if (viewModel != null && viewModel.ExecuteSearchCommand.CanExecute(null))
                    viewModel.ExecuteSearchCommand.Execute(null);
            }
        }

        private async void Game_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!DialogHost.IsDialogOpen("RootDialog") && (((Grid)sender).DataContext is IgdbSharp.Models.Game))
                await DialogHost.Show(((Grid)sender).DataContext, dialogIdentifier: "RootDialog");
        }

        private async void SearchResultsListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(sender as ListBox, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                if (!DialogHost.IsDialogOpen("RootDialog") && item.DataContext is IgdbSharp.Models.Game)
                    await DialogHost.Show(item.DataContext, dialogIdentifier: "RootDialog");
            }
        }
    }
}
