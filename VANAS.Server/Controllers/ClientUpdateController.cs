using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VANAS.Shared.Contracts;

namespace VANAS.Server.Controllers
{
    /// <summary>
    /// Serves the latest build of the PC client so the café's PCs can update themselves.
    /// The folder (setting Client:UpdateFolder, default "ClientUpdates") holds client.zip, version.txt and client.sha256,
    /// written by tools/Publish-Client.ps1. The package never contains client.json (each PC keeps its own settings).
    /// </summary>
    [ApiController]
    [Route("api/client")]
    public class ClientUpdateController : ControllerBase
    {
        private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+(\.\d+)?$", RegexOptions.Compiled);

        private readonly string _folder;

        public ClientUpdateController(IConfiguration config, IWebHostEnvironment env)
        {
            var configured = config["Client:UpdateFolder"] ?? "ClientUpdates";
            _folder = Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));
        }

        private string ZipPath => Path.Combine(_folder, "client.zip");

        /// <summary>The PCs are not signed in when they check for an update (it runs at boot), so this is public: it only describes a program.</summary>
        [AllowAnonymous]
        [HttpGet("version")]
        public async Task<IActionResult> Version()
        {
            var versionFile = Path.Combine(_folder, "version.txt");
            if (!System.IO.File.Exists(ZipPath) || !System.IO.File.Exists(versionFile)) return NotFound("Aucune mise à jour publiée.");

            var version = (await System.IO.File.ReadAllTextAsync(versionFile)).Trim();
            if (!VersionPattern.IsMatch(version)) return NotFound("Numéro de version invalide.");

            var size = new FileInfo(ZipPath).Length;
            return Ok(new ClientVersionInfo(version, await Sha256Async(), size));
        }

        [AllowAnonymous]
        [HttpGet("package")]
        public IActionResult Package() =>
            System.IO.File.Exists(ZipPath)
                ? PhysicalFile(ZipPath, "application/zip", "vanas-client.zip", enableRangeProcessing: true)
                : NotFound("Aucune mise à jour publiée.");

        /// <summary>The checksum written at publication; computed from the file when it is missing.</summary>
        private async Task<string> Sha256Async()
        {
            var hashFile = Path.Combine(_folder, "client.sha256");
            if (System.IO.File.Exists(hashFile))
            {
                var recorded = (await System.IO.File.ReadAllTextAsync(hashFile)).Trim().ToLowerInvariant();
                if (Regex.IsMatch(recorded, "^[0-9a-f]{64}$")) return recorded;
            }

            await using var stream = System.IO.File.OpenRead(ZipPath);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
        }
    }
}
