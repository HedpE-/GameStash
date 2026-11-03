using GameStash.Core.Models;
using IgdbSharp;
using IgdbSharp.Enumerators;
using IgdbSharp.Helpers;
using IgdbSharp.Models;
using System.Globalization;
using System.Net.Sockets;

namespace GameStash.Core.Extensions
{
    public static class GameExtensions
    {
        /// <summary>
        /// Resolves the full artwork URL string for any IGDB game contract, aligning fallback placeholders dynamically.
        /// </summary>
        /// <param name="game">The targeted game instance (supports both base contract and local library extensions).</param>
        /// <param name="size">The targeted ImageSize token configuration profiles.</param>
        /// <returns>A fully qualified image location string path.</returns>
        public static string GetCoverUrl<T>(this T game, ImageSize size, bool retina = false) where T : IgdbSharp.Models.Game
        {
            // 1. If the game contract contains a valid image payload, map via the native helper utility
            if (game?.Cover != null && game.Cover.Value?.ImageId != null)
            {
                return ImageHelper.GetImageUrl(game.Cover.Value.ImageId, size);
            }

            // 2. Fallback logic: Extract the strict string format token layout straight from the ImageSizeMap dictionary
            if (ImageHelper.ImageSizeMap.ContainsKey(size))
            {
                return ImageHelper.IGDB_IMAGE_TEMPLATE
                  .Replace("{hash}", "nocover")
                  .Replace("{size}", ImageHelper.ImageSizeMap[size] + (retina ? "_2x" : ""));
            }

            // Global generic safety anchor fallback
            return "//images.igdb.com/igdb/image/upload/t_cover_big/nocover.jpg";
        }

        public static async Task GetCoverValue<T>(this T game, IgdbClient igdbClient) where T : IgdbSharp.Models.Game
        {
            var query = $"fields *; where id = {game.Cover.Id};";
            IgdbSharp.Models.Cover result = (await igdbClient.QueryAsync<IgdbSharp.Models.Cover>(Endpoints.Covers, query: query, requestPriority: RequestPriority.High)).FirstOrDefault();
            //game.Cover.Value = result;
        }

        public static string GetReleaseDate<T>(this T game, string format = "MMM dd, yyyy", CultureInfo culture = null) where T : IgdbSharp.Models.Game
        {
            if (game == null) return null;
            if (culture == null)
                culture = CultureInfo.InvariantCulture;
            return game.FirstReleaseDate?.ToString(format, culture);
        }

        public static Task<Game> GetBaseGame(this StashGame game, string client_id, string secret, string fields = null, CancellationToken cancellationToken = default, RequestPriority requestPriority = RequestPriority.Medium)
        {
            var igdbClient = new IgdbClient(client_id, secret);
            return game.GetBaseGame(igdbClient, fields, cancellationToken, requestPriority);
        }

        public static async Task<Game> GetBaseGame(this StashGame game, IgdbClient igdbClient, string fields = null, CancellationToken cancellationToken = default, RequestPriority requestPriority = RequestPriority.Medium)
        {
            fields = string.IsNullOrEmpty(fields) ? StashGame.FullQueryFields : fields;

            var igdbMatches = await igdbClient.QueryAsync<Game>(Endpoints.Games, query: $"fields {fields}; where id = {game.Id};", cancellationToken: cancellationToken, requestPriority: requestPriority);
            return igdbMatches?.FirstOrDefault();
        }
    }
}