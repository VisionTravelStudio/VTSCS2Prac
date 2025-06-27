using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VTSPrac;

/// <summary>
/// 玩家设置类
/// </summary>
public class PlayerSettings
{
    public bool ShowImpact { get; set; } = false;
    public bool BlindImmune { get; set; } = false;
    public DateTime ConnectTime { get; set; } = DateTime.Now;
    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;
    
    public PlayerSettings()
    {
        ShowImpact = false;
        BlindImmune = false;
        ConnectTime = DateTime.Now;
        TotalPlayTime = TimeSpan.Zero;
    }
}

/// <summary>
/// 玩家累计时长数据（用于持久化）
/// </summary>
public class PlayerPlayTimeData
{
    public ulong SteamID { get; set; }
    public string Name { get; set; } = "";
    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;
    public DateTime LastSeen { get; set; } = DateTime.Now;
}

/// <summary>
/// 玩家设置管理器
/// </summary>
public class PlayerSettingsManager
{
    private static readonly string ChatPrefix = $"{ChatColors.Red}[VTS Prac]{ChatColors.Default}";
    private readonly Dictionary<ulong, PlayerSettings> _playerSettings = new();
    private readonly string _playTimeDataPath;
    private Dictionary<ulong, PlayerPlayTimeData> _playTimeData = new();

    public PlayerSettingsManager()
    {
        _playTimeDataPath = Path.Combine(Server.GameDirectory, "csgo", "VTSPrac_playtime.json");
        LoadPlayTimeData();
    }

    /// <summary>
    /// 获取玩家设置，如果不存在则创建默认设置
    /// </summary>
    public PlayerSettings GetPlayerSettings(CCSPlayerController player)
    {
        if (player?.SteamID == null) return new PlayerSettings();

        if (!_playerSettings.ContainsKey(player.SteamID))
        {
            _playerSettings[player.SteamID] = new PlayerSettings();
        }

        return _playerSettings[player.SteamID];
    }

    /// <summary>
    /// 切换玩家的弹着点显示状态
    /// </summary>
    public void ToggleImpactDisplay(CCSPlayerController player)
    {
        if (player?.IsValid != true) return;

        var settings = GetPlayerSettings(player);
        settings.ShowImpact = !settings.ShowImpact;

        string status = settings.ShowImpact ? "开启" : "关闭";
        var color = settings.ShowImpact ? ChatColors.Green : ChatColors.Red;

        player.PrintToChat($"{ChatPrefix} {color}弹着点显示已{status}");

        // 如果开启了弹着点显示，可以在这里添加额外的逻辑
        if (settings.ShowImpact)
        {
            // 启用弹着点显示逻辑
            Console.WriteLine($"[VTS Prac] 玩家 {player.PlayerName} 开启了弹着点显示");
        }
        else
        {
            // 禁用弹着点显示逻辑
            Console.WriteLine($"[VTS Prac] 玩家 {player.PlayerName} 关闭了弹着点显示");
        }
    }

    /// <summary>
    /// 切换玩家的闪光弹免疫状态
    /// </summary>
    public void ToggleBlindImmunity(CCSPlayerController player)
    {
        if (player?.IsValid != true) return;

        var settings = GetPlayerSettings(player);
        settings.BlindImmune = !settings.BlindImmune;

        string status = settings.BlindImmune ? "开启" : "关闭";
        var color = settings.BlindImmune ? ChatColors.Green : ChatColors.Red;

        player.PrintToChat($"{ChatPrefix} {color}闪光弹免疫已{status}");

        Console.WriteLine($"[VTS Prac] 玩家 {player.PlayerName} {status}了闪光弹免疫");
    }

