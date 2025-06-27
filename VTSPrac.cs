using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Listeners;
using CounterStrikeSharp.API.Modules.UserMessages;
using System.Timers;
using System.Reflection;
using System.Linq;

#if RELEASE
[assembly: System.Reflection.AssemblyMetadata("BuildVersion", "VTSPRAC_BUILD_20250627_RELEASE")]
[assembly: System.Reflection.AssemblyMetadata("Environment", "Release")]
#else
[assembly: System.Reflection.AssemblyMetadata("BuildVersion", "VTSPRAC_BUILD_DEBUG")]
[assembly: System.Reflection.AssemblyMetadata("Environment", "Debug")]
#endif

namespace VTSPrac;

[MinimumApiVersion(280)]
public class VTSPracPlugin : BasePlugin, IPluginConfig<PracticeConfig>
{
    public override string ModuleName => "VTS 跑图练习插件";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "VTSDT Guangyun Zhou";
    public override string ModuleDescription => "VTS 跑图练习插件";

    private static readonly string ChatPrefix = $"{ChatColors.Red}[VTS Prac]{ChatColors.Default}";
    private BotManager _botManager = new();
    private PlayerGrenadeManager _grenadeManager = new();
    private BotCommandHandler _botCommandHandler;
    private SpawnManager _spawnManager = new();
    private TeleportManager _teleportManager = new();
    private GameModeManager _gameModeManager = new();
    private PlayerSettingsManager _playerSettingsManager = new();
    private System.Timers.Timer? _announcementTimer;
    public PracticeConfig Config { get; set; } = new();

    public VTSPracPlugin()
    {
        _botCommandHandler = new BotCommandHandler(_botManager);
    }

    public void OnConfigParsed(PracticeConfig config)
    {
        Config = config;
        _botManager.SetConfig(config);
    }

    public override void Load(bool hotReload)
    {
        // 注册聊天命令监听器
        AddCommandListener("say", OnPlayerSay);
        AddCommandListener("say_team", OnPlayerSay);
        
        // 注册事件监听器
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterEventHandler<EventPlayerBlind>(OnPlayerBlind);
        RegisterEventHandler<EventBulletImpact>(OnBulletImpact);
        
        // 注册实体生成监听器以获取道具投掷信息
        RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
        
        Console.WriteLine($"[{ModuleName}] 插件加载成功！");
        Console.WriteLine($"[{ModuleName}] 统一命令格式:");
        Console.WriteLine($"[{ModuleName}]   /bot spawn [name] [side ct/t] [at x y z] [status crouch/normal]");
        Console.WriteLine($"[{ModuleName}]   /bot kick [name] - 踢出BOT (无参数=视线踢出)");
        Console.WriteLine($"[{ModuleName}]   /bot kickall [ct/t] - 踢出所有BOT");
        Console.WriteLine($"[{ModuleName}] 聊天命令: .bot, .dbot, .ctbot, .tbot, .kick, .kickall");
        Console.WriteLine($"[{ModuleName}] 道具投掷功能:");
        Console.WriteLine($"[{ModuleName}]   /rethrow [参数] - 重新投掷道具");
        Console.WriteLine($"[{ModuleName}]   /huidian(.hd) - 回到上次投掷位置");
        Console.WriteLine($"[{ModuleName}]   /qingyan(.qy) - 清除烟雾弹");
        Console.WriteLine($"[{ModuleName}]   /qinghuo(.qh) - 清除火焰弹");
        Console.WriteLine($"[{ModuleName}] 示例: /rethrow index=1 delay=1000");
        Console.WriteLine($"[{ModuleName}] 传送功能:");
        Console.WriteLine($"[{ModuleName}]   /spawn(.sp .s) - 传送到出生点");
        Console.WriteLine($"[{ModuleName}]   /teleport(.tp) - 玩家传送");
        Console.WriteLine($"[{ModuleName}]   /tphere - 请求传送玩家到自己");
        Console.WriteLine($"[{ModuleName}]   /tpaccept(.a .accept) - 接受传送请求");
        Console.WriteLine($"[{ModuleName}]   /tpahere - 强制传送所有玩家(管理员)");
        Console.WriteLine($"[{ModuleName}] 游戏模式功能:");
        Console.WriteLine($"[{ModuleName}]   /gamemode(.gm .g) - 切换玩家模式(管理员)");
        Console.WriteLine($"[{ModuleName}]   /specall - 强制所有玩家观察(管理员)");
        Console.WriteLine($"[{ModuleName}]   /pleasewatchme(.plswm .pm) - 请求玩家观察");
        Console.WriteLine($"[{ModuleName}]   /watchme - 强制玩家观察(管理员)");
        Console.WriteLine($"[{ModuleName}]   /god(.god .wudi) - 切换无敌状态");
        Console.WriteLine($"[{ModuleName}] 练习功能:");
        Console.WriteLine($"[{ModuleName}]   /clear(.clear .c) - 清除当前玩家扔出和重投的所有道具");
        Console.WriteLine($"[{ModuleName}]   /impact(.impact /dankong .dankong /dk .dk) - 切换显示弹着点");
        Console.WriteLine($"[{ModuleName}]   /blind(.blind /b .b .shanguang /shanguang .sg /sg) - 切换闪光弹免疫");
        Console.WriteLine($"[{ModuleName}]   /break(.break .bk /bk) - 破坏地图中的所有可破坏物");
        Console.WriteLine($"[{ModuleName}] 帮助系统:");
        Console.WriteLine($"[{ModuleName}]   /help(.help) [命令] - 显示命令帮助信息");
        Console.WriteLine($"[{ModuleName}]   使用 /help <命令名> 查看详细说明");
        Console.WriteLine($"[{ModuleName}] BOT死亡自动复活功能已启用");
        
        // 定期清理无效的BOT引用
        var cleanupTimer = new System.Timers.Timer(30000); // 每30秒检查一次
        cleanupTimer.Elapsed += (sender, e) =>
        {
            Server.NextFrame(() =>
            {
                _botManager.CleanupInvalidBots();
            });
        };
        cleanupTimer.Start();
        
        // 延迟加载配置文件，确保服务器完全启动
        Server.NextFrame(() =>
        {
            LoadPracticeConfig();
            // 初始化出生点管理器
            _spawnManager.Initialize();
        });

        // 定期清理传送请求
        var teleportCleanupTimer = new System.Timers.Timer(60000); // 每分钟检查一次
        teleportCleanupTimer.Elapsed += (sender, e) =>
        {
            Server.NextFrame(() =>
            {
                _teleportManager.CleanupExpiredRequests();
                _gameModeManager.CleanupExpiredRequests();
                _playerSettingsManager.CleanupInvalidSettings();
            });
        };
        teleportCleanupTimer.Start();

        // 聊天公告定时器 - 每120秒发送一次公告
        _announcementTimer = new System.Timers.Timer(120000); // 120秒
        _announcementTimer.Elapsed += (sender, e) =>
        {
            Server.NextFrame(() =>
            {
                SendChatAnnouncement();
            });
        };
        _announcementTimer.Start();
    }

