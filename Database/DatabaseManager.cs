using System;
using System.IO;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace VTSPrac.Database;

public class DatabaseManager
{
    private SqliteConnection? _connection;
    private readonly ILogger _logger;

    public DatabaseManager(ILogger logger)
    {
        _logger = logger;
    }

    public SqliteConnection? Connection => _connection;

    public void Initialize()
    {
        try
        {
            // Use a more reliable database path in the game server directory
            var gameDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
            var pluginDataPath = Path.Join(gameDirectory, "csgo", "addons", "counterstrikesharp", "plugins", "VTSPrac");
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(pluginDataPath);
            
            var databasePath = Path.Join(pluginDataPath, "vtsprac.db");
            _logger.LogInformation("Loading database from {Path}", databasePath);
            _logger.LogInformation("Database file exists: {Exists}", File.Exists(databasePath));
            
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

            _logger.LogInformation("Database connected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
        }
    }

    public void Close()
    {
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
    }

    public async Task UpsertPlayerAsync(ulong steamId, string playerName)
    {
        if (_connection == null) return;

        await _connection.ExecuteAsync(@"
            INSERT INTO `players` (`steamid`, `name`, `last_seen`) 
            VALUES (@SteamId, @Name, CURRENT_TIMESTAMP)
            ON CONFLICT(`steamid`) DO UPDATE SET 
                `name` = @Name, 
                `last_seen` = CURRENT_TIMESTAMP;",
            new { SteamId = steamId, Name = playerName });
    }

    public async Task<PlayerData?> GetPlayerDataAsync(ulong steamId)
    {
        if (_connection == null) return null;

        var result = await _connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT `language`, `total_playtime_seconds` FROM `players` WHERE `steamid` = @SteamId;",
            new { SteamId = steamId });

        if (result == null) return null;

        return new PlayerData
        {
            Language = (string)result.language,
            TotalPlaytimeSeconds = (int)(result.total_playtime_seconds ?? 0)
        };
    }

    public async Task CreateSessionAsync(ulong steamId)
    {
        if (_connection == null) return;

        await _connection.ExecuteAsync(@"
            INSERT INTO `player_sessions` (`steamid`, `connect_time`) 
            VALUES (@SteamId, CURRENT_TIMESTAMP);",
            new { SteamId = steamId });
    }

    public async Task UpdatePlayerPlaytimeAsync(ulong steamId, int playtimeToAdd)
    {
        if (_connection == null) return;

        // Update total playtime in players table
        await _connection.ExecuteAsync(@"
            UPDATE `players` 
            SET `total_playtime_seconds` = `total_playtime_seconds` + @PlaytimeToAdd,
                `last_seen` = CURRENT_TIMESTAMP
            WHERE `steamid` = @SteamId;",
            new { SteamId = steamId, PlaytimeToAdd = playtimeToAdd });

        // Update current session playtime
        await _connection.ExecuteAsync(@"
            UPDATE `player_sessions` 
            SET `session_playtime_seconds` = `session_playtime_seconds` + @PlaytimeToAdd
            WHERE `steamid` = @SteamId AND `disconnect_time` IS NULL;",
            new { SteamId = steamId, PlaytimeToAdd = playtimeToAdd });
    }

    public async Task CloseSessionAsync(ulong steamId, int sessionTime)
    {
        if (_connection == null) return;

        // Update session end time and playtime
        await _connection.ExecuteAsync(@"
            UPDATE `player_sessions` 
            SET `disconnect_time` = CURRENT_TIMESTAMP,
                `session_playtime_seconds` = @SessionTime
            WHERE `steamid` = @SteamId AND `disconnect_time` IS NULL;",
            new { SteamId = steamId, SessionTime = sessionTime });
            
        // Update last seen time
        await _connection.ExecuteAsync(@"
            UPDATE `players` 
            SET `last_seen` = CURRENT_TIMESTAMP
            WHERE `steamid` = @SteamId;",
            new { SteamId = steamId });
    }

    public async Task UpdatePlayerLanguageAsync(ulong steamId, string language)
    {
        if (_connection == null) return;

        await _connection.ExecuteAsync(@"
            UPDATE `players` SET `language` = @Language 
            WHERE `steamid` = @SteamId;",
            new { Language = language, SteamId = steamId });
    }

    public async Task<int> GetSessionCountAsync(ulong steamId)
    {
        if (_connection == null) return 0;

        return await _connection.QueryFirstOrDefaultAsync<int>(@"
            SELECT COUNT(*) FROM `player_sessions` WHERE `steamid` = @SteamId;",
            new { SteamId = steamId });
    }

    public async Task<int> GetTotalPlayersCountAsync()
    {
        if (_connection == null) return 0;

        return await _connection.QueryFirstOrDefaultAsync<int>(@"
            SELECT COUNT(*) FROM `players`;");
    }
}

public class PlayerData
{
    public string Language { get; set; } = "en";
    public int TotalPlaytimeSeconds { get; set; } = 0;
}
