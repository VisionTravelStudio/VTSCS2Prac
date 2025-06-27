using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace VTSPrac;

/// <summary>
/// 游戏模式类型
/// </summary>
public enum GameMode
{
    Normal,
    Spectator
}

/// <summary>
/// 观察请求
/// </summary>
public class WatchRequest
{
    public CCSPlayerController Requester { get; set; }
    public CCSPlayerController Target { get; set; }
    public DateTime CreatedAt { get; set; }

    public WatchRequest(CCSPlayerController requester, CCSPlayerController target)
    {
        Requester = requester;
        Target = target;
        CreatedAt = DateTime.Now;
    }

    /// <summary>
    /// 检查请求是否过期（5分钟）
    /// </summary>
    public bool IsExpired => DateTime.Now.Subtract(CreatedAt).TotalMinutes > 5;
}



/// <summary>
/// 游戏模式管理器
/// </summary>
public class GameModeManager
{
    private static readonly string ChatPrefix = $"{ChatColors.Red}[VTS Prac]{ChatColors.Default}";
    private readonly Dictionary<CCSPlayerController, WatchRequest> _pendingWatchRequests = new();
    private readonly HashSet<CCSPlayerController> _godModePlayers = new();
    private static readonly HashSet<string> _adminSteamIDs = new();
    private static readonly string AdminConfigPath = Path.Combine(Server.GameDirectory, "csgo", "cfg", "VTSPrac", "admins.cfg");

    /// <summary>
    /// 构造函数
    /// </summary>
    public GameModeManager()
    {
        LoadAdminConfig();
    }

