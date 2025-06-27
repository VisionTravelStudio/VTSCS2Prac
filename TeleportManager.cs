using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;
using System.Linq;

namespace VTSPrac;

/// <summary>
/// 传送请求类型
/// </summary>
public enum TeleportRequestType
{
    TpHere,
    TpTo
}

/// <summary>
/// 传送请求
/// </summary>
public class TeleportRequest
{
    public CCSPlayerController Requester { get; set; }
    public CCSPlayerController Target { get; set; }
    public TeleportRequestType Type { get; set; }
    public DateTime CreatedAt { get; set; }

    public TeleportRequest(CCSPlayerController requester, CCSPlayerController target, TeleportRequestType type)
    {
        Requester = requester;
        Target = target;
        Type = type;
        CreatedAt = DateTime.Now;
    }

    /// <summary>
    /// 检查请求是否过期（5分钟）
    /// </summary>
    public bool IsExpired => DateTime.Now.Subtract(CreatedAt).TotalMinutes > 5;
}

/// <summary>
/// 传送管理器
/// </summary>
public class TeleportManager
{
    private static readonly string ChatPrefix = $"{ChatColors.Red}[VTS Prac]{ChatColors.Default}";
    private readonly Dictionary<CCSPlayerController, TeleportRequest> _pendingRequests = new();

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
        if (player?.PlayerPawn?.Value == null) return null;

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
            const float maxDistance = 2000f; // 最大检测距离
            const float maxAngle = 30f; // 最大角度差

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

