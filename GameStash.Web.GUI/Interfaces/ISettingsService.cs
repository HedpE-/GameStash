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
        Task InitializeAsync();
        Task<IgdbClient> GetIgdbClient();
        SteamClient GetSteamClient();
        GogClient GetGogClient();
        EpicClient GetEpicClient();
        AmazonClient GetAmazonClient();
        string GenerateAmazonLoginUrl();

        Task UpdateCredentialsAsync(string apiName, string clientId, string secret);
        Task<Settings> GetCurrentSettings();
    }
}