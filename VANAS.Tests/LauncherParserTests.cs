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