                // 计算角度
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
    /// 传送玩家到指定位置
    /// </summary>
    public bool TeleportToPosition(CCSPlayerController player, Vector position, QAngle? angles = null)
    {
        if (player?.PlayerPawn?.Value == null) return false;

        try
        {
            var playerPawn = player.PlayerPawn.Value;
            var teleportAngles = angles ?? playerPawn.EyeAngles;
            
            // 稍微抬高一点避免卡在地面里
            var adjustedPosition = new Vector(position.X, position.Y, position.Z + 2.0f);
            
            playerPawn.Teleport(adjustedPosition, teleportAngles, new Vector(0, 0, 0));
            
            // 确保玩家状态正常
            Server.NextFrame(() =>
            {
                if (playerPawn.IsValid)
                {
                    // 重置速度
                    if (playerPawn.AbsVelocity != null)
                    {
                        playerPawn.AbsVelocity.X = 0;
                        playerPawn.AbsVelocity.Y = 0;
                        playerPawn.AbsVelocity.Z = 0;
                    }
                    
                    // 清除可能导致无法移动的标志
                    var currentFlags = playerPawn.Flags;
                    
                    // 清除冻结、蹲下等可能影响移动的标志
                    uint flagsToRemove = (uint)(
                        PlayerFlags.FL_FROZEN |
                        PlayerFlags.FL_DUCKING
                    );
                    
                    // 确保玩家在地面上
                    uint flagsToAdd = (uint)PlayerFlags.FL_ONGROUND;
                    
                    playerPawn.Flags = (currentFlags & ~flagsToRemove) | flagsToAdd;
                    
                    // 更新网络状态
                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_fFlags");
                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_vecAbsOrigin");
                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_angAbsRotation");
                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_vecAbsVelocity");
                }
            });
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 传送玩家到位置时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 传送玩家到另一个玩家
    /// </summary>
    public bool TeleportToPlayer(CCSPlayerController player, CCSPlayerController target, bool copyFacing = false)
    {
        if (player?.PlayerPawn?.Value == null || target?.PlayerPawn?.Value == null) return false;

        try
        {
            var playerPawn = player.PlayerPawn.Value;
            var targetPawn = target.PlayerPawn.Value;
            
            var targetPosition = targetPawn.AbsOrigin;
            if (targetPosition == null) return false;

            var targetAngles = copyFacing ? targetPawn.EyeAngles : playerPawn.EyeAngles;

            // 创建新的位置对象以避免重叠，并稍微抬高
            var newPosition = new Vector(
                targetPosition.X + 50, 
                targetPosition.Y + 50, 
                targetPosition.Z + 2.0f
            );

            playerPawn.Teleport(newPosition, targetAngles, new Vector(0, 0, 0));
            
            // 确保玩家状态正常
            Server.NextFrame(() =>
            {
                if (playerPawn.IsValid)
                {
                    // 重置速度
                    if (playerPawn.AbsVelocity != null)
                    {
                        playerPawn.AbsVelocity.X = 0;
                        playerPawn.AbsVelocity.Y = 0;
                        playerPawn.AbsVelocity.Z = 0;
                    }
                    
                    // 清除可能导致无法移动的标志
                    var currentFlags = playerPawn.Flags;
                    
                    // 清除冻结、蹲下等可能影响移动的标志
                    uint flagsToRemove = (uint)(
                        PlayerFlags.FL_FROZEN |
                        PlayerFlags.FL_DUCKING
                    );
                    
                    // 确保玩家在地面上
                    uint flagsToAdd = (uint)PlayerFlags.FL_ONGROUND;
                    
                    playerPawn.Flags = (currentFlags & ~flagsToRemove) | flagsToAdd;
                    
                    // 更新网络状态
                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_fFlags");
                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_vecAbsOrigin");
                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_angAbsRotation");
                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_vecAbsVelocity");
                }
            });
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 传送玩家到玩家时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 解析坐标字符串
    /// </summary>
    public Vector? ParseCoordinates(string coordStr)
    {
        try
        {
            var parts = coordStr.Split(',');
            if (parts.Length != 3) return null;

            if (float.TryParse(parts[0].Trim(), out float x) &&
                float.TryParse(parts[1].Trim(), out float y) &&
                float.TryParse(parts[2].Trim(), out float z))
            {
                return new Vector(x, y, z);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 解析坐标时出错: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 发送传送请求
    /// </summary>
    public void SendTeleportRequest(CCSPlayerController requester, CCSPlayerController target, TeleportRequestType type)
    {
        var request = new TeleportRequest(requester, target, type);
        _pendingRequests[target] = request;

        string requestText = type == TeleportRequestType.TpHere 
            ? "传送到他的位置" 
            : "传送他到你的位置";

        target.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}{requester.PlayerName} 请求{requestText}");
        target.PrintToChat($"{ChatPrefix} {ChatColors.Green}输入 /tpaccept 或 .a 来同意请求");
        target.PrintToChat($"{ChatPrefix} {ChatColors.Red}请求将在5分钟后过期");

        requester.PrintToChat($"{ChatPrefix} {ChatColors.Green}已向 {target.PlayerName} 发送传送请求");
    }

    /// <summary>
    /// 接受传送请求
    /// </summary>
    public bool AcceptTeleportRequest(CCSPlayerController player)
    {
        if (!_pendingRequests.TryGetValue(player, out var request))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}没有待处理的传送请求");
            return false;
        }

        if (request.IsExpired)
        {
            _pendingRequests.Remove(player);
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}传送请求已过期");
            return false;
        }

        if (!request.Requester.IsValid || !request.Target.IsValid)
        {
            _pendingRequests.Remove(player);
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}传送请求无效（玩家已离线）");
            return false;
        }

        bool success = false;
        string action = "";

        if (request.Type == TeleportRequestType.TpHere)
        {
            // 传送请求者到目标玩家
            success = TeleportToPlayer(request.Requester, request.Target);
            action = $"{request.Requester.PlayerName} 传送到了 {request.Target.PlayerName}";
        }
        else
        {
            // 传送目标玩家到请求者
            success = TeleportToPlayer(request.Target, request.Requester);
            action = $"{request.Target.PlayerName} 传送到了 {request.Requester.PlayerName}";
        }

        _pendingRequests.Remove(player);

        if (success)
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Green}传送请求已接受");
            request.Requester.PrintToChat($"{ChatPrefix} {ChatColors.Green}{action}");
        }
        else
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}传送失败");
            request.Requester.PrintToChat($"{ChatPrefix} {ChatColors.Red}传送失败");
        }

        return success;
    }

    /// <summary>
    /// 清理过期请求
    /// </summary>
    public void CleanupExpiredRequests()
    {
        var expiredRequests = _pendingRequests.Where(kvp => kvp.Value.IsExpired).ToList();
        foreach (var kvp in expiredRequests)
        {
            _pendingRequests.Remove(kvp.Key);
        }
    }

    /// <summary>
    /// 检查玩家是否是管理员
    /// </summary>
    public bool IsAdmin(CCSPlayerController player)
    {
        // 这里可以根据需要实现管理员检查逻辑
        // 目前简单返回 true，实际使用时可以添加权限检查
        try
        {
            // 如果有权限系统，可以使用类似下面的代码：
            // return AdminManager.PlayerHasPermissions(player, "@css/root");
            
            // 临时实现：检查玩家是否是服务器主机或具有特定标志
            return player.AuthorizedSteamID?.SteamId64.ToString() == "76561198000000000"; // 替换为实际的管理员SteamID
        }
        catch
        {
            return false;
        }
    }
}
