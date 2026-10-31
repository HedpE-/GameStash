using IgdbSharp.Models;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameStash.MVVM.GUI.Views
{
    /// <summary>
    /// Interaction logic for GameCardView.xaml
    /// </summary>
    public partial class GameCardView : UserControl
    {
        // Dependency Property for TitleDateFormatProperty
        public static readonly DependencyProperty TitleDateFormatProperty =
            DependencyProperty.Register(
                nameof(TitleDateFormat), typeof(string), typeof(IgdbGameDetailsView),
                new PropertyMetadata("yyyy"));

        public string TitleDateFormat
        {
            get => (string)GetValue(TitleDateFormatProperty);
            set => SetValue(TitleDateFormatProperty, value);
        }

        public GameCardView()
        {
            InitializeComponent();
        }

        private async void GameCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if(!DialogHost.IsDialogOpen("RootDialog") && sender is FrameworkElement fe && fe.DataContext is Game)
                await DialogHost.Show(fe.DataContext, dialogIdentifier: "RootDialog");
        }
    }
}
