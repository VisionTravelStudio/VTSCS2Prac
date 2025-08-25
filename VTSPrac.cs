using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace VTSPrac;

[MinimumApiVersion(80)]
public class VTSPracPlugin : BasePlugin
{
    public override string ModuleName => "VTSPrac Plugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "VisionTravelStudio";
    public override string ModuleDescription => "A comprehensive plugin with translation, database and command features";

    private SqliteConnection? _connection;
    private readonly Dictionary<ulong, DateTime> _playerConnectTimes = new();
    private readonly Dictionary<ulong, DateTime> _playerLastSaveTimes = new();
    private readonly Dictionary<ulong, string> _playerLanguages = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _saveTimer;

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("Loading {PluginName}", Localizer["plugin.name"]);
        
        // Initialize database
        InitializeDatabase();
        
        // Register dynamic commands
        RegisterCommands();

        // Start timer for saving playtime every 60 seconds
        _saveTimer = AddTimer(60.0f, SavePlaytimeForAllPlayers, TimerFlags.REPEAT);

        Logger.LogInformation(@"
__      _________ _____ _____                
\ \    / /__   __/ ____|  __ \               
 \ \  / /   | | | (___ | |__) | __ __ _  ___ 
  \ \/ /    | |  \___ \|  ___/ '__/ _` |/ __|
   \  /     | |  ____) | |   | | | (_| | (__ 
    \/      |_| |_____/|_|   |_|  \__,_|\___|
                HELLO WORLD");
    }

    private void InitializeDatabase()
    {
        try
        {
            // Use a more reliable database path in the game server directory
            var gameDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
            var pluginDataPath = Path.Join(gameDirectory, "csgo", "addons", "counterstrikesharp", "plugins", "VTSPrac");
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(pluginDataPath);
            
            var databasePath = Path.Join(pluginDataPath, "vtsprac.db");
            Logger.LogInformation("Loading database from {Path}", databasePath);
            Logger.LogInformation("Module Directory: {ModuleDirectory}", ModuleDirectory);
            Logger.LogInformation("Plugin Data Directory: {PluginDataPath}", pluginDataPath);
            Logger.LogInformation("Database file exists: {Exists}", File.Exists(databasePath));
            
            _connection = new SqliteConnection($"Data Source={databasePath}");
            _connection.Open();

            // Create tables if they don't exist
            Task.Run(async () =>
            {
                await _connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `players` (
                        `steamid` UNSIGNED BIG INT NOT NULL,
                        `name` TEXT NOT NULL,
                        `total_playtime_seconds` INT NOT NULL DEFAULT 0,
                        `language` TEXT DEFAULT 'en',
                        `last_seen` DATETIME DEFAULT CURRENT_TIMESTAMP,
                        PRIMARY KEY (`steamid`));");

                await _connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS `player_sessions` (
                        `id` INTEGER PRIMARY KEY AUTOINCREMENT,
                        `steamid` UNSIGNED BIG INT NOT NULL,
                        `connect_time` DATETIME DEFAULT CURRENT_TIMESTAMP,
                        `disconnect_time` DATETIME NULL,
                        `session_playtime_seconds` INT DEFAULT 0,
                        FOREIGN KEY (`steamid`) REFERENCES `players`(`steamid`));");
            });

            Logger.LogInformation(Localizer["database.connected"]);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, Localizer["database.error"]);
        }
    }

    private void RegisterCommands()
    {
        // Register ping command dynamically
        AddCommand("css_ping", "Responds with pong", (player, commandInfo) =>
        {
            if (player == null)
            {
                commandInfo.ReplyToCommand("pong server");
                return;
            }
            commandInfo.ReplyToCommand("pong");
        });

        // Register help command dynamically
        AddCommand("css_help", "Shows available commands", (player, commandInfo) =>
        {
            if (player == null) return;
            player.PrintToChat(Localizer["commands.help"]);
        });
    }

