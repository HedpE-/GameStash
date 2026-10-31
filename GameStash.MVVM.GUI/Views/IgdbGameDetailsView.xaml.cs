using IgdbSharp.Models;
using MaterialDesignThemes.Wpf;
using GameStash.MVVM.GUI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameStash.MVVM.GUI.Views
{
    /// <summary>
    /// Interaction logic for IgdbGameDetailsView.xaml
    /// </summary>
    public partial class IgdbGameDetailsView : UserControl
    {
        // Dependency Property for IsScreenshotsViewerVisibleProperty
        public static readonly DependencyProperty IsScreenshotsViewerVisibleProperty =
            DependencyProperty.Register(
                nameof(IsScreenshotsViewerVisible), typeof(bool), typeof(IgdbGameDetailsView),
                new PropertyMetadata(false));

        public bool IsScreenshotsViewerVisible
        {
            get => (bool)GetValue(IsScreenshotsViewerVisibleProperty);
            set => SetValue(IsScreenshotsViewerVisibleProperty, value);
        }

        // Dependency Property for CurrentScreenshotNumberProperty
        public static readonly DependencyProperty CurrentScreenshotNumberProperty =
            DependencyProperty.Register(
                nameof(CurrentScreenshotNumber), typeof(int), typeof(IgdbGameDetailsView),
                new PropertyMetadata(0));

        public int CurrentScreenshotNumber
        {
            get => (int)GetValue(CurrentScreenshotNumberProperty);
            set => SetValue(CurrentScreenshotNumberProperty, value);
        }

        // Dependency Property for CurrentScreenshot
        public static readonly DependencyProperty CurrentScreenshotProperty =
            DependencyProperty.Register(
                nameof(CurrentScreenshot), typeof(object), typeof(IgdbGameDetailsView),
                new PropertyMetadata(null));

        public object CurrentScreenshot
        {
            get => (object)GetValue(CurrentScreenshotProperty);
            set => SetValue(CurrentScreenshotProperty, value);
        }

        // Dependency Property for IsUpdatingSimilarGames
        public static readonly DependencyProperty IsUpdatingSimilarGamesProperty =
            DependencyProperty.Register(
                nameof(IsUpdatingSimilarGames), typeof(bool), typeof(IgdbGameDetailsView),
                new PropertyMetadata(false));

        public bool IsUpdatingSimilarGames
        {
            get => (bool)GetValue(IsUpdatingSimilarGamesProperty);
            set => SetValue(IsUpdatingSimilarGamesProperty, value);
        }

        // Dependency Property for SimilarGames
        public static readonly DependencyProperty SimilarGamesProperty =
            DependencyProperty.Register(
                nameof(SimilarGames), typeof(IEnumerable<object>), typeof(IgdbGameDetailsView),
                new PropertyMetadata(null));

        public IEnumerable<object> SimilarGames
        {
            get => (IEnumerable<object>)GetValue(SimilarGamesProperty);
            set => SetValue(SimilarGamesProperty, value);
        }

        public IgdbGameDetailsView()
        {
            InitializeComponent();
            this.DataContextChanged += IgdbGameDetailsView_DataContextChanged;
        }

        public IgdbGameDetailsView(Game game) : this()
        {
            this.DataContext = game;
        }

        private async void IgdbGameDetailsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SimilarGames = null;
            if (DataContext is Game game)
            {
                if (game.SimilarGames != null && game.SimilarGames.Values.Any())
                {
                    IsUpdatingSimilarGames = true;

                    try
                    {
                        var mainViewModel = Application.Current.MainWindow.DataContext as MainViewModel;
                        var similarGames = new List<object>();
                        foreach (var similarGame in game.SimilarGames.Values)
                        {
                            if (mainViewModel.LocalGamesLibrary.Any(g => g.Id == similarGame.Id))
                                similarGames.Add(mainViewModel.LocalGamesLibrary.First(g => g.Id == similarGame.Id));
                            else
                                similarGames.Add(await Core.Models.Game.GetBaseGame(mainViewModel.Igdb_Client_Id, mainViewModel.Igdb_Secret, similarGame.Id.Value));
                        }

                        SimilarGames = similarGames;
                    }
                    finally
                    {
                        IsUpdatingSimilarGames = false;
                    }
                }
            }
        }

        private void ScreenshotCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                if (fe.DataContext is Screenshot screenshot)
                {
                    if (this.DataContext is Game game)
                    {
                        var screenshotPosition = Array.IndexOf(game.Screenshots.Values, screenshot) + 1;
                        CurrentScreenshotNumber = screenshotPosition;
                        CurrentScreenshot = screenshot;
                        IsScreenshotsViewerVisible = true;
                    }
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            IsScreenshotsViewerVisible = false;
        }

        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is Game game)
            {
                if (CurrentScreenshotNumber == 1)
                    CurrentScreenshotNumber = game.Screenshots.Values.Count();
                else
                    CurrentScreenshotNumber--;

                CurrentScreenshot = game.Screenshots.Values[CurrentScreenshotNumber - 1];
            }
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is Game game)
            {
                if (game.Screenshots.Values.Count() == CurrentScreenshotNumber)
                    CurrentScreenshotNumber = 1;
                else
                    CurrentScreenshotNumber++;

                CurrentScreenshot = game.Screenshots.Values[CurrentScreenshotNumber - 1];
            }
        }

        private void SimilarGame_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DataContext = ((FrameworkElement)sender).DataContext;
        }
    }
}
