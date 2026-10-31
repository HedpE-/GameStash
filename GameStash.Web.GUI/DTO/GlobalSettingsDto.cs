using GameStash.Core.Models;
using System;

namespace GameStash.Web.GUI.DTO
{
    public class GlobalSettingsDto
    {
        public string IgdbClientId { get; set; } = string.Empty;
        public string IgdbSecret { get; set; } = string.Empty;
        public RootFolder[] RootFolders { get; set; } = Array.Empty<RootFolder>();
    }
}
