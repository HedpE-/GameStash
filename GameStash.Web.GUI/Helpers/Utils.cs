using System;
using System.IO;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Helpers
{
    public class Utils
    {
        // Helper: Normalizes directory path endings to prevent slash mismatches
        public static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        // Helper: Returns true if candidatePath is located inside parentPath
        public static bool IsSubdirectoryOf(string candidatePath, string parentPath)
        {
            if (string.Equals(candidatePath, parentPath, StringComparison.OrdinalIgnoreCase))
                return false;

            string normalizedParent = parentPath.EndsWith(Path.DirectorySeparatorChar)
                ? parentPath
                : parentPath + Path.DirectorySeparatorChar;

            return candidatePath.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }
    }
}
