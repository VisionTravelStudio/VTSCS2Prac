using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;
using VTSPrac.Database;
using VTSPrac.PlayTime;
using VTSPrac.Localization;
using VTSPrac.Handlers;
using VTSPrac.Commands;
using VTSPrac.Debug;

namespace VTSPrac;

[MinimumApiVersion(80)]
public class VTSPracPlugin : BasePlugin
{
    public override string ModuleName => "VTSPrac Plugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "VisionTravelStudio";
    public override string ModuleDescription => "A comprehensive plugin with translation, database and command features";

    // Module managers
    private DatabaseManager? _databaseManager;
    private PlayTimeManager? _playTimeManager;
    private LocalizationManager? _localizationManager;
    private EventHandlers? _eventHandlers;
    private CommandHandlers? _commandHandlers;
    private DebugCommands? _debugCommands;

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("Loading {PluginName}", ModuleName);
        
        // Initialize managers
        InitializeManagers();
        
        // Register event handlers and commands
        RegisterHandlers();

        Logger.LogInformation(@"
__      _________ _____ _____                
\ \    / /__   __/ ____|  __ \               
 \ \  / /   | | | (___ | |__) | __ __ _  ___ 
  \ \/ /    | |  \___ \|  ___/ '__/ _` |/ __|
   \  /     | |  ____) | |   | | | (_| | (__ 
    \/      |_| |_____/|_|   |_|  \__,_|\___|
                VTSPrac Plugin Loaded!");
    }

    private void InitializeManagers()
    {
        try
        {
            // Initialize database manager
            _databaseManager = new DatabaseManager(Logger);
            _databaseManager.Initialize();

            // Initialize playtime manager
            _playTimeManager = new PlayTimeManager(_databaseManager, Logger, this);
            _playTimeManager.StartAutoSaveTimer();

            // Initialize localization manager
            _localizationManager = new LocalizationManager(this, _databaseManager);

            Logger.LogInformation("All managers initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize managers");
        }
    }

    private void RegisterHandlers()
    {
        if (_databaseManager == null || _playTimeManager == null || _localizationManager == null)
        {
            Logger.LogError("Cannot register handlers: managers not initialized");
            return;
        }

        try
        {
            // Initialize event handlers
            _eventHandlers = new EventHandlers(_databaseManager, _playTimeManager, _localizationManager, Logger);

            // Initialize command handlers
            _commandHandlers = new CommandHandlers(_databaseManager, _playTimeManager, _localizationManager, Logger, this);
            _commandHandlers.RegisterDynamicCommands();

            // Initialize debug commands
            _debugCommands = new DebugCommands(_databaseManager, _playTimeManager, _localizationManager, Logger);

            Logger.LogInformation("All handlers registered successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to register handlers");
        }
    }

    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("Unloading VTSPrac Plugin");

        // Save all current playtime before unloading
        _playTimeManager?.SaveAllPlayerTimes();
        
        // Stop timers
        _playTimeManager?.StopAutoSaveTimer();
        
        // Close database connection
        _databaseManager?.Close();
        
        base.Unload(hotReload);
    }

    #region Event Handlers

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (_eventHandlers != null)
            return _eventHandlers.OnPlayerConnectFull(@event, info);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (_eventHandlers != null)
            return _eventHandlers.OnPlayerDisconnect(@event, info);
        return HookResult.Continue;
    }

    #endregion

    #region Commands

    [ConsoleCommand("css_hello", "Greets the player")]
    [CommandHelper(minArgs: 0, usage: "[name]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnHelloCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _commandHandlers?.OnHelloCommand(player, commandInfo);
    }

    [ConsoleCommand("css_playtime", "Shows player's current session and total playtime")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnPlaytimeCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _commandHandlers?.OnPlaytimeCommand(player, commandInfo);
    }

    [ConsoleCommand("css_stats", "Shows detailed player statistics")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnStatsCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _commandHandlers?.OnStatsCommand(player, commandInfo);
    }

    [ConsoleCommand("css_lang", "Change player's language preference")]
    [CommandHelper(minArgs: 1, usage: "[language_code]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnLanguageCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _commandHandlers?.OnLanguageCommand(player, commandInfo);
    }

    [ConsoleCommand("css_vtsprac_reload", "Reload VTSPrac plugin configuration")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/admin")]
    public void OnReloadCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _commandHandlers?.OnReloadCommand(player, commandInfo);
    }

    [ConsoleCommand("css_vtsprac_info", "Show VTSPrac plugin information")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/admin")]
    public void OnInfoCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _commandHandlers?.OnInfoCommand(player, commandInfo);
    }

    [ConsoleCommand("css_debug_time", "Debug current session time calculation")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnDebugTimeCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _debugCommands?.OnDebugTimeCommand(player, commandInfo);
    }

    [ConsoleCommand("css_debug_lang", "Debug language system")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnDebugLangCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _debugCommands?.OnDebugLangCommand(player, commandInfo);
    }

    [ConsoleCommand("css_debug_db", "Debug database content")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnDebugDbCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _debugCommands?.OnDebugDbCommand(player, commandInfo);
    }

    #endregion
}
