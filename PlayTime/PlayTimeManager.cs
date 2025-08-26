using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using VTSPrac.Database;

namespace VTSPrac.PlayTime;

public class PlayTimeManager
{
    private readonly DatabaseManager _databaseManager;
    private readonly ILogger _logger;
    private readonly Dictionary<ulong, DateTime> _playerConnectTimes = new();
    private readonly Dictionary<ulong, DateTime> _playerLastSaveTimes = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _saveTimer;
    private readonly BasePlugin _plugin;

    public PlayTimeManager(DatabaseManager databaseManager, ILogger logger, BasePlugin plugin)
    {
        _databaseManager = databaseManager;
        _logger = logger;
        _plugin = plugin;
    }

    public void StartAutoSaveTimer()
    {
        _saveTimer = _plugin.AddTimer(60.0f, SavePlaytimeForAllPlayers, TimerFlags.REPEAT);
    }

    public void StopAutoSaveTimer()
    {
        _saveTimer?.Kill();
        _saveTimer = null;
    }

    public void RecordPlayerConnect(ulong steamId)
    {
        var connectTime = DateTime.UtcNow;
        _playerConnectTimes[steamId] = connectTime;
        _playerLastSaveTimes[steamId] = connectTime;
        
        _logger.LogInformation("Player {SteamId} connect time recorded at {Time}", 
            steamId, connectTime.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    public int? RecordPlayerDisconnect(ulong steamId)
    {
        if (!_playerConnectTimes.TryGetValue(steamId, out var connectTime))
            return null;

        var sessionTime = (int)(DateTime.UtcNow - connectTime).TotalSeconds;
        _playerConnectTimes.Remove(steamId);
        _playerLastSaveTimes.Remove(steamId);

        _logger.LogInformation("Player {SteamId} disconnected, session time: {Time} seconds", 
            steamId, sessionTime);

        return sessionTime;
    }

    public int GetCurrentSessionTime(ulong steamId)
    {
        if (_playerConnectTimes.TryGetValue(steamId, out var connectTime))
        {
            return (int)(DateTime.UtcNow - connectTime).TotalSeconds;
        }
        return 0;
    }

    public bool IsPlayerConnected(ulong steamId)
    {
        return _playerConnectTimes.ContainsKey(steamId);
    }

    public DateTime? GetPlayerConnectTime(ulong steamId)
    {
        return _playerConnectTimes.TryGetValue(steamId, out var connectTime) ? connectTime : null;
    }

    public DateTime? GetPlayerLastSaveTime(ulong steamId)
    {
        return _playerLastSaveTimes.TryGetValue(steamId, out var lastSaveTime) ? lastSaveTime : null;
    }

    public void SavePlaytimeForAllPlayers()
    {
        if (_databaseManager.Connection == null) return;

        var currentTime = DateTime.UtcNow;
        var playersToUpdate = new List<(ulong SteamId, int PlaytimeToAdd)>();

        foreach (var kvp in _playerLastSaveTimes.ToList())
        {
            var steamId = kvp.Key;
            var lastSaveTime = kvp.Value;
            var timeSinceLastSave = (int)(currentTime - lastSaveTime).TotalSeconds;
            
            // Only update if at least 55 seconds have passed
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
                        await _databaseManager.UpdatePlayerPlaytimeAsync(steamId, playtimeToAdd);
                        Server.NextFrame(() => _logger.LogInformation("Updated playtime for player {SteamId}: +{PlaytimeToAdd} seconds", steamId, playtimeToAdd));
                    }
                    catch (Exception ex)
                    {
                        Server.NextFrame(() => _logger.LogError(ex, "Failed to update playtime for player {SteamId}", steamId));
                    }
                }
                
                Server.NextFrame(() => _logger.LogInformation("Completed playtime update for {Count} players", playersToUpdate.Count));
            });

            // Update last save time
            foreach (var steamId in playersToUpdate.Select(p => p.SteamId))
            {
                _playerLastSaveTimes[steamId] = currentTime;
            }
        }
    }

    public void SaveAllPlayerTimes()
    {
        SavePlaytimeForAllPlayers();
    }

    public void RemovePlayer(ulong steamId)
    {
        _playerConnectTimes.Remove(steamId);
        _playerLastSaveTimes.Remove(steamId);
    }
}
