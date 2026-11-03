using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameStash.Core.Models;
using IgdbSharp;

namespace GameStash.Web.GUI.Interfaces
{
    public interface ISettingsService
    {
        string IgdbClientId { get; }
        string IgdbSecret { get; }

        Task InitializeAsync();
        IgdbClient GetIgdbClient();
        Task UpdateIgdbCredentialsAsync(string clientId, string secret);
    }
}