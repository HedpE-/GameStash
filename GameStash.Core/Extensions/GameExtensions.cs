using IgdbSharp;
using IgdbSharp.Enumerators;
using IgdbSharp.Helpers;
using IgdbSharp.Models;
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
        public static string GetCoverUrl(this Game game, ImageSize size, bool retina = false)
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

        public static async Task GetCoverValue(this Game game, string client_id, string secret)
        {
            var igdbClient = new IgdbClient(client_id, secret);

            await game.GetCoverValue(igdbClient);
        }

        public static async Task GetCoverValue(this Game game, IgdbClient igdbClient)
        {
            var query = $"fields *; where id = {game.Cover.Id};";
            Cover result = (await igdbClient.QueryAsync<Cover>(Endpoints.Covers, query: query, requestPriority: RequestPriority.High)).FirstOrDefault();
            //game.Cover.Value = result;
        }
    }
}