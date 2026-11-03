using System;
using System.IO;

namespace GameStash.Web.GUI.Helpers
{
    public class PathHelper
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
    }
}
