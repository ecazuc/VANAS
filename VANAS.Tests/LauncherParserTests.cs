using VANAS.Shared.Launchers;

namespace VANAS.Tests;

public class LauncherParserTests
{
    private const string LibraryFolders = """
        "libraryfolders"
        {
        	"0"
        	{
        		"path"		"C:\\Program Files (x86)\\Steam"
        		"label"		""
        		"apps"
        		{
        			"228980"		"157818239"
        		}
        	}
        	"1"
        	{
        		"path"		"Q:\\SteamLibrary"
        		"apps"
        		{
        			"730"		"945021611"
        		}
        	}
        }
        """;

    [Fact]
    public void Steam_library_folders_are_listed_with_unescaped_paths()
    {
        var libraries = InstalledGameParsers.ParseSteamLibraries(LibraryFolders);
        Assert.Equal(new[] { @"C:\Program Files (x86)\Steam", @"Q:\SteamLibrary" }, libraries.Select(l => l.Path));
    }

    [Fact]
    public void Fully_installed_steam_app_is_detected()
    {
        const string acf = "\"AppState\"\n{\n\t\"appid\"\t\t\"730\"\n\t\"name\"\t\t\"Counter-Strike 2\"\n\t\"StateFlags\"\t\t\"4\"\n\t\"installdir\"\t\t\"Counter-Strike Global Offensive\"\n}";
        var game = InstalledGameParsers.ParseSteamAppManifest(acf);
        Assert.Equal(new InstalledGame(GameLauncher.Steam, "730", "Counter-Strike 2"), game);
    }

    [Theory]
    [InlineData("1026")]  // update required + running install flags, bit 4 not set
    [InlineData("2")]     // update required only
    [InlineData("1")]
    public void Steam_app_that_is_not_fully_installed_is_ignored(string flags)
    {
        var acf = $"\"AppState\"\n{{\n\t\"appid\"\t\"570\"\n\t\"name\"\t\"Dota 2\"\n\t\"StateFlags\"\t\"{flags}\"\n}}";
        Assert.Null(InstalledGameParsers.ParseSteamAppManifest(acf));
    }

    [Fact]
    public void Steam_app_flags_with_extra_bits_still_count_as_installed()
    {
        // 4 (installed) + 1024 (update queued): still playable
        var acf = "\"AppState\"\n{\n\t\"appid\"\t\"570\"\n\t\"name\"\t\"Dota 2\"\n\t\"StateFlags\"\t\"1028\"\n}";
        Assert.NotNull(InstalledGameParsers.ParseSteamAppManifest(acf));
    }

    [Fact]
    public void Garbage_manifests_never_throw()
    {
        Assert.Null(InstalledGameParsers.ParseSteamAppManifest("not a manifest {{{ \""));
        Assert.Null(InstalledGameParsers.ParseSteamAppManifest(""));
        Assert.Empty(InstalledGameParsers.ParseSteamLibraries("\"x\" {"));
        Assert.Null(InstalledGameParsers.ParseEpicManifest("{ broken"));
    }

    [Fact]
    public void Epic_manifest_gives_app_name_and_display_name()
    {
        const string json = """{ "bIsIncompleteInstall": false, "AppName": "Fortnite", "DisplayName": "Fortnite", "InstallLocation": "Q:\\Epic Games\\Fortnite" }""";
        Assert.Equal(new InstalledGame(GameLauncher.Epic, "Fortnite", "Fortnite"), InstalledGameParsers.ParseEpicManifest(json));
    }

    [Fact]
    public void Epic_incomplete_install_is_ignored()
    {
        const string json = """{ "bIsIncompleteInstall": true, "AppName": "Sugar", "DisplayName": "Rocket League" }""";
        Assert.Null(InstalledGameParsers.ParseEpicManifest(json));
    }

    [Fact]
    public void Riot_install_folders_come_from_associated_client()
    {
        const string json = """
            {
              "associated_client": {
                "Q:/Riot Games/League of Legends/": "Q:/Riot Games/Riot Client/RiotClientServices.exe",
                "Q:/Riot Games/VALORANT/live/": "Q:/Riot Games/Riot Client/RiotClientServices.exe"
              },
              "rc_default": "Q:/Riot Games/Riot Client/RiotClientServices.exe"
            }
            """;
        var folders = InstalledGameParsers.ParseRiotInstallFolders(json);
        Assert.Equal(2, folders.Count);
        Assert.Contains(folders, f => f.Contains("VALORANT"));
    }

