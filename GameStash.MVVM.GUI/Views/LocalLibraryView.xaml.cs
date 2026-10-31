using Common.Lib.Extensions;
using MaterialDesignThemes.Wpf;
using GameStash.Core.Models;
using GameStash.MVVM.GUI.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameStash.MVVM.GUI.Views
{
    /// <summary>
    /// Interaction logic for LocalLibraryView.xaml
    /// </summary>
    public partial class LocalLibraryView : UserControl
    {
        public LocalLibraryView()
        {
            InitializeComponent();            
        }

        private void StackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Assigning the Action that scrolls and positions the item at the top
                //vm.ScrollToLetterAction = letter =>
                //{
                //    foreach (var item in GamesItemsControl.Items)
                //    {
                //        if (item is Game game)
                //        {
                //            var firstChar = string.IsNullOrEmpty(game.SortName) ? game.Name?.FirstOrDefault() : game.SortName?.FirstOrDefault();
                //            if (!firstChar.HasValue)
                //                continue;

                //            bool isMatch = false;

                //            if (letter == '#')
                //            {
                //                // If '#' is pressed, match if firstChar is a digit
                //                isMatch = char.IsDigit(firstChar.Value);
                //            }
                //            else
                //            {
                //                // Normal letter match (case insensitive)
                //                isMatch = char.ToUpperInvariant(firstChar.Value) == char.ToUpperInvariant(letter);
                //            }

                //            if (isMatch)
                //            {
                //                // Force container generation
                //                GamesItemsControl.UpdateLayout();

                //                // Invoke later to make sure the container is ready
                //                GamesItemsControl.Dispatcher.BeginInvoke(new Action(() =>
                //                {
                //                    var container = (FrameworkElement)GamesItemsControl.ItemContainerGenerator.ContainerFromItem(item);
                //                    if (container != null)
                //                    {
                //                        // Find the ScrollViewer of the ItemsControl
                //                        ScrollViewer sv = FindScrollViewer(GamesItemsControl);
                //                        if (sv != null)
                //                        {
                //                            // Get the item's position relative to the ScrollViewer
                //                            GeneralTransform transform = container.TransformToAncestor(sv);
                //                            Point position = transform.Transform(new Point(0, 0));

                //                            // Adjust ScrollViewer offset to position item at the top
                //                            sv.ScrollToVerticalOffset(sv.VerticalOffset + position.Y);
                //                        }
                //                    }
                //                }), System.Windows.Threading.DispatcherPriority.Loaded);

                //                break;
                //            }
                //        }
                //    }
                //};
                vm.ScrollToLetterAction = letter =>
                {
                    // 1. Encontrar o índice ou o item correspondente primeiro na memória (rápido)
                    object targetItem = null;
                    foreach (var item in GamesItemsControl.Items)
                    {
                        if (item is Game game)
                        {
                            var firstChar = string.IsNullOrEmpty(game.SortName) ? game.Name?.FirstOrDefault() : game.SortName?.FirstOrDefault();
                            if (!firstChar.HasValue) continue;

                            bool isMatch = letter == '#' ? char.IsDigit(firstChar.Value) : char.ToUpperInvariant(firstChar.Value) == char.ToUpperInvariant(letter);

                            if (isMatch)
                            {
                                targetItem = item;
                                break;
                            }
                        }
                    }

                    // 2. Se encontrarmos o item, fazemos o scroll usando o método nativo do VirtualizingWrapPanel
                    if (targetItem != null)
                    {
                        // Removemos o UpdateLayout() que bloqueava a UI.
                        // Encontra o ScrollViewer nativo
                        ScrollViewer sv = FindScrollViewer(GamesItemsControl);

                        // Se o WpfToolkit.VirtualizingWrapPanel estiver bem integrado, podes usar o BringIntoView do framework:
                        var container = GamesItemsControl.ItemContainerGenerator.ContainerFromItem(targetItem) as FrameworkElement;

                        if (container != null)
                        {
                            container.BringIntoView();
                        }
                        else
                        {
                            // Se o contentor ainda não foi gerado devido à virtualização,
                            // dizemos ao ItemsControl para trazer o item para a área visível de forma limpa
                            GamesItemsControl.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var targetContainer = GamesItemsControl.ItemContainerGenerator.ContainerFromItem(targetItem) as FrameworkElement;
                                targetContainer?.BringIntoView();
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                };
            }
        }

        private async void GameCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!DialogHost.IsDialogOpen("RootDialog") && sender is FrameworkElement fe && fe.DataContext is Game)
                await DialogHost.Show(fe.DataContext, dialogIdentifier: "RootDialog");
        }

        private void RemoveGameEntryButton_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender)?.DataContext is Game game && Application.Current.MainWindow.DataContext is MainViewModel vm)
            {
                if (vm.RemoveLibraryItemCommand.CanExecute(game))
                    vm.RemoveLibraryItemCommand.Execute(game);
            }
            DialogHost.Close("RootDialog");
        }

        private void EditSortNameSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject source)
            {
                var container = FindAncestor<Grid>(source);
                if (container != null)
                {
                    var textBox = FindVisualChildByName<TextBox>(container, "EditSortNameTextBox");
                    if (textBox != null)
                    {
                        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                        ((MainViewModel)Application.Current.MainWindow.DataContext).MessageQueue.Enqueue("Sort name saved successfully.");
                    }
                }
            }
        }

        public static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;

                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        public static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && typedChild.Name == name)
                    return typedChild;

                var result = FindVisualChildByName<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        // Helper method to find the ScrollViewer inside the ItemsControl
        public static ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            if (obj == null) return null;
            if (obj is ScrollViewer sv) return sv;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            //((Image)sender).Source = new BitmapImage(new Uri("https://images.igdb.com/igdb/image/upload/t_cover_big/nocover.png"));
            if (sender is Image img)
            {
                if (Application.Current.TryFindResource("NoCoverBitmap") is BitmapImage cachedImage)
                {
                    img.Source = cachedImage;
                }
            }
        }

        private void CloseDialogButton_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute("RootDialog", null);
        }
    }
}
