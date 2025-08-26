using System;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using VTSPrac.Database;
using VTSPrac.PlayTime;
using VTSPrac.Localization;

namespace VTSPrac.Debug;

public class DebugCommands
{
    private readonly DatabaseManager _databaseManager;
    private readonly PlayTimeManager _playTimeManager;
    private readonly LocalizationManager _localizationManager;
    private readonly ILogger _logger;

    public DebugCommands(DatabaseManager databaseManager, PlayTimeManager playTimeManager, 
                        LocalizationManager localizationManager, ILogger logger)
    {
        _databaseManager = databaseManager;
        _playTimeManager = playTimeManager;
        _localizationManager = localizationManager;
        _logger = logger;
    }

    public void OnDebugTimeCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        var currentTime = DateTime.UtcNow;
        var connectTime = _playTimeManager.GetPlayerConnectTime(steamId.Value);
        var lastSaveTime = _playTimeManager.GetPlayerLastSaveTime(steamId.Value);
        
        if (connectTime.HasValue)
        {
            var sessionTimeSeconds = (int)(currentTime - connectTime.Value).TotalSeconds;
            var sessionTimeMinutes = sessionTimeSeconds / 60;
            
            player.PrintToChat($"[DEBUG] Connect Time: {connectTime.Value:yyyy-MM-dd HH:mm:ss}");
            player.PrintToChat($"[DEBUG] Current Time: {currentTime:yyyy-MM-dd HH:mm:ss}");
            player.PrintToChat($"[DEBUG] Session Seconds: {sessionTimeSeconds}");
            player.PrintToChat($"[DEBUG] Session Minutes: {sessionTimeMinutes}");
        }
        else
        {
            player.PrintToChat("[DEBUG] No connect time recorded!");
        }
        
        if (lastSaveTime.HasValue)
        {
            var timeSinceLastSave = (int)(currentTime - lastSaveTime.Value).TotalSeconds;
            player.PrintToChat($"[DEBUG] Last Save Time: {lastSaveTime.Value:yyyy-MM-dd HH:mm:ss}");
            player.PrintToChat($"[DEBUG] Time Since Last Save: {timeSinceLastSave} seconds");
        }
        
        player.PrintToChat($"[DEBUG] Player connected: {_playTimeManager.IsPlayerConnected(steamId.Value)}");
        player.PrintToChat($"[DEBUG] Player has language: {_localizationManager.HasPlayerLanguageInMemory(steamId.Value)}");
        
        // Check database content
        if (_databaseManager.Connection != null)
        {
            Task.Run(async () =>
            {
                var playerData = await _databaseManager.GetPlayerDataAsync(steamId.Value);
                    
                Server.NextFrame(() =>
                {
                    if (playerData != null)
                    {
                        player.PrintToChat($"[DEBUG] DB Total Playtime: {playerData.TotalPlaytimeSeconds} seconds");
                        player.PrintToChat($"[DEBUG] DB Language: {playerData.Language}");
                        
                        var currentLang = _localizationManager.GetPlayerLanguage(steamId.Value);
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

    public void OnDebugLangCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        var currentLang = _localizationManager.GetPlayerLanguage(steamId.Value);
        player.PrintToChat($"[LANG DEBUG] Current language in memory: {currentLang}");
        
        // Test translation
        var testKey = "stats.title";
        var localizedText = _localizationManager.GetLocalizedText(player, testKey);
        
        player.PrintToChat($"[LANG DEBUG] Localized text for '{testKey}': {localizedText}");
        player.PrintToChat($"[LANG DEBUG] Has player in memory: {_localizationManager.HasPlayerLanguageInMemory(steamId.Value)}");
    }

    public void OnDebugDbCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || _databaseManager.Connection == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        Task.Run(async () =>
        {
            try
            {
                var playerData = await _databaseManager.GetPlayerDataAsync(steamId.Value);
                var sessionCount = await _databaseManager.GetSessionCountAsync(steamId.Value);
                var totalPlayersInDb = await _databaseManager.GetTotalPlayersCountAsync();

                Server.NextFrame(() =>
                {
                    player.PrintToChat($"[DB DEBUG] Database connection: {(_databaseManager.Connection?.State == System.Data.ConnectionState.Open ? "Open" : "Closed")}");
                    player.PrintToChat($"[DB DEBUG] Total players in DB: {totalPlayersInDb}");
                    
                    if (playerData != null)
                    {
                        player.PrintToChat($"[DB DEBUG] Player exists in DB: Yes");
                        player.PrintToChat($"[DB DEBUG] Total playtime: {playerData.TotalPlaytimeSeconds} seconds");
                        player.PrintToChat($"[DB DEBUG] Language: {playerData.Language}");
                        player.PrintToChat($"[DB DEBUG] Total sessions: {sessionCount}");
                    }
                    else
                    {
                        player.PrintToChat("[DB DEBUG] Player not found in database!");
                    }
                });
            }
            catch (Exception ex)
            {
                Server.NextFrame(() =>
                {
                    player.PrintToChat($"[DB DEBUG] Error: {ex.Message}");
                    _logger.LogError(ex, "Database debug error for player {SteamId}", steamId);
                });
            }
        });
    }
}