    public override void Unload(bool hotReload)
    {
        // Save all current playtime before unloading
        SavePlaytimeForAllPlayers();
        
        _saveTimer?.Kill();
        _connection?.Close();
        _connection?.Dispose();
        base.Unload(hotReload);
    }

    private string GetLocalizedText(CCSPlayerController player, string key, params object[] args)
    {
        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId.HasValue && _playerLanguages.ContainsKey(steamId.Value))
        {
            var playerLang = _playerLanguages[steamId.Value];
            // For now, we'll use a simple approach - we need to implement proper localization later
            if (playerLang == "zh")
            {
                return GetChineseTranslation(key, args);
            }
        }
        return Localizer[key, args];
    }

    private string GetChineseTranslation(string key, params object[] args)
    {
        var translations = new Dictionary<string, string>
        {
            ["stats.title"] = "玩家统计信息",
            ["stats.currentsession.minutes"] = "本次游戏时间：{0} 分钟",
            ["stats.currentsession.hours"] = "本次游戏时间：{0}小时 {1}分钟",
            ["stats.totalplaytime.minutes"] = "总游戏时间：{0} 分钟",
            ["stats.totalplaytime.hours"] = "总游戏时间：{0}小时 {1}分钟",
            ["stats.totalsessions"] = "总游戏次数：{0}"
        };

        if (translations.TryGetValue(key, out var translation))
        {
            return string.Format(translation, args);
        }
        return key; // Fallback
    }

    private void SavePlaytimeForAllPlayers()
    {
        if (_connection == null) return;

        var currentTime = DateTime.UtcNow;
        var playersToUpdate = new List<(ulong SteamId, int PlaytimeToAdd)>();

        foreach (var kvp in _playerLastSaveTimes.ToList())
        {
            var steamId = kvp.Key;
            var lastSaveTime = kvp.Value;
            var timeSinceLastSave = (int)(currentTime - lastSaveTime).TotalSeconds;
            
            // Only update if at least 55 seconds have passed (防止重复)
            if (timeSinceLastSave >= 55)
            {
                playersToUpdate.Add((steamId, timeSinceLastSave));
            }
        }

        if (playersToUpdate.Count > 0)
        {
            Task.Run(async () =>
            {
                foreach (var (steamId, playtimeToAdd) in playersToUpdate)
                {
                    try
                    {
                        // Update total playtime in players table (恢复累积功能)
                        await _connection.ExecuteAsync(@"
                            UPDATE `players` 
                            SET `total_playtime_seconds` = `total_playtime_seconds` + @PlaytimeToAdd,
                                `last_seen` = CURRENT_TIMESTAMP
                            WHERE `steamid` = @SteamId;",
                            new { SteamId = steamId, PlaytimeToAdd = playtimeToAdd });

                        // Update current session playtime in database (累计值)
                        await _connection.ExecuteAsync(@"
                            UPDATE `player_sessions` 
                            SET `session_playtime_seconds` = `session_playtime_seconds` + @PlaytimeToAdd
                            WHERE `steamid` = @SteamId AND `disconnect_time` IS NULL;",
                            new { SteamId = steamId, PlaytimeToAdd = playtimeToAdd });

                        Server.NextFrame(() => Logger.LogInformation("Updated playtime for player {SteamId}: +{PlaytimeToAdd} seconds", steamId, playtimeToAdd));
                    }
                    catch (Exception ex)
                    {
                        Server.NextFrame(() => Logger.LogError(ex, "Failed to update playtime for player {SteamId}", steamId));
                    }
                }
                
                Server.NextFrame(() => Logger.LogInformation("Completed playtime update for {Count} players", playersToUpdate.Count));
            });

            // 更新最后保存时间
            foreach (var steamId in playersToUpdate.Select(p => p.SteamId))
            {
                _playerLastSaveTimes[steamId] = currentTime;
            }
        }
    }

    #region Event Handlers

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        var playerName = player.PlayerName;

        if (steamId == null) return HookResult.Continue;

        // Record connect time for playtime tracking
        var connectTime = DateTime.UtcNow;
        _playerConnectTimes[steamId.Value] = connectTime;
        _playerLastSaveTimes[steamId.Value] = connectTime;
        
        Logger.LogInformation("Player {Name} ({SteamId}) connected at {Time}", 
            playerName, steamId, connectTime.ToString("yyyy-MM-dd HH:mm:ss"));

        // Welcome message with translation
        Server.NextFrame(() =>
        {
            player.PrintToChat(Localizer["colors.welcome"]);
            player.PrintToChat(Localizer["player.welcome", playerName]);
        });

        // Update database in background
        if (_connection != null)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Insert or update player info
                    await _connection.ExecuteAsync(@"
                        INSERT INTO `players` (`steamid`, `name`, `last_seen`) 
                        VALUES (@SteamId, @Name, CURRENT_TIMESTAMP)
                        ON CONFLICT(`steamid`) DO UPDATE SET 
                            `name` = @Name, 
                            `last_seen` = CURRENT_TIMESTAMP;",
                        new { SteamId = steamId, Name = playerName });

                    Server.NextFrame(() => Logger.LogInformation("Player {SteamId} inserted/updated in database", steamId));

                    // Load player's language preference
                    var playerData = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
                        SELECT `language`, `total_playtime_seconds` FROM `players` WHERE `steamid` = @SteamId;",
                        new { SteamId = steamId });
                    
                    if (playerData?.language != null)
                    {
                        var language = (string)playerData.language;
                        var totalPlaytime = (int)(playerData.total_playtime_seconds ?? 0);
                        _playerLanguages[steamId.Value] = language;
                        Server.NextFrame(() => Logger.LogInformation("Loaded language preference for player {SteamId}: {Language}, Total playtime: {Playtime}s", 
                            steamId, language, totalPlaytime));
                    }
                    else
                    {
                        // Set default language
                        _playerLanguages[steamId.Value] = "en";
                        Server.NextFrame(() => Logger.LogInformation("Set default language for new player {SteamId}: en", steamId));
                    }

                    // Create new session record
                    await _connection.ExecuteAsync(@"
                        INSERT INTO `player_sessions` (`steamid`, `connect_time`) 
                        VALUES (@SteamId, CURRENT_TIMESTAMP);",
                        new { SteamId = steamId });

                    Server.NextFrame(() => Logger.LogInformation("New session created for player {SteamId}", steamId));
                }
                catch (Exception ex)
                {
                    Server.NextFrame(() => Logger.LogError(ex, "Failed to update database for player {SteamId} ({Name})", steamId, playerName));
                }
            });
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return HookResult.Continue;

        // Calculate and save final playtime for this session
        if (_playerConnectTimes.TryGetValue(steamId.Value, out var connectTime))
        {
            var sessionTime = (int)(DateTime.UtcNow - connectTime).TotalSeconds;
            _playerConnectTimes.Remove(steamId.Value);
            _playerLastSaveTimes.Remove(steamId.Value);
            _playerLanguages.Remove(steamId.Value);

            if (_connection != null && sessionTime > 0)
            {
                Task.Run(async () =>
                {
                    // Only update session end time and playtime (不再累积到总游戏时间)
                    await _connection.ExecuteAsync(@"
                        UPDATE `player_sessions` 
                        SET `disconnect_time` = CURRENT_TIMESTAMP,
                            `session_playtime_seconds` = @SessionTime
                        WHERE `steamid` = @SteamId AND `disconnect_time` IS NULL;",
                        new { SteamId = steamId, SessionTime = sessionTime });
                        
                    // Update last seen time only
                    await _connection.ExecuteAsync(@"
                        UPDATE `players` 
                        SET `last_seen` = CURRENT_TIMESTAMP
                        WHERE `steamid` = @SteamId;",
                        new { SteamId = steamId });
                        
                    Server.NextFrame(() => Logger.LogInformation("Player {SteamId} disconnected, session time: {Time} seconds (not added to total)", 
                        steamId, sessionTime));
                });
            }
        }

        return HookResult.Continue;
    }

    #endregion

    #region Commands

    [ConsoleCommand("css_hello", "Greets the player")]
    [CommandHelper(minArgs: 0, usage: "[name]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnHelloCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return;

        var name = commandInfo.ArgCount > 1 ? commandInfo.GetArg(1) : player.PlayerName;
        player.PrintToChat(Localizer["commands.hello", name]);
    }

    [ConsoleCommand("css_playtime", "Shows player's current session and total playtime")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnPlaytimeCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || _connection == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        // Calculate current session time
        var sessionTime = 0;
        if (_playerConnectTimes.TryGetValue(steamId.Value, out var connectTime))
        {
            sessionTime = (int)(DateTime.UtcNow - connectTime).TotalSeconds;
        }

        Task.Run(async () =>
        {
            var result = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT `total_playtime_seconds` FROM `players` WHERE `steamid` = @SteamId;",
                new { SteamId = steamId });

            Server.NextFrame(() =>
            {
                var totalSeconds = result?.total_playtime_seconds ?? 0;
                var currentSessionMinutes = sessionTime / 60;
                var currentSessionHours = currentSessionMinutes / 60;
                var totalMinutes = totalSeconds / 60;
                var totalHours = totalMinutes / 60;

                player.PrintToChat(" \x04=== Playtime Stats ===\x01");
                
                if (currentSessionHours > 0)
                    player.PrintToChat(Localizer["playtime.current.hours", currentSessionHours, currentSessionMinutes % 60]);
                else
                    player.PrintToChat(Localizer["playtime.current.minutes", currentSessionMinutes]);
                
                if (totalHours > 0)
                    player.PrintToChat(Localizer["playtime.total.hours", totalHours, totalMinutes % 60]);
                else
                    player.PrintToChat(Localizer["playtime.total.minutes", totalMinutes]);
            });
        });
    }

    [ConsoleCommand("css_stats", "Shows detailed player statistics")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnStatsCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || _connection == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        // Calculate current session time
        var sessionTime = 0;
        if (_playerConnectTimes.TryGetValue(steamId.Value, out var connectTime))
        {
            sessionTime = (int)(DateTime.UtcNow - connectTime).TotalSeconds;
        }

        Task.Run(async () =>
        {
            var playerResult = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT `total_playtime_seconds`, `last_seen` FROM `players` WHERE `steamid` = @SteamId;",
                new { SteamId = steamId });

            var sessionCount = await _connection.QueryFirstOrDefaultAsync<int>(@"
                SELECT COUNT(*) FROM `player_sessions` WHERE `steamid` = @SteamId;",
                new { SteamId = steamId });

            Server.NextFrame(() =>
            {
                if (playerResult != null)
                {
                    var totalSeconds = playerResult.total_playtime_seconds ?? 0;
                    var totalMinutes = totalSeconds / 60;
                    var totalHours = totalMinutes / 60;
                    var currentSessionMinutes = sessionTime / 60;
                    var currentSessionHours = currentSessionMinutes / 60;
                    
                    player.PrintToChat(" \x04=== " + GetLocalizedText(player, "stats.title") + " ===\x01");
                    
                    // Current session display
                    if (currentSessionHours > 0)
                        player.PrintToChat(" \x09" + GetLocalizedText(player, "stats.currentsession.hours", currentSessionHours, currentSessionMinutes % 60) + "\x01");
                    else
                        player.PrintToChat(" \x09" + GetLocalizedText(player, "stats.currentsession.minutes", currentSessionMinutes) + "\x01");
                    
                    // Total playtime display (恢复自动累积)
                    if (totalHours > 0)
                        player.PrintToChat(" \x04" + GetLocalizedText(player, "stats.totalplaytime.hours", totalHours, totalMinutes % 60) + "\x01");
                    else
                        player.PrintToChat(" \x04" + GetLocalizedText(player, "stats.totalplaytime.minutes", totalMinutes) + "\x01");
                        
                    player.PrintToChat(" \x06" + GetLocalizedText(player, "stats.totalsessions", sessionCount) + "\x01");
                }
                else
                {
                    player.PrintToChat(GetLocalizedText(player, "player.nodata"));
                }
            });
        });
    }

    [ConsoleCommand("css_debug_time", "Debug current session time calculation")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnDebugTimeCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        var currentTime = DateTime.UtcNow;
        
        if (_playerConnectTimes.TryGetValue(steamId.Value, out var connectTime))
        {
            var sessionTimeSeconds = (int)(currentTime - connectTime).TotalSeconds;
            var sessionTimeMinutes = sessionTimeSeconds / 60;
            
            player.PrintToChat($"[DEBUG] Connect Time: {connectTime:yyyy-MM-dd HH:mm:ss}");
            player.PrintToChat($"[DEBUG] Current Time: {currentTime:yyyy-MM-dd HH:mm:ss}");
            player.PrintToChat($"[DEBUG] Session Seconds: {sessionTimeSeconds}");
            player.PrintToChat($"[DEBUG] Session Minutes: {sessionTimeMinutes}");
        }
        else
        {
            player.PrintToChat("[DEBUG] No connect time recorded!");
        }
        
        if (_playerLastSaveTimes.TryGetValue(steamId.Value, out var lastSaveTime))
        {
            var timeSinceLastSave = (int)(currentTime - lastSaveTime).TotalSeconds;
            player.PrintToChat($"[DEBUG] Last Save Time: {lastSaveTime:yyyy-MM-dd HH:mm:ss}");
            player.PrintToChat($"[DEBUG] Time Since Last Save: {timeSinceLastSave} seconds");
        }
        
        player.PrintToChat($"[DEBUG] Player in connect times dict: {_playerConnectTimes.ContainsKey(steamId.Value)}");
        player.PrintToChat($"[DEBUG] Player in languages dict: {_playerLanguages.ContainsKey(steamId.Value)}");
        
        // Check database content
        if (_connection != null)
        {
            Task.Run(async () =>
            {
                var dbData = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT `total_playtime_seconds`, `language` FROM `players` WHERE `steamid` = @SteamId;",
                    new { SteamId = steamId });
                    
                Server.NextFrame(() =>
                {
                    if (dbData != null)
                    {
                        player.PrintToChat($"[DEBUG] DB Total Playtime: {dbData.total_playtime_seconds} seconds");
                        player.PrintToChat($"[DEBUG] DB Language: {dbData.language}");
                        
                        var currentLang = _playerLanguages.GetValueOrDefault(steamId.Value, "none");
                        player.PrintToChat($"[DEBUG] Memory Language: {currentLang}");
                    }
                    else
                    {
                        player.PrintToChat("[DEBUG] No player data found in database!");
                    }
                });
            });
        }
    }

    [ConsoleCommand("css_debug_lang", "Debug language system")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnDebugLangCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        var currentLang = _playerLanguages.GetValueOrDefault(steamId.Value, "not_set");
        player.PrintToChat($"[LANG DEBUG] Current language in memory: {currentLang}");
        
        // Test translation
        var testKey = "stats.title";
        var englishText = Localizer[testKey];
        var localizedText = GetLocalizedText(player, testKey);
        
        player.PrintToChat($"[LANG DEBUG] English text: {englishText}");
        player.PrintToChat($"[LANG DEBUG] Localized text: {localizedText}");
    }

    [ConsoleCommand("css_debug_db", "Debug database content")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnDebugDbCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || _connection == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        Task.Run(async () =>
        {
            try
            {
                // Check if player exists in database
                var playerExists = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT COUNT(*) as count FROM `players` WHERE `steamid` = @SteamId;",
                    new { SteamId = steamId });

                var playerData = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT * FROM `players` WHERE `steamid` = @SteamId;",
                    new { SteamId = steamId });

                var sessionCount = await _connection.QueryFirstOrDefaultAsync<int>(@"
                    SELECT COUNT(*) FROM `player_sessions` WHERE `steamid` = @SteamId;",
                    new { SteamId = steamId });

                var totalPlayersInDb = await _connection.QueryFirstOrDefaultAsync<int>(@"
                    SELECT COUNT(*) FROM `players`;");

                Server.NextFrame(() =>
                {
                    player.PrintToChat($"[DB DEBUG] Database connection: {(_connection?.State == System.Data.ConnectionState.Open ? "Open" : "Closed")}");
                    player.PrintToChat($"[DB DEBUG] Total players in DB: {totalPlayersInDb}");
                    player.PrintToChat($"[DB DEBUG] Player exists in DB: {(playerExists?.count > 0 ? "Yes" : "No")}");
                    
                    if (playerData != null)
                    {
                        player.PrintToChat($"[DB DEBUG] Player name: {playerData.name}");
                        player.PrintToChat($"[DB DEBUG] Total playtime: {playerData.total_playtime_seconds} seconds");
                        player.PrintToChat($"[DB DEBUG] Language: {playerData.language}");
                        player.PrintToChat($"[DB DEBUG] Last seen: {playerData.last_seen}");
                        player.PrintToChat($"[DB DEBUG] Total sessions: {sessionCount}");
                    }
                    else
                    {
                        player.PrintToChat("[DB DEBUG] No player data found!");
                    }
                });
            }
            catch (Exception ex)
            {
                Server.NextFrame(() =>
                {
                    player.PrintToChat($"[DB DEBUG] Error: {ex.Message}");
                    Logger.LogError(ex, "Database debug error for player {SteamId}", steamId);
                });
            }
        });
    }
    [ConsoleCommand("css_lang", "Change player's language preference")]
    [CommandHelper(minArgs: 1, usage: "[language_code]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnLanguageCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        var langCode = commandInfo.GetArg(1);
        
        // Validate language code
        if (langCode != "en" && langCode != "zh")
        {
            player.PrintToChat("❌ Invalid language code. Available: en, zh");
            return;
        }
        
        // Store player's language preference
        _playerLanguages[steamId.Value] = langCode;
        
        // Save to database
        if (_connection != null)
        {
            Task.Run(async () =>
            {
                await _connection.ExecuteAsync(@"
                    UPDATE `players` SET `language` = @Language 
                    WHERE `steamid` = @SteamId;",
                    new { Language = langCode, SteamId = steamId });
            });
        }
        
        // Respond in the new language
        if (langCode == "zh")
        {
            player.PrintToChat("✅ 语言已设置为中文");
            player.PrintToChat("可用语言: en (English), zh (中文)");
        }
        else
        {
            player.PrintToChat("✅ Language set to English");
            player.PrintToChat("Available languages: en (English), zh (中文)");
        }
    }

    [RequiresPermissions("@css/admin")]
    [ConsoleCommand("css_vtsprac_reload", "Reload VTSPrac plugin configuration")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnReloadCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        Logger.LogInformation("Reloading VTSPrac plugin configuration...");
        commandInfo.ReplyToCommand("VTSPrac plugin configuration reloaded successfully!");
    }

    [RequiresPermissions("@css/admin")]
    [ConsoleCommand("css_vtsprac_info", "Show VTSPrac plugin information")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnInfoCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        commandInfo.ReplyToCommand($"VTSPrac Plugin v{ModuleVersion}");
        commandInfo.ReplyToCommand($"Author: {ModuleAuthor}");
        commandInfo.ReplyToCommand($"Description: {ModuleDescription}");
        commandInfo.ReplyToCommand($"Database: {(_connection?.State == System.Data.ConnectionState.Open ? "Connected" : "Disconnected")}");
    }

    #endregion
}
