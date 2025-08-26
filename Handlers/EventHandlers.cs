using System;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using VTSPrac.Database;
using VTSPrac.PlayTime;
using VTSPrac.Localization;

namespace VTSPrac.Handlers;

public class EventHandlers
{
    private readonly DatabaseManager _databaseManager;
    private readonly PlayTimeManager _playTimeManager;
    private readonly LocalizationManager _localizationManager;
    private readonly ILogger _logger;

    public EventHandlers(DatabaseManager databaseManager, PlayTimeManager playTimeManager, 
                        LocalizationManager localizationManager, ILogger logger)
    {
        _databaseManager = databaseManager;
        _playTimeManager = playTimeManager;
        _localizationManager = localizationManager;
        _logger = logger;
    }

    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        var playerName = player.PlayerName;

        if (steamId == null) return HookResult.Continue;

        // Record connect time for playtime tracking
        _playTimeManager.RecordPlayerConnect(steamId.Value);
        
        _logger.LogInformation("Player {Name} ({SteamId}) connected", playerName, steamId);

        // Welcome message with translation
        Server.NextFrame(() =>
        {
            player.PrintToChat(" \x04=== VTSPrac Server ===\x01");
            player.PrintToChat(_localizationManager.GetLocalizedText(player, "player.welcome", playerName));
        });

        // Update database in background
        Task.Run(async () =>
        {
            try
            {
                // Insert or update player info
                await _databaseManager.UpsertPlayerAsync(steamId.Value, playerName);
                Server.NextFrame(() => _logger.LogInformation("Player {SteamId} inserted/updated in database", steamId));

                // Load player's language preference
                var playerData = await _databaseManager.GetPlayerDataAsync(steamId.Value);
                
                if (playerData != null)
                {
                    _localizationManager.SetPlayerLanguage(steamId.Value, playerData.Language);
                    Server.NextFrame(() => _logger.LogInformation("Loaded language preference for player {SteamId}: {Language}, Total playtime: {Playtime}s", 
                        steamId, playerData.Language, playerData.TotalPlaytimeSeconds));
                }
                else
                {
                    // Set default language
                    _localizationManager.SetPlayerLanguage(steamId.Value, "en");
                    Server.NextFrame(() => _logger.LogInformation("Set default language for new player {SteamId}: en", steamId));
                }

                // Create new session record
                await _databaseManager.CreateSessionAsync(steamId.Value);
                Server.NextFrame(() => _logger.LogInformation("New session created for player {SteamId}", steamId));
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => _logger.LogError(ex, "Failed to update database for player {SteamId} ({Name})", steamId, playerName));
            }
        });

        return HookResult.Continue;
    }

    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return HookResult.Continue;

        // Calculate and save final playtime for this session
        var sessionTime = _playTimeManager.RecordPlayerDisconnect(steamId.Value);
        _localizationManager.RemovePlayer(steamId.Value);

        if (sessionTime.HasValue && sessionTime > 0)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _databaseManager.CloseSessionAsync(steamId.Value, sessionTime.Value);
                    Server.NextFrame(() => _logger.LogInformation("Player {SteamId} session closed, session time: {Time} seconds", 
                        steamId, sessionTime.Value));
                }
                catch (Exception ex)
                {
                    Server.NextFrame(() => _logger.LogError(ex, "Failed to close session for player {SteamId}", steamId));
                }
            });
        }

        return HookResult.Continue;
    }
}
