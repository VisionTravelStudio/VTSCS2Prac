using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace VTSPrac;

public class BotCommandParser
{
    public string? BotName { get; set; }
    public string? Side { get; set; }
    public float? X { get; set; }
    public float? Y { get; set; }
    public float? Z { get; set; }
    public string? Status { get; set; } // "crouch"/"normal"
    public bool HasCoordinates => X.HasValue && Y.HasValue && Z.HasValue;
    public bool IsCrouch => Status?.ToLower() == "crouch";
    
    public static BotCommandParser Parse(string[] args)
    {
        var parser = new BotCommandParser();
        
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLower();
            
            switch (arg)
            {
                case "side":
                    if (i + 1 < args.Length)
                    {
                        string side = args[i + 1].ToLower();
                        if (side == "ct" || side == "t")
                        {
                            parser.Side = side;
                            i++; // 跳过下一个参数
                        }
                    }
                    break;
                    
                case "at":
                    if (i + 3 < args.Length && 
                        float.TryParse(args[i + 1], out float x) &&
                        float.TryParse(args[i + 2], out float y) &&
                        float.TryParse(args[i + 3], out float z))
                    {
                        parser.X = x;
                        parser.Y = y;
                        parser.Z = z;
                        i += 3; // 跳过坐标参数
                    }
                    break;
                    
                case "status":
                    if (i + 1 < args.Length)
                    {
                        string status = args[i + 1].ToLower();
                        if (status == "crouch" || status == "normal")
                        {
                            parser.Status = status;
                            i++; // 跳过下一个参数
                        }
                    }
                    break;
                    
                // 保持向后兼容的蹲下关键字
                case "crouch":
                case "duck":
                case "squat":
                    parser.Status = "crouch";
                    break;
                    
                default:
                    // 如果不是关键字且还没有设置名称，则认为是BOT名称
                    if (parser.BotName == null && arg != "spawn" && arg != "kick" && arg != "kickall" && 
                        arg != "ct" && arg != "t" && arg != "crouch" && arg != "normal") // 排除队伍名称和状态
                    {
                        parser.BotName = args[i]; // 保持原始大小写
                    }
                    break;
            }
        }
        
        return parser;
    }
}

/// <summary>
/// BOT命令处理器 - 处理所有BOT相关的聊天和控制台命令
/// </summary>
public class BotCommandHandler
{
    private readonly BotManager _botManager;
    private static readonly string ChatPrefix = $"{ChatColors.Red}[VTS Prac]{ChatColors.Default}";

    public BotCommandHandler(BotManager botManager)
    {
        _botManager = botManager;
    }

    #region 聊天命令处理

    /// <summary>
    /// 处理聊天中的BOT相关命令
    /// </summary>
    public bool HandleChatBotCommand(CCSPlayerController player, string command, string[] args)
    {
        switch (command)
        {
            case ".bot":
                if (args.Length > 1)
                {
                    // 新的 .bot spawn/kick/kickall 格式
                    string subCommand = args[1].ToLower();
                    string[] subArgs = args.Skip(2).ToArray();
                    
                    switch (subCommand)
                    {
                        case "spawn":
                            Console.WriteLine("[VTS Prac] 处理 .bot spawn 命令");
                            HandleBotSpawn(player, subArgs);
                            break;
                        case "kick":
                            Console.WriteLine("[VTS Prac] 处理 .bot kick 命令");
                            HandleBotKick(player, subArgs);
                            break;
                        case "kickall":
                            Console.WriteLine("[VTS Prac] 处理 .bot kickall 命令");
                            HandleBotKickAll(player, subArgs);
                            break;
                        default:
                            // 提供帮助信息
                            ShowChatBotHelp(player);
                            break;
                    }
                }
                else
                {
                    // 简单的 .bot 命令 - 生成普通BOT
                    Console.WriteLine("[VTS Prac] 处理 .bot 命令 (简单格式)");
                    HandleChatBot(player);
                }
                return true;

            case ".dbot":
                Console.WriteLine("[VTS Prac] 处理 .dbot 命令");
                HandleChatDBot(player);
                return true;

            case ".ctbot":
                Console.WriteLine("[VTS Prac] 处理 .ctbot 命令");
                HandleChatCTBot(player);
                return true;

            case ".tbot":
                Console.WriteLine("[VTS Prac] 处理 .tbot 命令");
                HandleChatTBot(player);
                return true;

            case ".bot_spawn":
                Console.WriteLine("[VTS Prac] 处理 .bot_spawn 命令 (旧格式)");
                HandleChatBotSpawn(player, args);
                return true;

            case ".kick":
                Console.WriteLine("[VTS Prac] 处理 .kick 命令");
                HandleChatKick(player, args);
                return true;

            case ".kickall":
                Console.WriteLine("[VTS Prac] 处理 .kickall 命令");
                HandleChatKickAll(player);
                return true;

            default:
                return false; // 不是BOT命令
        }
    }

    private void HandleChatBot(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}正在你的位置生成BOT...");
        _botManager.SpawnBot(player);
    }

    private void HandleChatDBot(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}正在你的位置生成蹲下BOT...");
        _botManager.SpawnBot(player, "none", true);
    }

    private void HandleChatCTBot(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}正在你的位置生成CT BOT...");
        _botManager.SpawnBot(player, "ct");
    }

    private void HandleChatTBot(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}正在你的位置生成T BOT...");
        _botManager.SpawnBot(player, "t");
    }

    private void HandleChatKick(CCSPlayerController player, string[] args)
    {
        if (args.Length < 2)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: .kick <机器人名称>");
            return;
        }

        string botName = args[1];
        _botManager.KickBot(botName);
        player.PrintToChat($"{ChatPrefix} {ChatColors.LightBlue}正在踢出机器人: {botName}");
    }

    private void HandleChatKickAll(CCSPlayerController player)
    {
        int botCount = _botManager.KickAllBots();
        player.PrintToChat($"{ChatPrefix} {ChatColors.LightBlue}已踢出所有机器人 (总数: {botCount})");
    }

    private void HandleChatBotSpawn(CCSPlayerController player, string[] args)
    {
        if (args.Length < 4)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}用法: .bot_spawn <x> <y> <z> [team] [crouch]");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}示例: .bot_spawn 100 200 64 ct true");
            return;
        }

        if (!float.TryParse(args[1], out float x) || 
            !float.TryParse(args[2], out float y) || 
            !float.TryParse(args[3], out float z))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}坐标必须是数字！");
            return;
        }

        string team = args.Length > 4 ? args[4].ToLower() : "none";
        bool crouch = args.Length > 5 && (args[5].ToLower() == "true" || args[5] == "1");

        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}正在坐标 ({x}, {y}, {z}) 生成BOT...");
        _botManager.SpawnBotAtCoordinates(player, x, y, z, team, crouch);
    }

    #endregion

    #region 控制台命令处理

    /// <summary>
    /// 处理控制台BOT命令
    /// </summary>
    public void HandleConsoleBotCommand(CCSPlayerController player, string subCommand, string[] subArgs)
    {
        switch (subCommand)
        {
            case "spawn":
                HandleBotSpawn(player, subArgs);
                break;
            case "kick":
                HandleBotKick(player, subArgs);
                break;
            case "kickall":
                HandleBotKickAll(player, subArgs);
                break;
            default:
                ShowBotHelp(player);
                break;
        }
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

    #endregion

    #region 帮助信息

    public void ShowBotHelp(CCSPlayerController player)
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

    #endregion
}