    /// <summary>
    /// 切换玩家游戏模式
    /// </summary>
    public bool ChangePlayerGameMode(CCSPlayerController player, GameMode mode)
    {
        if (!IsPlayerValid(player)) return false;

        try
        {
            switch (mode)
            {
                case GameMode.Normal:
                    // 正常模式功能已删除
                    player.PrintToChat($"{ChatPrefix} {ChatColors.Red}正常模式切换功能已被禁用");
                    return false;

                case GameMode.Spectator:
                    // 切换到观察者模式 - 不再保存玩家状态
                    player.ChangeTeam(CsTeam.Spectator);
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 切换玩家游戏模式时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 设置观察目标
    /// </summary>
    public bool SetObserverTarget(CCSPlayerController observer, CCSPlayerController target)
    {
        if (observer?.PlayerPawn?.Value == null || target?.PlayerPawn?.Value == null) return false;

        try
        {
            // 首先确保观察者在观察者队伍
            if (observer.TeamNum != (int)CsTeam.Spectator)
            {
                observer.ChangeTeam(CsTeam.Spectator);
            }

            // 设置观察目标
            Server.NextFrame(() =>
            {
                if (observer.IsValid && target.IsValid && observer.PlayerPawn.Value != null)
                {
                    var observerPawn = observer.PlayerPawn.Value;
                    var targetPawn = target.PlayerPawn.Value;

                    if (observerPawn.IsValid && targetPawn != null && observerPawn.ObserverServices != null)
                    {
                        // 设置观察者模式
                        observerPawn.ObserverServices.ObserverMode = (byte)ObserverMode_t.OBS_MODE_IN_EYE;
                        
                        // 使用更安全的方式设置观察目标
                        try
                        {
                            // 尝试通过反射或其他方式设置目标
                            // 由于ObserverTarget是只读的，我们需要使用其他方法
                            observerPawn.ObserverServices.ObserverMode = (byte)ObserverMode_t.OBS_MODE_CHASE;
                            
                            // 传送观察者到目标附近
                            if (targetPawn.AbsOrigin != null)
                            {
                                var targetPos = targetPawn.AbsOrigin;
                                var observerPos = new Vector(targetPos.X + 50, targetPos.Y + 50, targetPos.Z + 100);
                                observerPawn.Teleport(observerPos, targetPawn.EyeAngles, new Vector(0, 0, 0));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[VTS Prac] 设置观察目标详细信息时出错: {ex.Message}");
                        }
                        
                        // 更新网络状态
                        Utilities.SetStateChanged(observerPawn, "CBasePlayerPawn", "m_pObserverServices");
                    }
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 设置观察目标时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 解析游戏模式
    /// </summary>
    public GameMode? ParseGameMode(string modeStr)
    {
        return modeStr.ToLower() switch
        {
            "normal" or "n" or "0" => GameMode.Normal,
            "spectator" or "spec" or "s" or "1" => GameMode.Spectator,
            _ => null
        };
    }

    /// <summary>
    /// 获取游戏模式显示名称
    /// </summary>
    public string GetGameModeDisplayName(GameMode mode)
    {
        return mode switch
        {
            GameMode.Normal => "正常模式",
            GameMode.Spectator => "观察者模式",
            _ => "未知模式"
        };
    }

    /// <summary>
    /// 解析玩家名称（支持特殊预设）
    /// </summary>
    public List<CCSPlayerController> ParsePlayerNames(string names, CCSPlayerController? requester = null)
    {
        var result = new List<CCSPlayerController>();
        var nameList = names.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var name in nameList)
        {
            var trimmedName = name.Trim();
            var players = ParseSinglePlayerName(trimmedName, requester);
            result.AddRange(players);
        }

        return result.Distinct().ToList();
    }

    private List<CCSPlayerController> ParseSinglePlayerName(string name, CCSPlayerController? requester)
    {
        var result = new List<CCSPlayerController>();

        switch (name.ToLower())
        {
            case "@a": // 所有玩家（不包括BOT）
                result.AddRange(Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot));
                break;
            case "@e": // 所有玩家（包括BOT）
                result.AddRange(Utilities.GetPlayers().Where(p => p.IsValid));
                break;
            case "@p": // 指令使用者目光指向的玩家
                if (requester != null)
                {
                    var targetPlayer = GetPlayerInSight(requester);
                    if (targetPlayer != null)
                        result.Add(targetPlayer);
                }
                break;
            case "@s": // 指令使用者
                if (requester != null)
                    result.Add(requester);
                break;
            case "@r": // 随机玩家（不包括BOT）
                var humanPlayers = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
                if (humanPlayers.Count > 0)
                {
                    var random = new Random();
                    result.Add(humanPlayers[random.Next(humanPlayers.Count)]);
                }
                break;
            default: // 具体玩家名称
                var player = Utilities.GetPlayers().FirstOrDefault(p => 
                    p.IsValid && p.PlayerName.Contains(name, StringComparison.OrdinalIgnoreCase));
                if (player != null)
                    result.Add(player);
                break;
        }

        return result;
    }

    /// <summary>
    /// 获取玩家视线指向的玩家
    /// </summary>
    private CCSPlayerController? GetPlayerInSight(CCSPlayerController player)
    {
        if (!IsPlayerValid(player)) return null;

        try
        {
            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn?.AbsOrigin == null || playerPawn.EyeAngles == null) return null;

            var eyePosition = playerPawn.AbsOrigin;
            var eyeAngles = playerPawn.EyeAngles;

            // 计算视线方向
            var forward = new Vector(
                (float)(Math.Cos(eyeAngles.Y * Math.PI / 180) * Math.Cos(eyeAngles.X * Math.PI / 180)),
                (float)(Math.Sin(eyeAngles.Y * Math.PI / 180) * Math.Cos(eyeAngles.X * Math.PI / 180)),
                (float)(-Math.Sin(eyeAngles.X * Math.PI / 180))
            );

            CCSPlayerController? closestPlayer = null;
            float closestDistance = float.MaxValue;
            const float maxDistance = 2000f;
            const float maxAngle = 30f;

            foreach (var targetPlayer in Utilities.GetPlayers())
            {
                if (!targetPlayer.IsValid || targetPlayer == player || targetPlayer.PlayerPawn?.Value == null)
                    continue;

                var targetPosition = targetPlayer.PlayerPawn.Value.AbsOrigin;
                if (targetPosition == null) continue;

                var direction = new Vector(
                    targetPosition.X - eyePosition.X,
                    targetPosition.Y - eyePosition.Y,
                    targetPosition.Z - eyePosition.Z
                );

                var distance = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
                if (distance > maxDistance) continue;

                var dot = (forward.X * direction.X + forward.Y * direction.Y + forward.Z * direction.Z) / distance;
                var angle = (float)(Math.Acos(Math.Clamp(dot, -1.0, 1.0)) * 180.0 / Math.PI);

                if (angle <= maxAngle && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = targetPlayer;
                }
            }

            return closestPlayer;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] GetPlayerInSight 错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 发送观察请求
    /// </summary>
    public void SendWatchRequest(CCSPlayerController requester, CCSPlayerController target)
    {
        var request = new WatchRequest(requester, target);
        _pendingWatchRequests[target] = request;

        target.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}{requester.PlayerName} 请求您观察他");
        target.PrintToChat($"{ChatPrefix} {ChatColors.Green}输入 /accept 或 .a 来同意请求");
        target.PrintToChat($"{ChatPrefix} {ChatColors.Red}请求将在5分钟后过期");

        requester.PrintToChat($"{ChatPrefix} {ChatColors.Green}已向 {target.PlayerName} 发送观察请求");
    }

    /// <summary>
    /// 接受观察请求
    /// </summary>
    public bool AcceptWatchRequest(CCSPlayerController player)
    {
        if (!_pendingWatchRequests.TryGetValue(player, out var request))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}没有待处理的观察请求");
            return false;
        }

        if (request.IsExpired)
        {
            _pendingWatchRequests.Remove(player);
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}观察请求已过期");
            return false;
        }

        if (!request.Requester.IsValid || !request.Target.IsValid)
        {
            _pendingWatchRequests.Remove(player);
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}观察请求无效（玩家已离线）");
            return false;
        }

        // 将目标玩家设置为观察者模式并观察请求者
        bool success = ChangePlayerGameMode(request.Target, GameMode.Spectator) &&
                      SetObserverTarget(request.Target, request.Requester);

        _pendingWatchRequests.Remove(player);

        if (success)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Green}已接受观察请求，现在观察 {request.Requester.PlayerName}");
            request.Requester.PrintToChat($"{ChatPrefix} {ChatColors.Green}{request.Target.PlayerName} 正在观察您");
        }
        else
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}设置观察模式失败");
            request.Requester.PrintToChat($"{ChatPrefix} {ChatColors.Red}观察请求失败");
        }

        return success;
    }

    /// <summary>
    /// 切换玩家无敌状态
    /// </summary>
    public bool ToggleGodMode(CCSPlayerController player)
    {
        if (!IsPlayerValid(player)) return false;

        try
        {
            bool isGodMode = _godModePlayers.Contains(player);
            
            if (isGodMode)
            {
                // 移除无敌状态
                _godModePlayers.Remove(player);
                player.PlayerPawn.Value!.TakesDamage = true;
                player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}无敌模式已关闭");
            }
            else
            {
                // 添加无敌状态
                _godModePlayers.Add(player);
                player.PlayerPawn.Value!.TakesDamage = false;
                player.PrintToChat($"{ChatPrefix} {ChatColors.Green}无敌模式已开启");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 切换无敌状态时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查玩家是否处于无敌状态
    /// </summary>
    public bool IsPlayerGodMode(CCSPlayerController player)
    {
        return _godModePlayers.Contains(player);
    }

    /// <summary>
    /// 清理过期的观察请求
    /// </summary>
    public void CleanupExpiredRequests()
    {
        var expiredRequests = _pendingWatchRequests
            .Where(kvp => kvp.Value.IsExpired)
            .ToList();

        foreach (var kvp in expiredRequests)
        {
            _pendingWatchRequests.Remove(kvp.Key);
            Console.WriteLine($"[VTS Prac] 清理过期的观察请求: {kvp.Value.Requester.PlayerName} -> {kvp.Key.PlayerName}");
        }
    }

    /// <summary>
    /// 管理员 SteamID64 列表
    /// </summary>
    private static readonly HashSet<string> AdminSteamIDs = new()
    {
        "76561198000000000", // 替换为实际的管理员SteamID64
        // "76561198111111111", // 添加更多管理员SteamID64
        // "76561198222222222", // 取消注释并替换为实际SteamID64
    };

    /// <summary>
    /// 加载管理员配置文件
    /// </summary>
    private static void LoadAdminConfig()
    {
        try
        {
            // 确保目录存在
            var configDir = Path.GetDirectoryName(AdminConfigPath);
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir!);
            }

            // 如果配置文件不存在，创建默认配置
            if (!File.Exists(AdminConfigPath))
            {
                CreateDefaultAdminConfig();
                return;
            }

            // 清空现有管理员列表
            _adminSteamIDs.Clear();

            // 读取配置文件
            var lines = File.ReadAllLines(AdminConfigPath);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // 跳过空行和注释行
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("//") || trimmedLine.StartsWith("#"))
                    continue;

                // 验证SteamID64格式（17位数字）
                if (trimmedLine.Length == 17 && trimmedLine.All(char.IsDigit))
                {
                    _adminSteamIDs.Add(trimmedLine);
                }
            }

            Console.WriteLine($"[VTS Prac] 已加载 {_adminSteamIDs.Count} 个管理员");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 加载管理员配置时出错: {ex.Message}");
            
            // 如果加载失败，使用默认管理员
            _adminSteamIDs.Clear();
            _adminSteamIDs.Add("76561198000000000"); // 默认管理员SteamID
        }
    }

    /// <summary>
    /// 创建默认管理员配置文件
    /// </summary>
    private static void CreateDefaultAdminConfig()
    {
        try
        {
            var defaultConfig = @"// VTSPrac 管理员配置文件
// 每行一个 SteamID64（17位数字）
// 以 // 或 # 开头的行为注释行
// 
// 如何获取 SteamID64：
// 1. 访问 https://steamid.io/
// 2. 输入你的 Steam 个人资料链接
// 3. 复制 steamID64 值
//
// 示例：
// 76561198123456789
// 76561198987654321
//
// 请在下方添加管理员的 SteamID64：

76561198000000000";

            File.WriteAllText(AdminConfigPath, defaultConfig);
            
            // 加载默认配置
            _adminSteamIDs.Clear();
            _adminSteamIDs.Add("76561198000000000");
            
            Console.WriteLine($"[VTS Prac] 已创建默认管理员配置文件: {AdminConfigPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 创建默认管理员配置文件时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 重新加载管理员配置
    /// </summary>
    public static void ReloadAdminConfig()
    {
        LoadAdminConfig();
    }

    /// <summary>
    /// 添加管理员
    /// </summary>
    public static bool AddAdmin(string steamId)
    {
        try
        {
            // 验证SteamID64格式
            if (string.IsNullOrEmpty(steamId) || steamId.Length != 17 || !steamId.All(char.IsDigit))
            {
                return false;
            }

            if (_adminSteamIDs.Contains(steamId))
            {
                return false; // 已存在
            }

            // 添加到内存列表
            _adminSteamIDs.Add(steamId);

            // 更新配置文件
            SaveAdminConfig();
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 添加管理员时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 移除管理员
    /// </summary>
    public static bool RemoveAdmin(string steamId)
    {
        try
        {
            if (!_adminSteamIDs.Remove(steamId))
            {
                return false; // 不存在
            }

            // 更新配置文件
            SaveAdminConfig();
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 移除管理员时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 保存管理员配置到文件
    /// </summary>
    private static void SaveAdminConfig()
    {
        try
        {
            var configLines = new List<string>
            {
                "// VTSPrac 管理员配置文件",
                "// 每行一个 SteamID64（17位数字）",
                "// 以 // 或 # 开头的行为注释行",
                "// ",
                "// 如何获取 SteamID64：",
                "// 1. 访问 https://steamid.io/",
                "// 2. 输入你的 Steam 个人资料链接",
                "// 3. 复制 steamID64 值",
                "//",
                "// 管理员列表："
            };

            configLines.AddRange(_adminSteamIDs);

            File.WriteAllLines(AdminConfigPath, configLines);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 保存管理员配置时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查玩家是否是管理员
    /// </summary>
    public bool IsAdmin(CCSPlayerController player)
    {
        try
        {
            var steamId = player.AuthorizedSteamID?.SteamId64.ToString();
            return !string.IsNullOrEmpty(steamId) && _adminSteamIDs.Contains(steamId);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 玩家断开连接时清理
    /// </summary>
    public void OnPlayerDisconnect(CCSPlayerController player)
    {
        _godModePlayers.Remove(player);
        _pendingWatchRequests.Remove(player);
        
        // 清理该玩家发送的请求
        var requestsToRemove = _pendingWatchRequests.Where(kvp => kvp.Value.Requester == player).ToList();
        foreach (var kvp in requestsToRemove)
        {
            _pendingWatchRequests.Remove(kvp.Key);
        }
    }

    /// <summary>
    /// 检查玩家是否有效并且连接正常
    /// </summary>
    private bool IsPlayerValid(CCSPlayerController player)
    {
        return player != null && 
               player.IsValid && 
               !player.IsBot && 
               player.Connected == PlayerConnectedState.PlayerConnected &&
               player.PlayerPawn?.Value != null;
    }
}
