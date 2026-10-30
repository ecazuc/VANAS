using System.IO;
using VANAS.Shared.Launchers;

namespace VANAS.Client.Services
{
    public static class DiskSpace
    {
        /// <summary>
        /// Free space (GB) on the drive a launcher installs to by default. Steam and Riot follow their own install
        /// drive; Epic follows ProgramData. Returns null if it cannot be determined (the install is then allowed).
        /// </summary>
        public static double? FreeGbFor(GameLauncher launcher, DetectionResult detection)
        {
            var reference = launcher switch
            {
                GameLauncher.Steam => detection.SteamPath,
                GameLauncher.Riot => detection.RiotClientPath,
                _ => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            };

            try
            {
                var root = reference == null ? null : Path.GetPathRoot(reference);
                return root == null ? null : new DriveInfo(root).AvailableFreeSpace / 1024d / 1024d / 1024d;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
