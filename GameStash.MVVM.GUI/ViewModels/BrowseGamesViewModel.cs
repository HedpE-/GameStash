using Common.Lib.Collections;
using Common.Lib.HelperClasses;
using IgdbSharp;
using IgdbSharp.Models;
using GameStash.Core.Services;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace GameStash.MVVM.GUI.ViewModels
{
    public class BrowseGamesViewModel : ObservableObject
    {
        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        private string _currentSearchQuery;
        public string CurrentSearchQuery
        {
            get => _currentSearchQuery;
            set => SetProperty(ref _currentSearchQuery, value);
        }

        private ObservableCollection<Game> _searchResults;
        public ObservableCollection<Game> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        private bool _searching;
        public bool Searching
        {
            get => _searching;
            set => SetProperty(ref _searching, value);
        }

        private bool _enableSearchView;
        public bool EnableSearchView
        {
            get => _enableSearchView;
            set => SetProperty(ref _enableSearchView, value);
        }

        private ICommand _executeSearchCommand;
        public ICommand ExecuteSearchCommand
        {
            get
            {
                return _executeSearchCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => !string.IsNullOrEmpty(SearchQuery),
                    ExecuteDelegate = async x => await ExecuteSearch(SearchQuery)
                };
            }
        }

        private ICommand _clearSearchResultsCommand;
        public ICommand ClearSearchResultsCommand
        {
            get
            {
                return _clearSearchResultsCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => EnableSearchView,
                    ExecuteDelegate = x =>
                    {
                        EnableSearchView = false;
                        SearchResults = null;
                        CurrentSearchQuery = null;
                    }
                };
            }
        }

        public event Action ScrollToTopRequested;

        private async Task ExecuteSearch(string query)
        {
            SearchResults = null;
            EnableSearchView = false;
            Searching = true;
            try
            {
                Core.Models.Settings settings;
                if (Application.Current.MainWindow.DataContext is MainViewModel vm)
                    settings = new Core.Models.Settings
                    {
                        IGDB_Client_ID = vm.Igdb_Client_Id,
                        IGDB_Secret = vm.Igdb_Secret
                    };
                else
                    settings = await SqliteHelper.GetSettingsAsync();

                var igdbClient = new IgdbClient(settings.IGDB_Client_ID, settings.IGDB_Secret);
                var results = await igdbClient.QueryAsync<Game>(Endpoints.Games, query: $"fields {Core.Models.Game.FullQueryFields}; search \"{query}\"; limit 500;");

                SearchResults = new ObservableCollection<Game>(results);
                CurrentSearchQuery = query;
            }
            catch(Exception e)
            {
                SearchResults = null;
                EnableSearchView = false;
                ((MainViewModel)Application.Current.MainWindow.DataContext).MessageQueue.Enqueue(e.Message);
            }
            finally
            {
                Searching = false;
                if(SearchResults != null)
                {
                    EnableSearchView = true;
                    ScrollToTopRequested?.Invoke();
                }
            }
        }
    }
}