    [Fact]
    public void Launch_and_install_uris_match_each_launchers_protocol()
    {
        Assert.Equal("steam://rungameid/730", LauncherUris.Launch(GameLauncher.Steam, "730"));
        Assert.Equal("steam://install/730", LauncherUris.Install(GameLauncher.Steam, "730"));
        Assert.Equal("com.epicgames.launcher://apps/Sugar?action=launch&silent=true", LauncherUris.Launch(GameLauncher.Epic, "Sugar"));
        Assert.Equal("com.epicgames.launcher://apps/Sugar?action=install", LauncherUris.Install(GameLauncher.Epic, "Sugar"));
        Assert.Null(LauncherUris.Launch(GameLauncher.Riot, "valorant"));
        Assert.Equal("--launch-product=valorant --launch-patchline=live", LauncherUris.RiotArguments("valorant"));
    }
}

public class LauncherInstallTests
{
    [Fact]
    public void Steam_progress_is_computed_from_downloaded_bytes()
    {
        const string acf = "\"AppState\"\n{\n\t\"appid\"\t\"730\"\n\t\"StateFlags\"\t\"1026\"\n\t\"BytesToDownload\"\t\"2000\"\n\t\"BytesDownloaded\"\t\"500\"\n}";
        var percent = InstalledGameParsers.ParseSteamProgress(acf, out var appId);
        Assert.Equal("730", appId);
        Assert.Equal(25, percent!.Value, 3);
    }

    [Fact]
    public void Steam_progress_is_unknown_when_nothing_to_download()
    {
        const string acf = "\"AppState\"\n{\n\t\"appid\"\t\"730\"\n\t\"StateFlags\"\t\"4\"\n\t\"BytesToDownload\"\t\"0\"\n}";
        Assert.Null(InstalledGameParsers.ParseSteamProgress(acf, out _));
        Assert.Null(InstalledGameParsers.ParseSteamProgress("garbage {", out _));
    }

    [Fact]
    public void Login_cleanup_removes_remembered_logins_but_leaves_games_and_other_files_alone()
    {
        var root = Path.Combine(Path.GetTempPath(), "vanas-cleanup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var local = Path.Combine(root, "local");
            var steam = Path.Combine(root, "Steam");

            string Touch(params string[] parts)
            {
                var path = Path.Combine(parts);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "x");
                return path;
            }

            var epicIni = Touch(local, "EpicGamesLauncher", "Saved", "Config", "Windows", "GameUserSettings.ini");
            var riotYaml = Touch(local, "Riot Games", "Riot Client", "Data", "RiotGamesPrivateSettings.yaml");
            var riotSession = Touch(local, "Riot Games", "Riot Client", "Data", "Sessions", "s.json");
            var webcache = Touch(local, "EpicGamesLauncher", "Saved", "webcache_4147", "cookies");
            var steamLogin = Touch(steam, "config", "loginusers.vdf");
            var gameFile = Touch(steam, "steamapps", "common", "Game", "game.exe");
            var riotSettings = Touch(local, "Riot Games", "Riot Client", "Config", "RiotClientSettings.yaml");

            var removed = LauncherLoginCleanup.Wipe(
                LauncherLoginCleanup.FilesToDelete(steam, local),
                LauncherLoginCleanup.DirectoriesToDelete(local));

            Assert.Equal(5, removed);
            Assert.False(File.Exists(epicIni));
            Assert.False(File.Exists(riotYaml));
            Assert.False(File.Exists(steamLogin));
            Assert.False(Directory.Exists(Path.GetDirectoryName(riotSession)));
            Assert.False(Directory.Exists(Path.GetDirectoryName(webcache)));
            Assert.True(File.Exists(gameFile));
            Assert.True(File.Exists(riotSettings));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Login_cleanup_on_a_machine_without_launchers_does_nothing_and_does_not_throw()
    {
        var nowhere = Path.Combine(Path.GetTempPath(), "vanas-none-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(0, LauncherLoginCleanup.Wipe(
            LauncherLoginCleanup.FilesToDelete(null, nowhere),
            LauncherLoginCleanup.DirectoriesToDelete(nowhere)));
    }
}
