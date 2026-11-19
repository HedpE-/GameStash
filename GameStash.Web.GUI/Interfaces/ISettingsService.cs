using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameStash.Core.Models;
using GameStash.Core.Services;
using IgdbSharp;

namespace GameStash.Web.GUI.Interfaces
{
    public interface ISettingsService
    {
        //string IgdbClientId { get; }
        //string IgdbSecret { get; }
        //string SteamId64 { get; }
        //string SteamApiKey { get; }
        //string GogRefreshToken { get; }
        //string GogAccessToken { get; }
        string GogUsername { get; }

        Task InitializeAsync();
        Task<IgdbClient> GetIgdbClient();
        SteamClient GetSteamClient();
        GogClient GetGogClient();
        EpicClient GetEpicClient();

        Task UpdateCredentialsAsync(string apiName, string clientId, string secret);
        Settings GetCurrentSettings();
    }
}