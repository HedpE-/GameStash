/*
 * Created by SharpDevelop.
 * User: GONCARJ3
 * Date: 06/01/2017
 * Time: 20:43
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System.IO;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Extensions

{
    public static class DirectoryInfoExtensions
    {
        public static void CopyTo(this DirectoryInfo source, string targetString)
        {
            DirectoryInfo target = new DirectoryInfo(targetString);
            if (!target.Exists)
                target.Create();

            foreach (var file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name), true);

            foreach (var subdir in source.GetDirectories())
                subdir.CopyTo(target.CreateSubdirectory(subdir.Name).FullName);
        }

        public static long GetDirectorySize(this DirectoryInfo directory, bool includeSubFolders = true)
        {
            if (directory.Exists)
            {
                long size = 0;

                // Add file sizes.
                FileInfo[] fis = directory.GetFiles();
                Parallel.ForEach(fis, fi => size += fi.Length);

                if (includeSubFolders)
                {
                    // Add subdirectory sizes.
                    DirectoryInfo[] dis = directory.GetDirectories();
                    Parallel.ForEach(dis, di => size += di.GetDirectorySize());
                }

                return size;
            }

            return 0;
        }
    }
}