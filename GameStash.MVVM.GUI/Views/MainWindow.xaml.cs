using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace GameStash.MVVM.GUI.Views
{
    public partial class MainWindow : Window
    {
        internal List<object> views;

        public MainWindow()
        {
            InitializeComponent();
            views = new List<object>();
            LocalLibraryView localLibraryView = new LocalLibraryView();
            views.Add(localLibraryView);
            var view = new HomeView();
            views.Add(view);
            MainContent.Content = view;
        }

        private void MenuTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue as TreeViewItem;
            if (selectedItem != null && selectedItem.Tag != null)
            {
                string tag = selectedItem.Tag.ToString();

                object view = null;
                switch (tag)
                {
                    case "Home":
                        if (!views.Any(v => v.GetType() == typeof(HomeView)))
                        {
                            view = new HomeView();
                            views.Add(view);
                        }
                        else
                            view = views.First(v => v.GetType() == typeof(HomeView));
                        break;
                    case "Browse":
                        if (!views.Any(v => v.GetType() == typeof(BrowseGamesView)))
                        {
                            view = new BrowseGamesView();
                            views.Add(view);
                        }
                        else
                            view = views.First(v => v.GetType() == typeof(BrowseGamesView));
                        break;
                    case "LocalLibrary":
                        if (!views.Any(v => v.GetType() == typeof(LocalLibraryView)))
                        {
                            view = new LocalLibraryView();
                            views.Add(view);
                        }
                        else
                            view = views.First(v => v.GetType() == typeof(LocalLibraryView));
                        break;
                    case "GlobalSettings":
                        view = new SettingsView();
                        break;
                    case "ImportFromRootFolders":
                        view = new ImportGamesView();
                        break;
                    default:
                        view = new FeatureNotImplementedView();
                        break;
                }
                MainContent.Content = view;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void TopHeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                MaximizeButton_Click(null, null);
        }

        private void MenuDarkModeButton_Click(object sender, RoutedEventArgs e)
            => ModifyTheme(((ToggleButton)sender).IsChecked == true);

        private static void ModifyTheme(bool isDarkTheme)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();

            theme.SetBaseTheme(isDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            this.Activate();
            this.Topmost = false;
        }
    }
}