    /// <summary>
    /// 清除当前玩家的所有道具（包括扔出的和重投的）
    /// </summary>
    public void ClearPlayerGrenades(CCSPlayerController player)
    {
        if (player?.IsValid != true) return;

        try
        {
            int removedCount = 0;

            // 移除地图上所有与该玩家相关的道具实体
            var entities = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("smokegrenade_projectile")
                .Concat(Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("flashbang_projectile"))
                .Concat(Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("hegrenade_projectile"))
                .Concat(Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("molotov_projectile"))
                .Concat(Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("decoy_projectile"))
                .Concat(Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("inferno"))
                .Concat(Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("env_fire"))
                .Concat(Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("smokegrenade"));

            foreach (var entity in entities)
            {
                if (entity?.IsValid == true)
                {
                    // 检查实体是否属于该玩家
                    var entityOwner = entity.OwnerEntity?.Value;
                    if (entityOwner?.IsValid == true && entityOwner.Handle == player.PlayerPawn?.Value?.Handle)
                    {
                        entity.Remove();
                        removedCount++;
                    }
                }
            }

            player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已清除您的所有道具 ({removedCount} 个)");
            Console.WriteLine($"[VTS Prac] 玩家 {player.PlayerName} 清除了 {removedCount} 个道具");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 清除玩家道具时出错: {ex.Message}");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}清除道具时出错");
        }
    }

    /// <summary>
    /// 破坏地图中的所有可破坏物
    /// </summary>
    public void BreakAllBreakables(CCSPlayerController player)
    {
        if (player?.IsValid != true) return;

        try
        {
            int brokenCount = 0;

            // 查找所有可破坏的实体类型
            var breakableTypes = new[]
            {
                "func_breakable",
                "func_breakable_surf",
                "prop_dynamic",
                "prop_physics",
                "prop_physics_multiplayer",
                "func_physbox",
                "item_crate"
            };

            foreach (var breakableType in breakableTypes)
            {
                var entities = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(breakableType);
                foreach (var entity in entities)
                {
                    if (entity?.IsValid == true)
                    {
                        try
                        {
                            // 尝试破坏实体
                            entity.AcceptInput("Break");
                            entity.AcceptInput("Kill");
                            brokenCount++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[VTS Prac] 破坏实体 {breakableType} 时出错: {ex.Message}");
                        }
                    }
                }
            }

            if (brokenCount > 0)
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已破坏所有可破坏物 ({brokenCount} 个)");
                Console.WriteLine($"[VTS Prac] 玩家 {player.PlayerName} 破坏了 {brokenCount} 个可破坏物");
            }
            else
            {
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}当前地图没有可破坏物");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 破坏可破坏物时出错: {ex.Message}");
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}破坏可破坏物时出错");
        }
    }

    /// <summary>
    /// 检查玩家是否免疫闪光弹
    /// </summary>
    public bool IsBlindImmune(CCSPlayerController player)
    {
        if (player?.IsValid != true) return false;
        var settings = GetPlayerSettings(player);
        return settings.BlindImmune;
    }

    /// <summary>
    /// 检查玩家是否显示弹着点
    /// </summary>
    public bool ShouldShowImpact(CCSPlayerController player)
    {
        if (player?.IsValid != true) return false;
        var settings = GetPlayerSettings(player);
        return settings.ShowImpact;
    }

    /// <summary>
    /// 显示帮助信息
    /// </summary>
    public void ShowHelp(CCSPlayerController player, string? specificCommand = null)
    {
        if (player?.IsValid != true) return;

        if (string.IsNullOrEmpty(specificCommand))
        {
            // 显示所有命令列表
            ShowAllCommands(player);
        }
        else
        {
            // 显示特定命令的详细说明
            ShowCommandDetails(player, specificCommand);
        }
    }

    /// <summary>
    /// 显示所有命令列表
    /// </summary>
    private void ShowAllCommands(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== VTS Prac 命令列表 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}BOT管理:");
        player.PrintToChat($"{ChatPrefix} /bot, .bot - BOT管理命令");
        
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}道具练习:");
        player.PrintToChat($"{ChatPrefix} /clear, .clear, .c - 清除道具");
        player.PrintToChat($"{ChatPrefix} /rethrow, .ct, .rt - 重投道具");
        player.PrintToChat($"{ChatPrefix} /huidian, .hd - 回到投掷位置");
        player.PrintToChat($"{ChatPrefix} /qingyan, .qy - 清除烟雾");
        player.PrintToChat($"{ChatPrefix} /qinghuo, .qh - 清除火焰");
        
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}辅助功能:");
        player.PrintToChat($"{ChatPrefix} /impact, .impact, .dk - 弹着点显示");
        player.PrintToChat($"{ChatPrefix} /blind, .blind, .sg - 闪光弹免疫");
        player.PrintToChat($"{ChatPrefix} /break, .break, .bk - 破坏物品");
        
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}传送功能:");
        player.PrintToChat($"{ChatPrefix} /spawn, .sp, .s - 传送到出生点");
        player.PrintToChat($"{ChatPrefix} /teleport, .tp - 玩家传送");
        player.PrintToChat($"{ChatPrefix} /tpaccept, .a, .accept - 接受传送");
        
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}游戏模式:");
        player.PrintToChat($"{ChatPrefix} /gamemode, .gm, .g - 切换模式");
        player.PrintToChat($"{ChatPrefix} /god, .god, .wudi - 无敌模式");
        player.PrintToChat($"{ChatPrefix} /pleasewatchme, .pm - 观察请求");
        
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}使用 /help <命令> 查看详细说明");
    }

    /// <summary>
    /// 显示特定命令的详细说明
    /// </summary>
    private void ShowCommandDetails(CCSPlayerController player, string command)
    {
        // 移除前缀符号，统一处理
        string cleanCommand = command.TrimStart('/', '.');

        switch (cleanCommand.ToLower())
        {
            case "bot":
                ShowBotCommandHelp(player);
                break;
            case "clear":
            case "c":
                ShowClearCommandHelp(player);
                break;
            case "impact":
            case "dankong":
            case "dk":
                ShowImpactCommandHelp(player);
                break;
            case "blind":
            case "b":
            case "shanguang":
            case "sg":
                ShowBlindCommandHelp(player);
                break;
            case "break":
            case "bk":
                ShowBreakCommandHelp(player);
                break;
            case "rethrow":
            case "ct":
            case "rt":
            case "chongtou":
                ShowRethrowCommandHelp(player);
                break;
            case "huidian":
            case "hd":
                ShowHuidianeCommandHelp(player);
                break;
            case "qingyan":
            case "qy":
                ShowQingyanCommandHelp(player);
                break;
            case "qinghuo":
            case "qh":
                ShowQinghuoCommandHelp(player);
                break;
            case "spawn":
            case "sp":
            case "s":
            case "csd":
                ShowSpawnCommandHelp(player);
                break;
            case "teleport":
            case "tp":
                ShowTeleportCommandHelp(player);
                break;
            case "tpaccept":
            case "a":
            case "accept":
                ShowTpacceptCommandHelp(player);
                break;
            case "gamemode":
            case "gm":
            case "g":
                ShowGamemodeCommandHelp(player);
                break;
            case "god":
            case "wudi":
                ShowGodCommandHelp(player);
                break;
            case "pleasewatchme":
            case "plswm":
            case "pm":
                ShowWatchmeCommandHelp(player);
                break;
            case "help":
                ShowHelpCommandHelp(player);
                break;
            default:
                player.PrintToChat($"{ChatPrefix} {ChatColors.Red}未知命令: {command}");
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}使用 /help 查看所有可用命令");
                break;
        }
    }

    // 各种命令的详细帮助信息
    private void ShowBotCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== BOT管理命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令格式: /bot <子命令> [参数]");
        player.PrintToChat($"{ChatPrefix} 子命令:");
        player.PrintToChat($"{ChatPrefix} • spawn [name] [side ct/t] [at x y z] [status crouch/normal]");
        player.PrintToChat($"{ChatPrefix} • kick [name] - 踢出BOT (无参数=视线踢出)");
        player.PrintToChat($"{ChatPrefix} • kickall [ct/t] - 踢出所有BOT");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}示例: /bot spawn TestBot side ct at 100 100 64 status crouch");
    }

    private void ShowClearCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 清除道具命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /clear, .clear, .c");
        player.PrintToChat($"{ChatPrefix} 功能: 清除当前玩家扔出和重投的所有道具");
        player.PrintToChat($"{ChatPrefix} 包括: 烟雾弹、闪光弹、燃烧弹、手雷、诱饵弹");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 练习道具投掷时快速清理场地");
    }

    private void ShowImpactCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 弹着点显示命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /impact, .impact, /dk, .dk, /dankong, .dankong");
        player.PrintToChat($"{ChatPrefix} 功能: 切换显示当前玩家射击的弹着点位置");
        player.PrintToChat($"{ChatPrefix} 效果: 在屏幕中央显示子弹撞击点坐标");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 练习枪法时观察弹道轨迹");
    }

    private void ShowBlindCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 闪光弹免疫命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /blind, .blind, /b, .b, /sg, .sg");
        player.PrintToChat($"{ChatPrefix} 功能: 切换当前玩家是否会被闪光弹致盲");
        player.PrintToChat($"{ChatPrefix} 效果: 开启后免疫所有闪光弹效果");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 练习反闪技巧时保持视野");
    }

    private void ShowBreakCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 破坏可破坏物命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /break, .break, /bk, .bk");
        player.PrintToChat($"{ChatPrefix} 功能: 破坏地图中的所有可破坏物");
        player.PrintToChat($"{ChatPrefix} 包括: 箱子、栅栏、玻璃等环境元素");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 为道具投掷创造更好的环境");
    }

    private void ShowRethrowCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 重投道具命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /rethrow, .ct, .rt");
        player.PrintToChat($"{ChatPrefix} 功能: 重新投掷之前投掷的道具");
        player.PrintToChat($"{ChatPrefix} 参数: index=数字, type=道具类型, delay=延迟毫秒");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}示例: /rethrow index=1 delay=1000");
    }

    private void ShowHuidianeCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 回到投掷位置命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /huidian, .hd");
        player.PrintToChat($"{ChatPrefix} 功能: 传送回到上一次投掷道具的位置");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 重复练习同一个道具投掷点");
    }

    private void ShowQingyanCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 清除烟雾命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /qingyan, .qy");
        player.PrintToChat($"{ChatPrefix} 功能: 清除地图上所有烟雾弹效果");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 快速清理烟雾干扰");
    }

    private void ShowQinghuoCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 清除火焰命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /qinghuo, .qh");
        player.PrintToChat($"{ChatPrefix} 功能: 清除地图上所有燃烧弹火焰");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 快速清理火焰障碍");
    }

    private void ShowSpawnCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 出生点传送命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /spawn, .sp, .s");
        player.PrintToChat($"{ChatPrefix} 功能: 传送到出生点位置");
        player.PrintToChat($"{ChatPrefix} 参数: side=ct/t, type=best/worst, index=数字");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}示例: /spawn side=ct type=best");
    }

    private void ShowTeleportCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 传送命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /teleport, .tp");
        player.PrintToChat($"{ChatPrefix} 功能: 请求传送到其他玩家位置");
        player.PrintToChat($"{ChatPrefix} 用法: /tp <玩家名称或ID>");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}示例: /tp PlayerName");
    }

    private void ShowTpacceptCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 接受传送命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /tpaccept, .a, .accept");
        player.PrintToChat($"{ChatPrefix} 功能: 接受传送请求或观察请求");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 同意其他玩家的传送申请");
    }

    private void ShowGamemodeCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 游戏模式命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /gamemode, .gm, .g");
        player.PrintToChat($"{ChatPrefix} 功能: 切换玩家游戏模式 (管理员专用)");
        player.PrintToChat($"{ChatPrefix} 模式: normal(正常), spectator(观察者)");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}示例: /gamemode spectator");
    }

    private void ShowGodCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 无敌模式命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /god, .god, .wudi");
        player.PrintToChat($"{ChatPrefix} 功能: 切换无敌状态");
        player.PrintToChat($"{ChatPrefix} 效果: 免疫所有伤害");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 安全地练习各种技巧");
    }

    private void ShowWatchmeCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 观察请求命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /pleasewatchme, .pm, .plswm");
        player.PrintToChat($"{ChatPrefix} 功能: 请求其他玩家观察自己");
        player.PrintToChat($"{ChatPrefix} 用法: /pleasewatchme <玩家名称>");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}使用场景: 展示技巧或请求指导");
    }

    private void ShowHelpCommandHelp(CCSPlayerController player)
    {
        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}=== 帮助命令 ===");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Green}命令: /help [命令名称]");
        player.PrintToChat($"{ChatPrefix} 功能: 显示命令帮助信息");
        player.PrintToChat($"{ChatPrefix} 用法: /help - 显示所有命令");
        player.PrintToChat($"{ChatPrefix} 用法: /help <命令> - 显示特定命令详情");
        player.PrintToChat($"{ChatPrefix} {ChatColors.Blue}示例: /help clear");
    }

    /// <summary>
    /// 清理无效的玩家设置
    /// </summary>
    public void CleanupInvalidSettings()
    {
        var playersToRemove = new List<ulong>();

        foreach (var steamId in _playerSettings.Keys)
        {
            bool playerFound = false;
            
            // 检查该SteamID对应的玩家是否仍然在线
            foreach (var player in Utilities.GetPlayers())
            {
                if (player?.IsValid == true && player.SteamID == steamId)
                {
                    playerFound = true;
                    break;
                }
            }

            if (!playerFound)
            {
                playersToRemove.Add(steamId);
            }
        }

        foreach (var steamId in playersToRemove)
        {
            _playerSettings.Remove(steamId);
        }

        if (playersToRemove.Count > 0)
        {
            Console.WriteLine($"[VTS Prac] 清理了 {playersToRemove.Count} 个无效的玩家设置");
        }
    }

    /// <summary>
    /// 加载玩家时长数据
    /// </summary>
    private void LoadPlayTimeData()
    {
        try
        {
            if (File.Exists(_playTimeDataPath))
            {
                var json = File.ReadAllText(_playTimeDataPath);
                var dataList = JsonSerializer.Deserialize<List<PlayerPlayTimeData>>(json) ?? new List<PlayerPlayTimeData>();
                _playTimeData = dataList.ToDictionary(d => d.SteamID, d => d);
                Console.WriteLine($"[VTS Prac] 已加载 {_playTimeData.Count} 个玩家的游玩时长数据");
            }
            else
            {
                _playTimeData = new Dictionary<ulong, PlayerPlayTimeData>();
                Console.WriteLine("[VTS Prac] 未找到游玩时长数据文件，使用空数据");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 加载游玩时长数据失败: {ex.Message}");
            _playTimeData = new Dictionary<ulong, PlayerPlayTimeData>();
        }
    }

    /// <summary>
    /// 保存玩家时长数据
    /// </summary>
    private void SavePlayTimeData()
    {
        try
        {
            var dataList = _playTimeData.Values.ToList();
            var json = JsonSerializer.Serialize(dataList, new JsonSerializerOptions { WriteIndented = true });
            
            // 确保目录存在
            var directory = Path.GetDirectoryName(_playTimeDataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(_playTimeDataPath, json);
            Console.WriteLine($"[VTS Prac] 已保存 {dataList.Count} 个玩家的游玩时长数据");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 保存游玩时长数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 玩家连接时初始化时长统计
    /// </summary>
    public void OnPlayerConnect(CCSPlayerController player)
    {
        if (player?.SteamID == null) return;

        var settings = GetPlayerSettings(player);
        settings.ConnectTime = DateTime.Now;

        // 加载累计时长
        if (_playTimeData.ContainsKey(player.SteamID))
        {
            settings.TotalPlayTime = _playTimeData[player.SteamID].TotalPlayTime;
        }
        else
        {
            _playTimeData[player.SteamID] = new PlayerPlayTimeData
            {
                SteamID = player.SteamID,
                Name = player.PlayerName,
                TotalPlayTime = TimeSpan.Zero,
                LastSeen = DateTime.Now
            };
        }
    }

    /// <summary>
    /// 玩家断线时保存时长
    /// </summary>
    public void OnPlayerDisconnect(CCSPlayerController player)
    {
        if (player?.SteamID == null) return;

        var settings = GetPlayerSettings(player);
        var sessionTime = DateTime.Now - settings.ConnectTime;
        
        if (_playTimeData.ContainsKey(player.SteamID))
        {
            _playTimeData[player.SteamID].TotalPlayTime += sessionTime;
            _playTimeData[player.SteamID].Name = player.PlayerName;
            _playTimeData[player.SteamID].LastSeen = DateTime.Now;
        }

        SavePlayTimeData();
    }

    /// <summary>
    /// 获取玩家本次游玩时长
    /// </summary>
    public TimeSpan GetSessionPlayTime(CCSPlayerController player)
    {
        if (player?.SteamID == null) return TimeSpan.Zero;
        
        var settings = GetPlayerSettings(player);
        return DateTime.Now - settings.ConnectTime;
    }

    /// <summary>
    /// 获取玩家累计游玩时长
    /// </summary>
    public TimeSpan GetTotalPlayTime(CCSPlayerController player)
    {
        if (player?.SteamID == null) return TimeSpan.Zero;

        if (_playTimeData.ContainsKey(player.SteamID))
        {
            var sessionTime = GetSessionPlayTime(player);
            return _playTimeData[player.SteamID].TotalPlayTime + sessionTime;
        }

        return GetSessionPlayTime(player);
    }
}