    /// <summary>
    /// 加载练习配置文件
    /// </summary>
    private void LoadPracticeConfig()
    {
        try
        {
            // 构建配置文件路径: game/csgo/cfg/VTSPrac/prac.cfg
            string configPath = "VTSPrac/prac.cfg";
            
            Console.WriteLine($"[{ModuleName}] 尝试加载练习配置文件: {configPath}");
            
            // 执行配置文件
            Server.ExecuteCommand($"exec {configPath}");
            
            Console.WriteLine($"[{ModuleName}] 练习配置文件加载完成: {configPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] 加载练习配置文件时出错: {ex.Message}");
            Console.WriteLine($"[{ModuleName}] 请确保配置文件存在: game/csgo/cfg/VTSPrac/prac.cfg");
        }
    }

    /// <summary>
    /// 处理玩家死亡事件，检查是否是BOT并进行自动复活
    /// </summary>
    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        
        if (player == null || !player.IsValid || !player.IsBot)
            return HookResult.Continue;

        Console.WriteLine($"[VTS Prac] 检测到机器人死亡: {player.PlayerName}");
        
        // 委托给BotManager处理BOT死亡
        _botManager.OnBotDeath(player);
        
        return HookResult.Continue;
    }

    /// <summary>
    /// 处理道具投掷事件
    /// </summary>
    [GameEventHandler]
    public HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        // 获取玩家当前位置和角度
        var playerPawn = player.PlayerPawn?.Value;
        if (playerPawn?.AbsOrigin == null || playerPawn.AbsRotation == null)
            return HookResult.Continue;

        // 获取投掷的道具信息
        var weapon = @event.Weapon;
        if (string.IsNullOrEmpty(weapon))
        {
            // 尝试从玩家当前武器获取道具名称
            var activeWeapon = playerPawn.WeaponServices?.ActiveWeapon?.Value;
            if (activeWeapon?.DesignerName != null)
            {
                weapon = activeWeapon.DesignerName;
            }
        }

        Console.WriteLine($"[VTS Prac Debug] 检测到道具投掷 - 玩家: {player.PlayerName}, 道具: {weapon}");

        if (string.IsNullOrEmpty(weapon))
            return HookResult.Continue;

        // 临时注释掉，因为已改用 OnEntitySpawned 来记录道具投掷
        // _grenadeManager.AddGrenadeThrow(player, playerPawn.AbsOrigin, playerPawn.AbsRotation, weapon);
        
        return HookResult.Continue;
    }

    /// <summary>
    /// 处理武器开火事件（备用道具投掷监听）
    /// </summary>
    [GameEventHandler]
    public HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        var weapon = @event.Weapon;
        if (string.IsNullOrEmpty(weapon))
            return HookResult.Continue;

        // 只处理道具类型的武器
        var grenadeType = _grenadeManager.GetGrenadeTypeFromName(weapon);
        if (grenadeType == GrenadeType.Unknown)
            return HookResult.Continue;

        // 获取玩家当前位置和角度
        var playerPawn = player.PlayerPawn?.Value;
        if (playerPawn?.AbsOrigin == null || playerPawn.AbsRotation == null)
            return HookResult.Continue;

        Console.WriteLine($"[VTS Prac Debug] OnWeaponFire - 玩家: {player.PlayerName}, 道具: {weapon}");

        // 延迟一帧记录，确保投掷动作已开始
        Server.NextFrame(() =>
        {
            // 临时注释掉，因为已改用 OnEntitySpawned 来记录道具投掷
            // _grenadeManager.AddGrenadeThrow(player, playerPawn.AbsOrigin, playerPawn.AbsRotation, weapon);
        });
        
        return HookResult.Continue;
    }

    public HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        var message = commandInfo.GetArg(1)?.Trim();
        if (string.IsNullOrEmpty(message))
            return HookResult.Continue;

        // 输出调试信息
        Console.WriteLine($"[VTS Prac] 收到聊天消息: '{message}' 来自 {player.PlayerName}");
        
        // 检查是否是我们的命令（以.开头）
        if (!message.StartsWith('.'))
            return HookResult.Continue;
            
        Console.WriteLine($"[VTS Prac] 处理命令: {message}");
        
        try
        {
            HandleChatMessage(player, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 处理聊天命令错误: {ex.Message}");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}命令处理出错！");
        }
        
        return HookResult.Continue;
    }

    private void HandleChatMessage(CCSPlayerController player, string message)
    {
        string[] args = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0)
            return;

        string command = args[0].ToLower();
        Console.WriteLine($"[VTS Prac] 执行命令: {command}");

        // 先尝试处理BOT相关命令
        if (_botCommandHandler.HandleChatBotCommand(player, command, args))
        {
            return; // BOT命令已处理，直接返回
        }

        // 处理道具投掷相关命令
        switch (command)
        {
            case ".ct":
            case ".rt":
                Console.WriteLine("[VTS Prac] 处理 .ct/.rt 命令");
                HandleRethrowCommand(player, args.Skip(1).ToArray());
                break;
            case ".hd":
                Console.WriteLine("[VTS Prac] 处理 .hd 命令");
                HandleBackToPosition(player, 1);
                break;
            case ".qy":
                Console.WriteLine("[VTS Prac] 处理 .qy 命令");
                HandleClearSmokes(player);
                break;
            case ".qh":
                Console.WriteLine("[VTS Prac] 处理 .qh 命令");
                HandleClearFires(player);
                break;
            case ".sp":
            case ".s":
                Console.WriteLine("[VTS Prac] 处理 .sp/.s 命令");
                HandleSpawnCommand(player, args.Skip(1).ToArray());
                break;
            case ".tp":
                Console.WriteLine("[VTS Prac] 处理 .tp 命令");
                HandleTeleportCommand(player, args.Skip(1).ToArray());
                break;
            case ".a":
            case ".accept":
                Console.WriteLine("[VTS Prac] 处理 .a/.accept 命令");
                // 先尝试处理传送请求，如果没有则尝试观察请求
                if (!_teleportManager.AcceptTeleportRequest(player))
                {
                    _gameModeManager.AcceptWatchRequest(player);
                }
                break;
            case ".gm":
            case ".g":
                Console.WriteLine("[VTS Prac] 处理 .gm/.g 命令");
                HandleGameModeCommand(player, args.Skip(1).ToArray());
                break;
            case ".plswm":
            case ".pm":
                Console.WriteLine("[VTS Prac] 处理 .plswm/.pm 命令");
                HandlePleasWatchMeCommand(player, args.Skip(1).ToArray());
                break;
            case ".god":
            case ".wudi":
                Console.WriteLine("[VTS Prac] 处理 .god/.wudi 命令");
                _gameModeManager.ToggleGodMode(player);
                break;
            case ".clear":
            case ".c":
                Console.WriteLine("[VTS Prac] 处理 .clear/.c 命令");
                _playerSettingsManager.ClearPlayerGrenades(player);
                break;
            case ".impact":
            case ".dankong":
            case ".dk":
                Console.WriteLine("[VTS Prac] 处理 impact/dankong/dk 命令");
                _playerSettingsManager.ToggleImpactDisplay(player);
                break;
            case ".blind":
            case ".b":
            case ".shanguang":
            case ".sg":
                Console.WriteLine("[VTS Prac] 处理 blind/b/shanguang/sg 命令");
                _playerSettingsManager.ToggleBlindImmunity(player);
                break;
            case ".break":
            case ".bk":
                Console.WriteLine("[VTS Prac] 处理 break/bk 命令");
                _playerSettingsManager.BreakAllBreakables(player);
                break;
            case ".help":
                Console.WriteLine("[VTS Prac] 处理 .help 命令");
                if (args.Length > 1)
                {
                    // 显示特定命令的帮助
                    _playerSettingsManager.ShowHelp(player, args[1]);
                }
                else
                {
                    // 显示所有命令列表
                    _playerSettingsManager.ShowHelp(player);
                }
                break;
            default:
                Console.WriteLine($"[VTS Prac] 未知命令: {command}");
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}未知命令: {command}");
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}使用 /help 或 .help 查看所有可用命令");
                break;
        }
    }



    [ConsoleCommand("/bot", "BOT管理命令 (用法: /bot spawn|kick|kickall [参数])")]
    [ConsoleCommand("css_bot", "BOT管理命令 (CSS前缀版本)")]
    public void CommandBot(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        var args = command.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length < 2)
        {
            _botCommandHandler.ShowBotHelp(player);
            return;
        }

        string subCommand = args[1].ToLower();
        string[] subArgs = args.Skip(2).ToArray();

        _botCommandHandler.HandleConsoleBotCommand(player, subCommand, subArgs);
    }

    private void ShowBotHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}BOT命令用法:");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}/bot spawn [name] [side ct/t] [at x y z] [status crouch/normal]");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}/bot kick [name] - 踢出BOT (无参数=视线踢出)");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}/bot kickall [ct/t] - 踢出所有BOT");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}示例: /bot spawn TestBot side ct at 100 100 64 status crouch");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}参数顺序可任意调整");
    }

    private void ShowChatBotHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}聊天BOT命令用法:");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}.bot spawn [name] [side ct/t] [at x y z] [status crouch/normal]");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}.bot kick [name] - 踢出BOT (无参数=视线踢出)");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}.bot kickall [ct/t] - 踢出所有BOT");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}旧格式: .bot, .dbot, .ctbot, .tbot, .kick, .kickall");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}示例: .bot spawn TestBot side ct at 100 100 64 status crouch");
    }

    private void HandleBotSpawn(CCSPlayerController player, string[] args)
    {
        var parsed = BotCommandParser.Parse(args);
        
        Console.WriteLine($"[VTS Prac] 解析BOT生成命令 - 名称: {parsed.BotName}, 队伍: {parsed.Side}, 坐标: {parsed.X} {parsed.Y} {parsed.Z}, 状态: {parsed.Status}");
        
        string team = parsed.Side ?? "none";
        bool crouch = parsed.IsCrouch;
        
        if (parsed.HasCoordinates && parsed.X.HasValue && parsed.Y.HasValue && parsed.Z.HasValue)
        {
            string nameText = parsed.BotName != null ? $" '{parsed.BotName}'" : "";
            string crouchText = crouch ? " (蹲下)" : "";
            player.PrintToChat($"{ChatPrefix} {ChatColors.Green}正在坐标 ({parsed.X}, {parsed.Y}, {parsed.Z}) 生成BOT{nameText}{crouchText}...");
            _botManager.SpawnBotAtCoordinates(player, parsed.X.Value, parsed.Y.Value, parsed.Z.Value, team, crouch, parsed.BotName);
        }
        else
        {
            string nameText = parsed.BotName != null ? $" '{parsed.BotName}'" : "";
            string crouchText = crouch ? " (蹲下)" : "";
            player.PrintToChat($"{ChatPrefix} {ChatColors.Green}正在你的位置生成BOT{nameText}{crouchText}...");
            _botManager.SpawnBot(player, team, crouch, null, parsed.BotName);
        }
    }

    private void HandleBotKick(CCSPlayerController player, string[] args)
    {
        if (args.Length == 0)
        {
            // 踢出玩家视角指向的BOT
            _botManager.KickBotInSight(player);
        }
        else
        {
            // 踢出指定名称的BOT
            string botName = args[0];
            _botManager.KickBot(botName);
            player.PrintToChat($"{ChatPrefix} {ChatColors.LightBlue}正在踢出机器人: {botName}");
        }
    }

    private void HandleBotKickAll(CCSPlayerController player, string[] args)
    {
        string? team = null;
        if (args.Length > 0)
        {
            string teamArg = args[0].ToLower();
            if (teamArg == "ct" || teamArg == "t")
            {
                team = teamArg;
            }
        }
        
        int botCount = _botManager.KickAllBots(team);
        if (team != null)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.LightBlue}已踢出所有 {team.ToUpper()} 机器人 (总数: {botCount})");
        }
        else
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.LightBlue}已踢出所有机器人 (总数: {botCount})");
        }
    }

    [ConsoleCommand("/rethrow", "重新投掷道具命令")]
    [ConsoleCommand("/chongtou", "重新投掷道具命令")]
    [ConsoleCommand("css_rethrow", "重新投掷道具命令 (CSS前缀版本)")]
    public void CommandRethrow(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        var args = command.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] parameters = args.Skip(1).ToArray();
        
        HandleRethrowCommand(player, parameters);
    }

    [ConsoleCommand("/huidian", "回到上一次投掷道具位置")]
    [ConsoleCommand("css_huidian", "回到上一次投掷道具位置 (CSS前缀版本)")]
    public void CommandHuidian(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        HandleBackToPosition(player, 1);
    }

    [ConsoleCommand("/qingyan", "清除玩家投掷的烟雾弹")]
    [ConsoleCommand("css_qingyan", "清除玩家投掷的烟雾弹 (CSS前缀版本)")]
    public void CommandQingyan(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        HandleClearSmokes(player);
    }

    [ConsoleCommand("/qinghuo", "清除玩家投掷的火焰弹")]
    [ConsoleCommand("css_qinghuo", "清除玩家投掷的火焰弹 (CSS前缀版本)")]
    public void CommandQinghuo(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        HandleClearFires(player);
    }

    private void HandleRethrowCommand(CCSPlayerController player, string[] args)
    {
        if (args.Length == 0)
        {
            // 没有参数，投掷上一个道具
            RethrowLastGrenade(player);
            return;
        }

        // 解析参数
        int? type = null;
        int? index = null;
        int? delay = null;
        int? back = null;
        bool clear = false;
        bool list = false;

        foreach (string arg in args)
        {
            if (arg.ToLower() == "clear")
            {
                clear = true;
            }
            else if (arg.ToLower() == "list")
            {
                list = true;
            }
            else if (arg.StartsWith("type="))
            {
                if (int.TryParse(arg.Substring(5), out int typeValue))
                {
                    type = typeValue;
                }
            }
            else if (arg.StartsWith("index="))
            {
                if (int.TryParse(arg.Substring(6), out int indexValue))
                {
                    index = indexValue;
                }
            }
            else if (arg.StartsWith("delay="))
            {
                if (int.TryParse(arg.Substring(6), out int delayValue))
                {
                    delay = delayValue;
                }
            }
            else if (arg.StartsWith("back="))
            {
                if (int.TryParse(arg.Substring(5), out int backValue))
                {
                    back = backValue;
                }
            }
        }

        // 检查参数冲突
        if (type.HasValue && index.HasValue)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}type和index参数不能同时使用！");
            return;
        }

        if (back.HasValue && delay.HasValue)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}back和delay参数不能同时使用！");
            return;
        }

        // 执行操作
        if (clear)
        {
            _grenadeManager.ClearPlayerHistory(player);
            player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已清除所有道具投掷记录！");
            return;
        }

        if (list)
        {
            ShowGrenadeHistory(player);
            return;
        }

        if (back.HasValue)
        {
            HandleBackToPosition(player, back.Value);
            return;
        }

        // 获取要投掷的道具
        GrenadeThrowRecord? targetGrenade = null;

        if (type.HasValue)
        {
            if (type.Value >= 1 && type.Value <= 5)
            {
                targetGrenade = _grenadeManager.GetGrenadeByType(player, (GrenadeType)type.Value);
                if (targetGrenade == null)
                {
                    string typeName = _grenadeManager.GetGrenadeDisplayName((GrenadeType)type.Value);
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}没有找到投掷过的{typeName}！");
                    return;
                }
            }
            else
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}道具类型必须是1-5之间的数字！(1=烟雾弹 2=闪光弹 3=火焰弹 4=手雷 5=诱饵弹)");
                return;
            }
        }
        else if (index.HasValue)
        {
            if (index.Value >= 1)
            {
                targetGrenade = _grenadeManager.GetGrenadeByIndex(player, index.Value);
                if (targetGrenade == null)
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}没有找到索引为 {index.Value} 的道具记录！");
                    return;
                }
            }
            else
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}索引必须大于等于1！");
                return;
            }
        }
        else
        {
            // 默认投掷最后一个道具
            targetGrenade = _grenadeManager.GetLastGrenade(player);
            if (targetGrenade == null)
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}没有找到道具投掷记录！");
                return;
            }
        }

        // 执行重新投掷
        if (delay.HasValue)
        {
            DelayedRethrow(player, targetGrenade, delay.Value);
        }
        else
        {
            ExecuteRethrow(player, targetGrenade);
        }
    }

    private void RethrowLastGrenade(CCSPlayerController player)
    {
        var lastGrenade = _grenadeManager.GetLastGrenade(player);
        if (lastGrenade == null)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}没有找到道具投掷记录！");
            return;
        }

        ExecuteRethrow(player, lastGrenade);
    }

    private void ExecuteRethrow(CCSPlayerController player, GrenadeThrowRecord grenade)
    {
        // 直接投掷道具，不传送玩家
        grenade.ThrowGrenade(player);
        
        string displayName = _grenadeManager.GetGrenadeDisplayName(grenade.GrenadeType);
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已重新投掷 {displayName} (索引: {grenade.Index})");
    }

    private void DelayedRethrow(CCSPlayerController player, GrenadeThrowRecord grenade, int delayMs)
    {
        string displayName = _grenadeManager.GetGrenadeDisplayName(grenade.GrenadeType);
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}将在 {delayMs}ms 后投掷 {displayName} (索引: {grenade.Index})");
        
        // 直接使用 GrenadeThrowRecord 的延迟投掷方法
        grenade.ThrowGrenadeWithDelay(player, delayMs);
    }

    private void HandleBackToPosition(CCSPlayerController player, int index)
    {
        GrenadeThrowRecord? targetGrenade;
        
        if (index == 1)
        {
            targetGrenade = _grenadeManager.GetLastGrenade(player);
        }
        else
        {
            targetGrenade = _grenadeManager.GetGrenadeByIndex(player, index);
        }

        if (targetGrenade == null)
        {
            if (index == 1)
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}没有找到道具投掷记录！");
            }
            else
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}没有找到索引为 {index} 的道具记录！");
            }
            return;
        }

        targetGrenade.TeleportToThrowPosition(player);
        string displayName = _grenadeManager.GetGrenadeDisplayName(targetGrenade.GrenadeType);
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已回到 {displayName} 的投掷位置 (索引: {targetGrenade.Index})");
    }

    private void TeleportPlayerToPosition(CCSPlayerController player, Vector position, QAngle angles)
    {
        if (player.PlayerPawn?.Value != null)
        {
            var playerPawn = player.PlayerPawn.Value;
            
            // 稍微抬高一点避免卡在地面里
            var adjustedPosition = new Vector(position.X, position.Y, position.Z + 2.0f);
            
            playerPawn.Teleport(adjustedPosition, angles, new Vector(0, 0, 0));
        }
    }

    private void ShowGrenadeHistory(CCSPlayerController player)
    {
        var history = _grenadeManager.GetPlayerHistory(player);
        if (history.Count == 0)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}没有道具投掷记录！");
            return;
        }

        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}道具投掷历史记录:");
        foreach (var record in history.Take(10)) // 显示最新的10个
        {
            string displayName = _grenadeManager.GetGrenadeDisplayName(record.GrenadeType);
            string timeStr = record.ThrowTime.ToString("HH:mm:ss");
            player.PrintToChat($"{ChatPrefix} {ChatColors.LightBlue}{record.Index}. {displayName} - {timeStr}");
        }
        
        if (history.Count > 10)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}... 以及其他 {history.Count - 10} 个记录");
        }
    }

    private void HandleClearSmokes(CCSPlayerController player)
    {
        // 清除烟雾弹实体
        var smokeEntities = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
        int clearedCount = 0;
        
        foreach (var smoke in smokeEntities)
        {
            if (smoke?.IsValid == true)
            {
                smoke.Remove();
                clearedCount++;
            }
        }

        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已清除 {clearedCount} 个烟雾弹");
    }

    private void HandleClearFires(CCSPlayerController player)
    {
        // 清除火焰弹实体
        var fireEntities = Utilities.FindAllEntitiesByDesignerName<CInferno>("inferno");
        int clearedCount = 0;
        
        foreach (var fire in fireEntities)
        {
            if (fire?.IsValid == true)
            {
                fire.Remove();
                clearedCount++;
            }
        }

        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已清除 {clearedCount} 个火焰");
    }

    // 添加道具投掷记录的常量映射
    private static readonly Dictionary<string, string> ProjectileTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "smokegrenade_projectile", "weapon_smokegrenade" },
        { "flashbang_projectile", "weapon_flashbang" },
        { "hegrenade_projectile", "weapon_hegrenade" },
        { "decoy_projectile", "weapon_decoy" },
        { "molotov_projectile", "weapon_molotov" }
    };

    /// <summary>
    /// 监听实体生成事件，用于记录道具投掷信息
    /// </summary>
    public void OnEntitySpawned(CEntityInstance entity)
    {
        try
        {
            if (entity?.Entity?.DesignerName == null) return;
            
            // 检查是否是道具投掷物
            if (!ProjectileTypeMap.ContainsKey(entity.Entity.DesignerName)) return;

            Server.NextFrame(() =>
            {
                var projectile = new CBaseCSGrenadeProjectile(entity.Handle);
                
                if (!projectile.IsValid ||
                    !projectile.Thrower.IsValid ||
                    projectile.Thrower.Value == null ||
                    projectile.Thrower.Value.Controller.Value == null ||
                    projectile.Globalname == "custom") // 忽略我们自己创建的投掷物
                    return;

                var player = new CCSPlayerController(projectile.Thrower.Value.Controller.Value.Handle);
                if (!player.IsValid || player.IsBot || player.PlayerPawn?.Value == null) return;

                // 获取投掷物信息
                var throwPos = new Vector(projectile.AbsOrigin!.X, projectile.AbsOrigin.Y, projectile.AbsOrigin.Z);
                var throwAngles = new QAngle(projectile.AbsRotation!.X, projectile.AbsRotation.Y, projectile.AbsRotation.Z);
                var throwVel = new Vector(projectile.AbsVelocity.X, projectile.AbsVelocity.Y, projectile.AbsVelocity.Z);
                
                // 获取玩家信息
                var playerPos = new Vector(player.PlayerPawn.Value.CBodyComponent!.SceneNode!.AbsOrigin.X,
                                         player.PlayerPawn.Value.CBodyComponent.SceneNode.AbsOrigin.Y,
                                         player.PlayerPawn.Value.CBodyComponent.SceneNode.AbsOrigin.Z);
                var playerAngles = new QAngle(player.PlayerPawn.Value.EyeAngles.X,
                                            player.PlayerPawn.Value.EyeAngles.Y,
                                            player.PlayerPawn.Value.EyeAngles.Z);
                
                // 获取武器名称
                string weaponName = ProjectileTypeMap[entity.Entity.DesignerName];
                
                Console.WriteLine($"[VTS Prac Debug] OnEntitySpawned - 玩家: {player.PlayerName}, 投掷物: {entity.Entity.DesignerName}, 武器: {weaponName}");
                
                // 记录道具投掷
                _grenadeManager.AddGrenadeThrow(player, throwPos, throwAngles, throwVel, playerPos, playerAngles, weaponName);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] OnEntitySpawned 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理玩家完全连接事件，检查是否是BOT重新连接
    /// </summary>
    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        // 处理BOT连接
        if (player.IsBot)
        {
            Console.WriteLine($"[VTS Prac] 检测到机器人连接: {player.PlayerName}");
            _botManager.OnBotConnect(player);
        }
        else
        {
            // 处理真实玩家连接
            Console.WriteLine($"[VTS Prac] 检测到玩家连接: {player.PlayerName}");
            // 初始化玩家时长统计
            _playerSettingsManager.OnPlayerConnect(player);
        }
        
        return HookResult.Continue;
    }

    /// <summary>
    /// 处理玩家断开连接事件
    /// </summary>
    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        Console.WriteLine($"[VTS Prac] 检测到玩家断开连接: {player.PlayerName}");
        
        // 保存玩家时长统计
        if (!player.IsBot)
        {
            _playerSettingsManager.OnPlayerDisconnect(player);
        }
        
        // 清理游戏模式管理器状态
        _gameModeManager.OnPlayerDisconnect(player);
        
        return HookResult.Continue;
    }

    [ConsoleCommand("/spawn", "传送到出生点命令")]
    [ConsoleCommand("/sp", "传送到出生点命令 (简写)")]
    [ConsoleCommand("/csd", "传送到出生点命令 (别名)")]
    [ConsoleCommand("/s", "传送到出生点命令 (简写)")]
    [ConsoleCommand("css_spawn", "传送到出生点命令 (CSS前缀版本)")]
    public void CommandSpawn(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        var args = command.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] parameters = args.Skip(1).ToArray();
        
        HandleSpawnCommand(player, parameters);
    }

    [ConsoleCommand("/teleport", "传送命令")]
    [ConsoleCommand("/tp", "传送命令 (简写)")]
    [ConsoleCommand("css_teleport", "传送命令 (CSS前缀版本)")]
    public void CommandTeleport(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        var args = command.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] parameters = args.Skip(1).ToArray();
        
        HandleTeleportCommand(player, parameters);
    }

    [ConsoleCommand("/tpaccept", "接受传送请求")]
    [ConsoleCommand("/a", "接受传送请求 (简写)")]
    [ConsoleCommand("/accept", "接受传送请求")]
    [ConsoleCommand("css_tpaccept", "接受传送请求 (CSS前缀版本)")]
    public void CommandTpAccept(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        _teleportManager.AcceptTeleportRequest(player);
    }

    [ConsoleCommand("/tphere", "请求传送玩家到自己位置")]
    [ConsoleCommand("css_tphere", "请求传送玩家到自己位置 (CSS前缀版本)")]
    public void CommandTpHere(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        var args = command.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length < 2)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: /tphere <玩家名>");
            return;
        }

        string targetName = args[1];
        var targets = _teleportManager.ParsePlayerNames(targetName, player);
        
        if (targets.Count == 0)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到玩家: {targetName}");
            return;
        }

        foreach (var target in targets)
        {
            if (target == player)
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}不能向自己发送传送请求");
                continue;
            }

            _teleportManager.SendTeleportRequest(player, target, TeleportRequestType.TpHere);
        }
    }

    [ConsoleCommand("/tpahere", "强制传送所有玩家到自己位置（管理员专用）")]
    [ConsoleCommand("css_tpahere", "强制传送所有玩家到自己位置（管理员专用） (CSS前缀版本)")]
    public void CommandTpaHere(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (!_teleportManager.IsAdmin(player))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}此命令仅限管理员使用");
            return;
        }

        var humanPlayers = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p != player).ToList();
        int successCount = 0;

        foreach (var target in humanPlayers)
        {
            if (_teleportManager.TeleportToPlayer(target, player))
            {
                successCount++;
            }
        }

        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已传送 {successCount} 个玩家到您的位置");
    }

    /// <summary>
    /// 处理出生点传送命令
    /// </summary>
    private void HandleSpawnCommand(CCSPlayerController player, string[] args)
    {
        if (args.Length == 0)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: /spawn index=<数字> [side=<ct/t>] | type=<best/worst> | random");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}示例: /spawn index=1 side=ct");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}示例: /spawn type=best");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}示例: /spawn random");
            return;
        }

        // 解析参数
        int? index = null;
        CsTeam? side = null;
        SpawnType? type = null;
        bool random = false;

        foreach (string arg in args)
        {
            if (arg.ToLower() == "random")
            {
                random = true;
            }
            else if (arg.StartsWith("index="))
            {
                if (int.TryParse(arg.Substring(6), out int indexValue) && indexValue >= 1)
                {
                    index = indexValue;
                }
                else
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}索引必须是大于等于1的数字");
                    return;
                }
            }
            else if (arg.StartsWith("side="))
            {
                string sideValue = arg.Substring(5).ToLower();
                if (sideValue == "ct")
                {
                    side = CsTeam.CounterTerrorist;
                }
                else if (sideValue == "t")
                {
                    side = CsTeam.Terrorist;
                }
                else
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}队伍必须是 ct 或 t");
                    return;
                }
            }
            else if (arg.StartsWith("type="))
            {
                string typeValue = arg.Substring(5).ToLower();
                if (typeValue == "best")
                {
                    type = SpawnType.Best;
                }
                else if (typeValue == "worst")
                {
                    type = SpawnType.Worst;
                }
                else
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}类型必须是 best 或 worst");
                    return;
                }
            }
        }

        // 检查参数冲突
        int paramCount = 0;
        if (index.HasValue) paramCount++;
        if (type.HasValue) paramCount++;
        if (random) paramCount++;

        if (paramCount == 0)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}必须指定 index、type 或 random 参数之一");
            return;
        }

        if (paramCount > 1)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}index、type 和 random 参数不能同时使用");
            return;
        }

        // 检查 side 参数是否只与 index 一起使用
        if (side.HasValue && !index.HasValue)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}side 参数必须与 index 参数同时使用");
            return;
        }

        // 确定目标队伍
        CsTeam targetTeam = side ?? _spawnManager.GetPlayerTeam(player);
        
        // 获取出生点
        SpawnPoint? spawnPoint = null;

        if (index.HasValue)
        {
            spawnPoint = _spawnManager.GetSpawnPointByIndex(targetTeam, index.Value);
            if (spawnPoint == null)
            {
                string teamName = _spawnManager.GetTeamDisplayName(targetTeam);
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到 {teamName} 队伍的第 {index.Value} 号出生点");
                return;
            }
        }
        else if (type.HasValue)
        {
            spawnPoint = _spawnManager.GetSpawnPointByType(targetTeam, type.Value);
            if (spawnPoint == null)
            {
                string teamName = _spawnManager.GetTeamDisplayName(targetTeam);
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到 {teamName} 队伍的{(type.Value == SpawnType.Best ? "最佳" : "最差")}出生点");
                return;
            }
        }
        else if (random)
        {
            spawnPoint = _spawnManager.GetRandomSpawnPoint(targetTeam);
            if (spawnPoint == null)
            {
                string teamName = _spawnManager.GetTeamDisplayName(targetTeam);
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到 {teamName} 队伍的可用出生点");
                return;
            }
        }

        // 传送玩家
        if (spawnPoint != null && _spawnManager.TeleportToSpawn(player, spawnPoint))
        {
            string teamName = _spawnManager.GetTeamDisplayName(targetTeam);
            string description = index.HasValue ? $"第 {index.Value} 号" :
                               type.HasValue ? (type.Value == SpawnType.Best ? "最佳" : "最差") :
                               "随机";
            player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已传送到 {teamName} 队伍的{description}出生点");
        }
        else
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}传送失败");
        }
    }

    /// <summary>
    /// 处理传送命令
    /// </summary>
    private void HandleTeleportCommand(CCSPlayerController player, string[] args)
    {
        if (args.Length == 0)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: /tp player=<玩家名> [facing=<true/false>]");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: /tp pos=<x,y,z>");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: /tp player=<玩家1>,<玩家2> (管理员专用)");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}特殊玩家名: @a(所有玩家) @e(包含BOT) @p(视线玩家) @s(自己) @r(随机玩家)");
            return;
        }

        string? playerParam = null;
        Vector? posParam = null;
        bool facing = false;

        foreach (string arg in args)
        {
            if (arg.StartsWith("player="))
            {
                playerParam = arg.Substring(7);
            }
            else if (arg.StartsWith("pos="))
            {
                string posStr = arg.Substring(4);
                posParam = _teleportManager.ParseCoordinates(posStr);
                if (posParam == null)
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}坐标格式错误，应为: x,y,z");
                    return;
                }
            }
            else if (arg.StartsWith("facing="))
            {
                string facingStr = arg.Substring(7).ToLower();
                if (facingStr == "true" || facingStr == "1")
                {
                    facing = true;
                }
                else if (facingStr == "false" || facingStr == "0")
                {
                    facing = false;
                }
                else
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}facing 参数必须是 true/false 或 1/0");
                    return;
                }
            }
        }

        // 检查参数
        if (playerParam == null && posParam == null)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}必须指定 player 或 pos 参数");
            return;
        }

        if (playerParam != null && posParam != null)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}player 和 pos 参数不能同时使用");
            return;
        }

        if (facing && playerParam == null)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}facing 参数必须与 player 参数同时使用");
            return;
        }

        // 处理坐标传送
        if (posParam != null)
        {
            if (_teleportManager.TeleportToPosition(player, posParam))
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已传送到坐标 ({posParam.X:F1}, {posParam.Y:F1}, {posParam.Z:F1})");
            }
            else
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}传送失败");
            }
            return;
        }

        // 处理玩家传送
        if (playerParam != null)
        {
            // 检查是否是管理员传送其他玩家的格式
            if (playerParam.Contains(','))
            {
                if (!_teleportManager.IsAdmin(player))
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}传送其他玩家需要管理员权限");
                    return;
                }

                var playerNames = playerParam.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (playerNames.Length != 2)
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}格式错误，应为: player=<玩家1>,<玩家2>");
                    return;
                }

                var sources = _teleportManager.ParsePlayerNames(playerNames[0].Trim(), player);
                var targets = _teleportManager.ParsePlayerNames(playerNames[1].Trim(), player);

                if (sources.Count == 0)
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到玩家: {playerNames[0].Trim()}");
                    return;
                }

                if (targets.Count == 0)
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到玩家: {playerNames[1].Trim()}");
                    return;
                }

                if (targets.Count > 1)
                {
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}目标玩家只能指定一个");
                    return;
                }

                var target = targets[0];
                int successCount = 0;

                foreach (var sourcePlayer in sources)
                {
                    if (_teleportManager.TeleportToPlayer(sourcePlayer, target, facing))
                    {
                        successCount++;
                    }
                }

                player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已传送 {successCount} 个玩家到 {target.PlayerName}");
                return;
            }

            // 普通玩家传送
            var targetPlayers = _teleportManager.ParsePlayerNames(playerParam, player);

            if (targetPlayers.Count == 0)
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到玩家: {playerParam}");
                return;
            }

            if (targetPlayers.Count > 1)
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找到多个玩家，请使用更具体的名称");
                return;
            }

            var targetPlayer = targetPlayers[0];

            if (targetPlayer == player)
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}不能传送到自己");
                return;
            }

            if (_teleportManager.TeleportToPlayer(player, targetPlayer, facing))
            {
                string facingText = facing ? " (跟随视角)" : "";
                player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已传送到 {targetPlayer.PlayerName}{facingText}");
            }
            else
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}传送失败");
            }
        }
    }

    [ConsoleCommand("/gamemode", "游戏模式切换命令")]
    [ConsoleCommand("/gm", "游戏模式切换命令 (简写)")]
    [ConsoleCommand("/g", "游戏模式切换命令 (简写)")]
    [ConsoleCommand("css_gamemode", "游戏模式切换命令 (CSS前缀版本)")]
    public void CommandGameMode(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        var args = command.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] parameters = args.Skip(1).ToArray();
        
        HandleGameModeCommand(player, parameters);
    }

    [ConsoleCommand("/specall", "强制所有玩家进入观察者模式（管理员专用）")]
    [ConsoleCommand("css_specall", "强制所有玩家进入观察者模式（管理员专用） (CSS前缀版本)")]
    public void CommandSpecAll(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (!_gameModeManager.IsAdmin(player))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}此命令仅限管理员使用");
            return;
        }

        var humanPlayers = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p != player).ToList();
        int successCount = 0;

        foreach (var target in humanPlayers)
        {
            if (_gameModeManager.ChangePlayerGameMode(target, GameMode.Spectator))
            {
                successCount++;
            }
        }

        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已将 {successCount} 个玩家设置为观察者模式");
    }

    [ConsoleCommand("/pleasewatchme", "请求玩家观察自己")]
    [ConsoleCommand("/plswm", "请求玩家观察自己 (简写)")]
    [ConsoleCommand("/pm", "请求玩家观察自己 (简写)")]
    [ConsoleCommand("css_pleasewatchme", "请求玩家观察自己 (CSS前缀版本)")]
    public void CommandPleaseWatchMe(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        var args = command.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] parameters = args.Skip(1).ToArray();
        
        HandlePleasWatchMeCommand(player, parameters);
    }

    [ConsoleCommand("/watchme", "强制玩家观察自己（管理员专用）")]
    [ConsoleCommand("css_watchme", "强制玩家观察自己（管理员专用） (CSS前缀版本)")]
    public void CommandWatchMe(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (!_gameModeManager.IsAdmin(player))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}此命令仅限管理员使用");
            return;
        }

        var args = command.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length < 2)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: /watchme <玩家名>");
            return;
        }

        string targetName = args[1];
        var targets = _gameModeManager.ParsePlayerNames(targetName, player);
        
        if (targets.Count == 0)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到玩家: {targetName}");
            return;
        }

        int successCount = 0;
        foreach (var target in targets)
        {
            if (target == player)
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}不能强制自己观察自己");
                continue;
            }

            if (_gameModeManager.ChangePlayerGameMode(target, GameMode.Spectator) &&
                _gameModeManager.SetObserverTarget(target, player))
            {
                target.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}管理员要求您观察 {player.PlayerName}");
                successCount++;
            }
        }

        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已设置 {successCount} 个玩家观察您");
    }

    [ConsoleCommand("/god", "切换无敌状态")]
    [ConsoleCommand("/wudi", "切换无敌状态")]
    [ConsoleCommand("css_god", "切换无敌状态 (CSS前缀版本)")]
    public void CommandGod(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        _gameModeManager.ToggleGodMode(player);
    }

    /// <summary>
    /// 清除玩家道具控制台命令
    /// </summary>
    [ConsoleCommand("/clear", "清除当前玩家扔出和重投的所有道具")]
    [ConsoleCommand("css_clear", "清除当前玩家扔出和重投的所有道具 (CSS前缀版本)")]
    public void CommandClear(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        _playerSettingsManager.ClearPlayerGrenades(player);
    }

    /// <summary>
    /// 弹着点显示控制台命令
    /// </summary>
    [ConsoleCommand("/impact", "切换显示当前玩家射击的弹着点")]
    [ConsoleCommand("/dankong", "切换显示当前玩家射击的弹着点 (弹孔别名)")]
    [ConsoleCommand("/dk", "切换显示当前玩家射击的弹着点 (简写)")]
    [ConsoleCommand("css_impact", "切换显示当前玩家射击的弹着点 (CSS前缀版本)")]
    public void CommandImpact(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        _playerSettingsManager.ToggleImpactDisplay(player);
    }

    /// <summary>
    /// 闪光弹免疫控制台命令
    /// </summary>
    [ConsoleCommand("/blind", "切换当前玩家是否会被闪光弹致盲")]
    [ConsoleCommand("/b", "切换当前玩家是否会被闪光弹致盲 (简写)")]
    [ConsoleCommand("/shanguang", "切换当前玩家是否会被闪光弹致盲 (闪光别名)")]
    [ConsoleCommand("/sg", "切换当前玩家是否会被闪光弹致盲 (简写)")]
    [ConsoleCommand("css_blind", "切换当前玩家是否会被闪光弹致盲 (CSS前缀版本)")]
    public void CommandBlind(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        _playerSettingsManager.ToggleBlindImmunity(player);
    }

    /// <summary>
    /// 破坏可破坏物控制台命令
    /// </summary>
    [ConsoleCommand("/break", "破坏地图中的所有可破坏物")]
    [ConsoleCommand("/bk", "破坏地图中的所有可破坏物 (简写)")]
    [ConsoleCommand("css_break", "破坏地图中的所有可破坏物 (CSS前缀版本)")]
    public void CommandBreak(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        _playerSettingsManager.BreakAllBreakables(player);
    }

    /// <summary>
    /// 帮助命令控制台处理
    /// </summary>
    [ConsoleCommand("/help", "显示命令帮助信息")]
    [ConsoleCommand("css_help", "显示命令帮助信息 (CSS前缀版本)")]
    public void CommandHelp(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        var args = command.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length > 1)
        {
            // 显示特定命令的帮助
            string specificCommand = args[1];
            _playerSettingsManager.ShowHelp(player, specificCommand);
        }
        else
        {
            // 显示所有命令列表
            _playerSettingsManager.ShowHelp(player);
        }
    }

    /// <summary>
    /// 处理游戏模式切换命令
    /// </summary>
    private void HandleGameModeCommand(CCSPlayerController player, string[] args)
    {
        if (!_gameModeManager.IsAdmin(player))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}此命令仅限管理员使用");
            return;
        }

        if (args.Length < 2)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: /gamemode <玩家名> <模式>");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}模式: normal(n/0) - 正常模式, spectator(spec/s/1) - 观察者模式");
            return;
        }

        string playerName = args[0];
        string modeStr = args[1];

        var mode = _gameModeManager.ParseGameMode(modeStr);
        if (mode == null)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}无效的模式: {modeStr}");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}可用模式: normal(n/0), spectator(spec/s/1)");
            return;
        }

        var targets = _gameModeManager.ParsePlayerNames(playerName, player);
        if (targets.Count == 0)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到玩家: {playerName}");
            return;
        }

        int successCount = 0;
        string modeDisplayName = _gameModeManager.GetGameModeDisplayName(mode.Value);

        foreach (var target in targets)
        {
            if (_gameModeManager.ChangePlayerGameMode(target, mode.Value))
            {
                target.PrintToChat($"{ChatPrefix} {ChatColors.Green}您的游戏模式已切换为: {modeDisplayName}");
                successCount++;
            }
        }

        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已将 {successCount} 个玩家设置为{modeDisplayName}");
    }

    /// <summary>
    /// 处理请求观察命令
    /// </summary>
    private void HandlePleasWatchMeCommand(CCSPlayerController player, string[] args)
    {
        if (args.Length < 1)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: /pleasewatchme <玩家名>");
            return;
        }

        string targetName = args[0];
        var targets = _gameModeManager.ParsePlayerNames(targetName, player);
        
        if (targets.Count == 0)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}找不到玩家: {targetName}");
            return;
        }

        foreach (var target in targets)
        {
            if (target == player)
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}不能请求自己观察自己");
                continue;
            }

            _gameModeManager.SendWatchRequest(player, target);
        }
    }

    /// <summary>
    /// 重新加载管理员配置命令
    /// </summary>
    [ConsoleCommand("css_reloadadmins", "重新加载管理员配置")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void CommandReloadAdmins(CCSPlayerController? player, CommandInfo command)
    {
        // 只有当前管理员或控制台才能重新加载配置
        if (player != null && !_gameModeManager.IsAdmin(player))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}您没有权限使用此命令");
            return;
        }

        GameModeManager.ReloadAdminConfig();
        
        string message = $"{ChatPrefix} {ChatColors.Green}管理员配置已重新加载";
        if (player != null)
        {
            player.PrintToChat(message);
        }
        else
        {
            Console.WriteLine("[VTS Prac] 管理员配置已重新加载");
        }
    }

    /// <summary>
    /// 添加管理员命令
    /// </summary>
    [ConsoleCommand("css_addadmin", "添加管理员")]
    [CommandHelper(minArgs: 1, usage: "<SteamID64>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void CommandAddAdmin(CCSPlayerController? player, CommandInfo command)
    {
        // 只有当前管理员或控制台才能添加管理员
        if (player != null && !_gameModeManager.IsAdmin(player))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}您没有权限使用此命令");
            return;
        }

        string steamId = command.GetArg(1);
        if (string.IsNullOrEmpty(steamId))
        {
            string message = $"{ChatPrefix} {ChatColors.Yellow}用法: css_addadmin <SteamID64>";
            if (player != null)
                player.PrintToChat(message);
            else
                Console.WriteLine("[VTS Prac] " + message);
            return;
        }

        if (GameModeManager.AddAdmin(steamId))
        {
            string message = $"{ChatPrefix} {ChatColors.Green}已添加管理员: {steamId}";
            if (player != null)
                player.PrintToChat(message);
            else
                Console.WriteLine("[VTS Prac] " + message);
        }
        else
        {
            string message = $"{ChatPrefix} {ChatColors.Red}添加管理员失败 (可能已存在或格式错误): {steamId}";
            if (player != null)
                player.PrintToChat(message);
            else
                Console.WriteLine("[VTS Prac] " + message);
        }
    }

    /// <summary>
    /// 移除管理员命令
    /// </summary>
    [ConsoleCommand("css_removeadmin", "移除管理员")]
    [CommandHelper(minArgs: 1, usage: "<SteamID64>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void CommandRemoveAdmin(CCSPlayerController? player, CommandInfo command)
    {
        // 只有当前管理员或控制台才能移除管理员
        if (player != null && !_gameModeManager.IsAdmin(player))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}您没有权限使用此命令");
            return;
        }

        string steamId = command.GetArg(1);
        if (string.IsNullOrEmpty(steamId))
        {
            string message = $"{ChatPrefix} {ChatColors.Yellow}用法: css_removeadmin <SteamID64>";
            if (player != null)
                player.PrintToChat(message);
            else
                Console.WriteLine("[VTS Prac] " + message);
            return;
        }

        if (GameModeManager.RemoveAdmin(steamId))
        {
            string message = $"{ChatPrefix} {ChatColors.Green}已移除管理员: {steamId}";
            if (player != null)
                player.PrintToChat(message);
            else
                Console.WriteLine("[VTS Prac] " + message);
        }
        else
        {
            string message = $"{ChatPrefix} {ChatColors.Red}移除管理员失败 (可能不存在): {steamId}";
            if (player != null)
                player.PrintToChat(message);
            else
                Console.WriteLine("[VTS Prac] " + message);
        }
    }

    /// <summary>
    /// 处理玩家被闪光弹致盲事件
    /// </summary>
    [GameEventHandler]
    public HookResult OnPlayerBlind(EventPlayerBlind @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        // 检查玩家是否免疫闪光弹
        if (_playerSettingsManager.IsBlindImmune(player))
        {
            // 取消闪光效果
            Server.NextFrame(() =>
            {
                if (player.IsValid && player.PlayerPawn?.Value != null)
                {
                    // 重置闪光持续时间
                    player.PlayerPawn.Value.FlashDuration = 0.0f;
                    player.PlayerPawn.Value.FlashMaxAlpha = 0.0f;
                }
            });

            Console.WriteLine($"[VTS Prac] 玩家 {player.PlayerName} 免疫了闪光弹");
        }

        return HookResult.Continue;
    }

    /// <summary>
    /// 处理子弹撞击事件（用于显示弹着点）
    /// </summary>
    [GameEventHandler]
    public HookResult OnBulletImpact(EventBulletImpact @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        // 检查玩家是否开启了弹着点显示
        if (_playerSettingsManager.ShouldShowImpact(player))
        {
            var x = @event.X;
            var y = @event.Y;
            var z = @event.Z;

            try
            {
                // 在撞击点创建一个临时的视觉效果
                var impactPos = new Vector(x, y, z);
                
                // 创建一个临时的粒子效果或其他视觉标记
                // 这里可以根据需要添加更复杂的视觉效果
                Server.NextFrame(() =>
                {
                    if (player.IsValid)
                    {
                        // 向玩家发送撞击点信息
                        player.PrintToCenter($"撞击点: ({x:F1}, {y:F1}, {z:F1})");
                    }
                });

                Console.WriteLine($"[VTS Prac] 玩家 {player.PlayerName} 的子弹撞击点: ({x:F2}, {y:F2}, {z:F2})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VTS Prac] 处理弹着点显示时出错: {ex.Message}");
            }
        }

        return HookResult.Continue;
    }

    /// <summary>
    /// 发送聊天公告
    /// </summary>
    private void SendChatAnnouncement()
    {
        try
        {
            var players = Utilities.GetPlayers().Where(p => p?.IsValid == true && !p.IsBot).ToList();
            if (players.Count == 0) return;

            // 获取编译信息
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var buildVersion = "VTSPRAC_BUILD_20250627_RELEASE_v1.0.0";
            var environment = "Release";
            
            // 尝试从程序集特性获取信息
            try
            {
                var buildAttr = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                    .Cast<System.Reflection.AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "BuildVersion");
                if (buildAttr != null) buildVersion = buildAttr.Value;

                var envAttr = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                    .Cast<System.Reflection.AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "Environment");
                if (envAttr != null) environment = envAttr.Value;
            }
            catch
            {
                // 使用默认值
            }

            // 发送公告给所有玩家
            foreach (var player in players)
            {
                if (player?.IsValid != true) continue;

                var sessionTime = _playerSettingsManager.GetSessionPlayTime(player);
                var totalTime = _playerSettingsManager.GetTotalPlayTime(player);

                // 格式化时间显示
                var sessionTimeStr = FormatTimeSpan(sessionTime);
                var totalTimeStr = FormatTimeSpan(totalTime);

                // 发送分段公告信息
                player.PrintToChat($"{ChatPrefix}{ChatColors.Yellow} 欢迎来到 VTS Prac 服务器！");
                player.PrintToChat($"{ChatPrefix}{ChatColors.Lime}{ChatColors.Yellow}在控制台或聊天栏输入 {ChatColors.Lime}/help{ChatColors.White} 查看所有命令");
                player.PrintToChat($"{ChatPrefix}{ChatColors.LightBlue}本次跑图: {ChatColors.White}{sessionTimeStr} 累计跑图: {ChatColors.White}{totalTimeStr}");
                player.PrintToChat($"{ChatPrefix}{ChatColors.Red}POWER BY VTSDT. {ChatColors.Blue}Link:https://space.bilibili.com/614688308");
                player.PrintToChat($"{ChatColors.Grey}{ModuleVersion}_{buildVersion}_{environment}");
                player.PrintToChat($"{ChatColors.Grey}插件仓库链接：https://github.com/VisionTravelStudio/VTSCS2Prac");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] 发送聊天公告时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 格式化时间显示
    /// </summary>
    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{(int)timeSpan.TotalDays}天 {timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
        }
        else if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}小时 {timeSpan.Minutes}分钟";
        }
        else if (timeSpan.TotalMinutes >= 1)
        {
            return $"{(int)timeSpan.TotalMinutes}分钟 {timeSpan.Seconds}秒";
        }
        else
        {
            return $"{timeSpan.Seconds}秒";
        }
    }

    public override void Unload(bool hotReload)
    {
        try
        {
            // 停止公告定时器
            if (_announcementTimer != null)
            {
                _announcementTimer.Stop();
                _announcementTimer.Dispose();
                _announcementTimer = null;
            }

            // 保存所有在线玩家的时长数据
            var players = Utilities.GetPlayers().Where(p => p?.IsValid == true && !p.IsBot).ToList();
            foreach (var player in players)
            {
                _playerSettingsManager.OnPlayerDisconnect(player);
            }

            Console.WriteLine($"[{ModuleName}] 插件已卸载");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] 卸载插件时出错: {ex.Message}");
        }
    }
}