using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using VTSPrac.Database;
using VTSPrac.PlayTime;
using VTSPrac.Localization;

namespace VTSPrac.Commands;

public class CommandHandlers
{
    private readonly DatabaseManager _databaseManager;
    private readonly PlayTimeManager _playTimeManager;
    private readonly LocalizationManager _localizationManager;
    private readonly ILogger _logger;
    private readonly BasePlugin _plugin;

    public CommandHandlers(DatabaseManager databaseManager, PlayTimeManager playTimeManager, 
                          LocalizationManager localizationManager, ILogger logger, BasePlugin plugin)
    {
        _databaseManager = databaseManager;
        _playTimeManager = playTimeManager;
        _localizationManager = localizationManager;
        _logger = logger;
        _plugin = plugin;
    }

    public void OnHelloCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return;

        var name = commandInfo.ArgCount > 1 ? commandInfo.GetArg(1) : player.PlayerName;
        player.PrintToChat(_localizationManager.GetLocalizedText(player, "commands.hello", name));
    }

    public void OnPlaytimeCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || _databaseManager.Connection == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        // Calculate current session time
        var sessionTime = _playTimeManager.GetCurrentSessionTime(steamId.Value);

        Task.Run(async () =>
        {
            var playerData = await _databaseManager.GetPlayerDataAsync(steamId.Value);

            Server.NextFrame(() =>
            {
                var totalSeconds = playerData?.TotalPlaytimeSeconds ?? 0;
                var currentSessionMinutes = sessionTime / 60;
                var currentSessionHours = currentSessionMinutes / 60;
                var totalMinutes = totalSeconds / 60;
                var totalHours = totalMinutes / 60;

                player.PrintToChat(" \x04=== Playtime Stats ===\x01");
                
                if (currentSessionHours > 0)
                    player.PrintToChat(_localizationManager.GetLocalizedText(player, "playtime.current.hours", currentSessionHours, currentSessionMinutes % 60));
                else
                    player.PrintToChat(_localizationManager.GetLocalizedText(player, "playtime.current.minutes", currentSessionMinutes));
                
                if (totalHours > 0)
                    player.PrintToChat(_localizationManager.GetLocalizedText(player, "playtime.total.hours", totalHours, totalMinutes % 60));
                else
                    player.PrintToChat(_localizationManager.GetLocalizedText(player, "playtime.total.minutes", totalMinutes));
            });
        });
    }

    public void OnStatsCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || _databaseManager.Connection == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        // Calculate current session time
        var sessionTime = _playTimeManager.GetCurrentSessionTime(steamId.Value);

        Task.Run(async () =>
        {
            var playerData = await _databaseManager.GetPlayerDataAsync(steamId.Value);
            var sessionCount = await _databaseManager.GetSessionCountAsync(steamId.Value);

            Server.NextFrame(() =>
            {
                if (playerData != null)
                {
                    var totalSeconds = playerData.TotalPlaytimeSeconds;
                    var totalMinutes = totalSeconds / 60;
                    var totalHours = totalMinutes / 60;
                    var currentSessionMinutes = sessionTime / 60;
                    var currentSessionHours = currentSessionMinutes / 60;
                    
                    player.PrintToChat(" \x04=== " + _localizationManager.GetLocalizedText(player, "stats.title") + " ===\x01");
                    
                    // Current session display
                    if (currentSessionHours > 0)
                        player.PrintToChat(" \x09" + _localizationManager.GetLocalizedText(player, "stats.currentsession.hours", currentSessionHours, currentSessionMinutes % 60) + "\x01");
                    else
                        player.PrintToChat(" \x09" + _localizationManager.GetLocalizedText(player, "stats.currentsession.minutes", currentSessionMinutes) + "\x01");
                    
                    // Total playtime display
                    if (totalHours > 0)
                        player.PrintToChat(" \x04" + _localizationManager.GetLocalizedText(player, "stats.totalplaytime.hours", totalHours, totalMinutes % 60) + "\x01");
                    else
                        player.PrintToChat(" \x04" + _localizationManager.GetLocalizedText(player, "stats.totalplaytime.minutes", totalMinutes) + "\x01");
                        
                    player.PrintToChat(" \x06" + _localizationManager.GetLocalizedText(player, "stats.totalsessions", sessionCount) + "\x01");
                }
                else
                {
                    player.PrintToChat(_localizationManager.GetLocalizedText(player, "player.nodata"));
                }
            });
        });
    }

    public void OnLanguageCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return;

        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        var langCode = commandInfo.GetArg(1);
        
        // Validate language code
        if (!_localizationManager.IsValidLanguage(langCode))
        {
            player.PrintToChat("❌ Invalid language code. Available: en, zh");
            return;
        }
        
        // Update player's language preference
        _localizationManager.UpdatePlayerLanguageAsync(steamId.Value, langCode);
        
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

    public void OnReloadCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _logger.LogInformation("Reloading VTSPrac plugin configuration...");
        commandInfo.ReplyToCommand("VTSPrac plugin configuration reloaded successfully!");
    }

    public void OnInfoCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        commandInfo.ReplyToCommand("VTSPrac Plugin v1.0.0");
        commandInfo.ReplyToCommand("Author: VisionTravelStudio");
        commandInfo.ReplyToCommand("Description: A comprehensive plugin with translation, database and command features");
        commandInfo.ReplyToCommand($"Database: {(_databaseManager.Connection?.State == System.Data.ConnectionState.Open ? "Connected" : "Disconnected")}");
    }

    public void RegisterDynamicCommands()
    {
        // Register ping command dynamically
        _plugin.AddCommand("css_ping", "Responds with pong", (player, commandInfo) =>
        {
            if (player == null)
            {
                commandInfo.ReplyToCommand("pong server");
                return;
            }
            commandInfo.ReplyToCommand("pong");
        });

        // Register help command dynamically
        _plugin.AddCommand("css_help", "Shows available commands", (player, commandInfo) =>
        {
            if (player == null) return;
            player.PrintToChat(_localizationManager.GetLocalizedText(player, "commands.help"));
        });
    }
}
