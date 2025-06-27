using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace VTSPrac;

public class BotSpawnInfo
{
    public Vector Position { get; set; }
    public QAngle Rotation { get; set; }
    public bool IsCrouched { get; set; }
    public string WeaponName { get; set; }
    public string TeamName { get; set; }
    public string? CustomName { get; set; }  // 添加自定义名称
    public DateTime SpawnTime { get; set; }  // 添加生成时间

    public BotSpawnInfo(Vector position, QAngle rotation, bool isCrouched, string weaponName, string teamName, string? customName = null)
    {
        Position = position;
        Rotation = rotation;
        IsCrouched = isCrouched;
        WeaponName = weaponName;
        TeamName = teamName;
        CustomName = customName;
        SpawnTime = DateTime.Now;
    }
}

public class BotManager
{
    private readonly List<CCSPlayerController> _managedBots = new();
    private readonly Dictionary<CCSPlayerController, BotSpawnInfo> _botSpawnInfos = new();
    // 使用名称映射来处理BOT重连和复活场景
    private readonly Dictionary<string, BotSpawnInfo> _botNameToSpawnInfo = new();
    private PracticeConfig? _config;
    
    private static readonly string ChatPrefix = $"{ChatColors.Red}[VTS 练习]{ChatColors.Default}";

    public void SetConfig(PracticeConfig config)
    {
        _config = config;
    }

    public void SpawnBot(CCSPlayerController player, string team = "none", bool crouch = false, Vector? customPosition = null, string? customName = null)
    {
        if (player?.PlayerPawn?.Value?.AbsOrigin == null || player?.PlayerPawn?.Value?.AbsRotation == null)
        {
            player?.PrintToChat($"{ChatPrefix} {ChatColors.Red}无法获取玩家位置！");
            return;
        }

        // Check bot limit
        if (_managedBots.Count >= (_config?.MaxBots ?? 32))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}已达到机器人数量上限！");
            return;
        }

        // Check if crouch bots are allowed
        if (crouch && !(_config?.AllowCrouchBots ?? true))
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}蹲下机器人功能已禁用！");
            return;
        }

        // 使用自定义位置或玩家位置
        var position = customPosition ?? player.PlayerPawn.Value.AbsOrigin;
        var rotation = player.PlayerPawn.Value.AbsRotation;
        
        // 获取玩家当前武器
        string playerWeapon = GetPlayerWeapon(player);
        
        // 保存生成前的BOT数量
        var beforeBotCount = Utilities.GetPlayers().Count(p => p.IsBot && p.IsValid);
        
        Console.WriteLine($"[VTS 练习] 尝试生成机器人，队伍: {team}");
        Console.WriteLine($"[VTS 练习] 生成前机器人数量: {beforeBotCount}");
        Console.WriteLine($"[VTS 练习] 玩家武器: {playerWeapon}");
        if (customPosition != null)
        {
            Console.WriteLine($"[VTS 练习] 自定义位置: {position.X}, {position.Y}, {position.Z}");
        }
        
        // Spawn bot using server command
        if (team.ToLower() == "ct")
        {
            Server.ExecuteCommand("bot_add_ct");
            Console.WriteLine("[VTS 练习] 执行: bot_add_ct");
        }
        else if (team.ToLower() == "t")
        {
            Server.ExecuteCommand("bot_add_t");
            Console.WriteLine("[VTS 练习] 执行: bot_add_t");
        }
        else
        {
            Server.ExecuteCommand("bot_add");
            Console.WriteLine("[VTS 练习] 执行: bot_add");
        }
        
        // Set bot difficulty if configured
        if (_config?.BotDifficulty >= 0)
        {
            Server.ExecuteCommand($"bot_difficulty {_config.BotDifficulty}");
        }
        
        // 等待多帧确保BOT生成完成
        Server.NextFrame(() =>
        {
            Server.NextFrame(() =>
            {
                Server.NextFrame(() =>
                {
                    // 查找新生成的BOT
                    var allBots = Utilities.GetPlayers().Where(p => p.IsBot && p.IsValid).ToList();
                    var afterBotCount = allBots.Count;
                    
                    Console.WriteLine($"[VTS 练习] 生成后机器人数量: {afterBotCount}");
                    
                    if (afterBotCount > beforeBotCount)
                    {
                        // 获取最新的BOT（通常是列表中的最后一个）
                        var bot = allBots.LastOrDefault();
                        
                        if (bot?.PlayerPawn?.Value != null)
                        {
                            Console.WriteLine($"[VTS 练习] 找到新机器人: {bot.PlayerName}");
                            
                            // 如果有自定义名称，尝试重命名BOT
                            if (!string.IsNullOrEmpty(customName))
                            {
                                // CS2中重命名BOT可能需要特殊处理
                                Console.WriteLine($"[VTS 练习] 尝试为机器人设置自定义名称: {customName}");
                            }
                            
                            // 使用新的配置方法
                            ConfigureBot(bot, position, rotation, crouch, playerWeapon);
                            
                            // 创建BOT生成信息，记录实际生成位置
                            var spawnInfo = new BotSpawnInfo(position, rotation, crouch, playerWeapon, team, customName);
                            
                            // 添加到管理列表和位置映射
                            _managedBots.Add(bot);
                            _botSpawnInfos[bot] = spawnInfo;
                            _botNameToSpawnInfo[bot.PlayerName] = spawnInfo;
                            
                            string positionText = customPosition != null ? 
                                $"坐标 ({position.X:F1}, {position.Y:F1}, {position.Z:F1})" : "你的位置";
                            string nameText = !string.IsNullOrEmpty(customName) ? $" (期望名称: {customName})" : "";
                            
                            player.PrintToChat($"{ChatPrefix} {ChatColors.LightBlue}机器人 '{bot.PlayerName}'{nameText} 已在{positionText}生成{(crouch ? " (蹲下)" : "")}，武器: {playerWeapon}！");
                            
                            Console.WriteLine($"[VTS 练习] 机器人 {bot.PlayerName} 位置信息已保存: {position.X:F2}, {position.Y:F2}, {position.Z:F2}");
                        }
                        else
                        {
                            Console.WriteLine("[VTS 练习] 找到机器人但 PlayerPawn 为空");
                            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}机器人已生成但配置失败！");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[VTS 练习] 生成命令后未检测到新机器人");
                        player.PrintToChat($"{ChatPrefix} {ChatColors.Red}机器人生成失败！请检查服务器控制台错误信息。");
                    }
                });
            });
        });
    }

    public void KickBot(string botName)
    {
        var bot = Utilities.GetPlayers().FirstOrDefault(p => 
            p.IsBot && p.PlayerName.Contains(botName, StringComparison.OrdinalIgnoreCase));
            
        if (bot != null)
        {
            string actualBotName = bot.PlayerName;
            Server.ExecuteCommand($"bot_kick {actualBotName}");
            
            // 清理所有相关记录
            _managedBots.Remove(bot);
            _botSpawnInfos.Remove(bot);
            _botNameToSpawnInfo.Remove(actualBotName);
            
            Console.WriteLine($"[VTS 练习] 机器人 '{actualBotName}' 已被踢出，位置信息已清理。");
        }
        else
        {
            // 即使找不到BOT对象，也尝试清理名称映射
            var matchingNames = _botNameToSpawnInfo.Keys
                .Where(name => name.Contains(botName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            foreach (var name in matchingNames)
            {
                _botNameToSpawnInfo.Remove(name);
                Console.WriteLine($"[VTS 练习] 清理了机器人 '{name}' 的位置信息。");
            }
            
            if (matchingNames.Count == 0)
            {
                Console.WriteLine($"[VTS 练习] 未找到机器人 '{botName}'。");
            }
        }
    }

    public void KickBotInSight(CCSPlayerController player)
    {
        // 简化实现：踢出最近的BOT
        // 在实际实现中，你可能需要使用射线检测来找到玩家视线中的BOT
        var nearestBot = Utilities.GetPlayers()
            .Where(p => p.IsBot && p.IsValid && p.PlayerPawn?.Value != null)
            .OrderBy(bot => 
            {
                var botPos = bot.PlayerPawn?.Value?.AbsOrigin;
                var playerPos = player.PlayerPawn?.Value?.AbsOrigin;
                if (botPos == null || playerPos == null) return float.MaxValue;
                
                var dx = botPos.X - playerPos.X;
                var dy = botPos.Y - playerPos.Y;
                var dz = botPos.Z - playerPos.Z;
                return dx * dx + dy * dy + dz * dz; // 距离平方
            })
            .FirstOrDefault();
            
        if (nearestBot != null)
        {
            string botName = nearestBot.PlayerName;
            Server.ExecuteCommand($"bot_kick {botName}");
            
            // 清理所有相关记录
            _managedBots.Remove(nearestBot);
            _botSpawnInfos.Remove(nearestBot);
            _botNameToSpawnInfo.Remove(botName);
            
            player.PrintToChat($"{ChatPrefix} {ChatColors.LightBlue}已踢出视线中的机器人: {botName}");
            Console.WriteLine($"[VTS 练习] 踢出视线中的机器人: {botName}，位置信息已清理");
        }
        else
        {
            player.PrintToChat($"{ChatPrefix} {ChatColors.Red}未找到可踢出的机器人");
        }
    }

    public int KickAllBots(string? team = null)
    {
        int kickedCount = 0;
        
        if (team == null)
        {
            // 踢出所有BOT
            var allBots = Utilities.GetPlayers().Where(p => p.IsBot && p.IsValid).ToList();
            kickedCount = allBots.Count;
            
            // 记录所有BOT名称
            var botNames = allBots.Select(bot => bot.PlayerName).ToList();
            
            Server.ExecuteCommand("bot_kick");
            
            // 清理所有记录
            _managedBots.Clear();
            _botSpawnInfos.Clear();
            _botNameToSpawnInfo.Clear();
            
            Console.WriteLine($"[VTS 练习] 所有 {kickedCount} 个机器人已被踢出，位置信息已清理。");
            Console.WriteLine($"[VTS 练习] 被踢出的机器人: {string.Join(", ", botNames)}");
        }
        else
        {
            // 踢出指定队伍的BOT
            var teamBots = Utilities.GetPlayers()
                .Where(p => p.IsBot && p.IsValid && 
                       ((team == "ct" && p.TeamNum == 3) || (team == "t" && p.TeamNum == 2)))
                .ToList();
                
            foreach (var bot in teamBots)
            {
                string botName = bot.PlayerName;
                Server.ExecuteCommand($"bot_kick {botName}");
                
                // 清理相关记录
                _managedBots.Remove(bot);
                _botSpawnInfos.Remove(bot);
                _botNameToSpawnInfo.Remove(botName);
                
                kickedCount++;
            }
            
            Console.WriteLine($"[VTS 练习] {team.ToUpper()} 队伍的 {kickedCount} 个机器人已被踢出，位置信息已清理。");
        }
        
        return kickedCount;
    }

    public int GetManagedBotCount()
    {
        return _managedBots.Count;
    }

    private string GetPlayerWeapon(CCSPlayerController player)
    {
        try
        {
            var weaponServices = player.PlayerPawn?.Value?.WeaponServices;
            if (weaponServices?.ActiveWeapon?.Value != null)
            {
                var weapon = weaponServices.ActiveWeapon.Value;
                string weaponName = weapon.DesignerName ?? "weapon_knife";
                Console.WriteLine($"[VTS 练习] 检测到玩家武器: {weaponName}");
                return weaponName;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS 练习] 获取玩家武器失败: {ex.Message}");
        }
        
        return "weapon_ak47"; // 默认武器
    }

    private void ConfigureBot(CCSPlayerController bot, Vector position, QAngle rotation, bool crouch, string weaponName)
    {
        if (bot?.PlayerPawn?.Value == null)
            return;

        try
        {
            // 传送BOT到指定位置
            bot.PlayerPawn.Value.Teleport(position, rotation, new Vector(0, 0, 0));
            
            // 设置BOT为静止状态
            Server.NextFrame(() =>
            {
                if (bot.PlayerPawn?.Value != null)
                {
                    // 禁用BOT AI
                    Server.ExecuteCommand($"bot_stop 1");
                    
                    // 给BOT装备武器
                    if (!string.IsNullOrEmpty(weaponName) && weaponName != "weapon_knife")
                    {
                        Server.ExecuteCommand($"bot_give {bot.PlayerName} {weaponName}");
                    }
                    
                    // 如果需要蹲下
                    if (crouch)
                    {
                        // 设置蹲下标志
                        bot.PlayerPawn.Value.Flags |= (uint)PlayerFlags.FL_DUCKING;
                        
                        // 调整位置（稍微降低）
                        var crouchPos = new Vector(position.X, position.Y, position.Z - 18);
                        bot.PlayerPawn.Value.Teleport(crouchPos, rotation, new Vector(0, 0, 0));
                        
                        Console.WriteLine($"[VTS 练习] 机器人 {bot.PlayerName} 设置为蹲下");
                    }
                    
                    // 再次确保BOT静止
                    Server.NextFrame(() =>
                    {
                        Server.ExecuteCommand($"bot_stop 1");
                        Console.WriteLine($"[VTS 练习] 机器人 {bot.PlayerName} 配置完成，武器: {weaponName}");
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS 练习] 配置机器人失败: {ex.Message}");
        }
    }

    public void SpawnBotAtCoordinates(CCSPlayerController player, float x, float y, float z, string team = "none", bool crouch = false, string? customName = null)
    {
        var customPosition = new Vector(x, y, z);
        SpawnBot(player, team, crouch, customPosition, customName);
    }

    /// <summary>
    /// 处理BOT死亡事件，自动复活到原位置
    /// </summary>
    public void OnBotDeath(CCSPlayerController bot)
    {
        if (!bot.IsBot)
            return;

        string botName = bot.PlayerName;
        Console.WriteLine($"[VTS 练习] 机器人 {botName} 死亡，检查是否需要复活");

        // 优先通过名称查找位置信息
        if (_botNameToSpawnInfo.ContainsKey(botName))
        {
            var spawnInfo = _botNameToSpawnInfo[botName];
            Console.WriteLine($"[VTS 练习] 找到机器人 {botName} 的位置信息，准备复活到原位置");

            // 延迟复活，确保死亡处理完成
            Server.NextFrame(() =>
            {
                Server.NextFrame(() =>
                {
                    Server.NextFrame(() =>
                    {
                        RespawnBotByName(botName, spawnInfo);
                    });
                });
            });
        }
        else
        {
            Console.WriteLine($"[VTS 练习] 机器人 {botName} 不在管理列表中，跳过复活");
        }
    }

    private void RespawnBot(CCSPlayerController bot, BotSpawnInfo spawnInfo)
    {
        if (!bot.IsValid || bot.PlayerPawn?.Value == null)
            return;

        try
        {
            // 复活BOT
            bot.Respawn();
            
            // 等待复活完成后配置
            Server.NextFrame(() =>
            {
                if (bot.PlayerPawn?.Value != null)
                {
                    // 传送到原位置
                    bot.PlayerPawn.Value.Teleport(spawnInfo.Position, spawnInfo.Rotation, new Vector(0, 0, 0));
                    
                    // 恢复原配置
                    ConfigureBot(bot, spawnInfo.Position, spawnInfo.Rotation, spawnInfo.IsCrouched, spawnInfo.WeaponName);
                    
                    Console.WriteLine($"[VTS 练习] 机器人 {bot.PlayerName} 已复活到原位置");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS 练习] 复活机器人失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 通过名称查找BOT并复活到原位置
    /// </summary>
    private void RespawnBotByName(string botName, BotSpawnInfo spawnInfo)
    {
        try
        {
            // 查找当前存活的同名BOT（可能是重新连接的）
            var bot = Utilities.GetPlayers().FirstOrDefault(p => 
                p.IsBot && p.IsValid && p.PlayerName == botName);
            
            if (bot != null)
            {
                Console.WriteLine($"[VTS 练习] 找到机器人 {botName}，开始复活流程");
                
                // 更新管理列表，确保新的BOT对象被正确跟踪
                if (!_managedBots.Contains(bot))
                {
                    _managedBots.Add(bot);
                }
                _botSpawnInfos[bot] = spawnInfo;
                
                // 复活BOT
                bot.Respawn();
                
                // 等待复活完成后配置
                Server.NextFrame(() =>
                {
                    if (bot.IsValid && bot.PlayerPawn?.Value != null)
                    {
                        // 传送到原位置
                        bot.PlayerPawn.Value.Teleport(spawnInfo.Position, spawnInfo.Rotation, new Vector(0, 0, 0));
                        
                        // 恢复原配置
                        ConfigureBot(bot, spawnInfo.Position, spawnInfo.Rotation, spawnInfo.IsCrouched, spawnInfo.WeaponName);
                        
                        Console.WriteLine($"[VTS 练习] 机器人 {botName} 已复活到原位置 ({spawnInfo.Position.X:F1}, {spawnInfo.Position.Y:F1}, {spawnInfo.Position.Z:F1})");
                    }
                    else
                    {
                        Console.WriteLine($"[VTS 练习] 机器人 {botName} 复活后无效或 PlayerPawn 为空");
                    }
                });
            }
            else
            {
                Console.WriteLine($"[VTS 练习] 未找到可复活的机器人: {botName}");
                // 如果找不到BOT，尝试重新生成
                // 这种情况可能发生在BOT被意外移除时
                Console.WriteLine($"[VTS 练习] 尝试重新生成机器人: {botName}");
                RegenerateBot(botName, spawnInfo);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS 练习] 通过名称复活机器人失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 重新生成BOT（当原BOT丢失时）
    /// </summary>
    private void RegenerateBot(string botName, BotSpawnInfo spawnInfo)
    {
        try
        {
            Console.WriteLine($"[VTS 练习] 开始重新生成机器人: {botName}");
            
            // 根据队伍信息生成BOT
            if (spawnInfo.TeamName.ToLower() == "ct")
            {
                Server.ExecuteCommand("bot_add_ct");
            }
            else if (spawnInfo.TeamName.ToLower() == "t")
            {
                Server.ExecuteCommand("bot_add_t");
            }
            else
            {
                Server.ExecuteCommand("bot_add");
            }
            
            // 等待BOT生成完成
            Server.NextFrame(() =>
            {
                Server.NextFrame(() =>
                {
                    Server.NextFrame(() =>
                    {
                        // 查找新生成的BOT
                        var newBot = Utilities.GetPlayers()
                            .Where(p => p.IsBot && p.IsValid)
                            .OrderByDescending(p => p.UserId) // 按UserId排序，获取最新的
                            .FirstOrDefault();
                        
                        if (newBot?.PlayerPawn?.Value != null)
                        {
                            Console.WriteLine($"[VTS 练习] 重新生成的机器人: {newBot.PlayerName}");
                            
                            // 更新名称映射
                            _botNameToSpawnInfo.Remove(botName);
                            _botNameToSpawnInfo[newBot.PlayerName] = spawnInfo;
                            
                            // 配置新BOT
                            ConfigureBot(newBot, spawnInfo.Position, spawnInfo.Rotation, spawnInfo.IsCrouched, spawnInfo.WeaponName);
                            
                            // 添加到管理列表
                            _managedBots.Add(newBot);
                            _botSpawnInfos[newBot] = spawnInfo;
                            
                            Console.WriteLine($"[VTS 练习] 机器人 {newBot.PlayerName} 重新生成完成，位置: ({spawnInfo.Position.X:F1}, {spawnInfo.Position.Y:F1}, {spawnInfo.Position.Z:F1})");
                        }
                        else
                        {
                            Console.WriteLine($"[VTS 练习] 重新生成机器人失败");
                        }
                    });
                });
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS 练习] 重新生成机器人失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 移除BOT及其位置信息
    /// </summary>
    public void RemoveBot(CCSPlayerController bot)
    {
        _managedBots.Remove(bot);
        _botSpawnInfos.Remove(bot);
    }

    /// <summary>
    /// 处理BOT连接事件，检查是否需要恢复管理
    /// </summary>
    public void OnBotConnect(CCSPlayerController bot)
    {
        if (!bot.IsBot)
            return;

        string botName = bot.PlayerName;
        
        // 检查是否是已知的BOT
        if (_botNameToSpawnInfo.ContainsKey(botName))
        {
            var spawnInfo = _botNameToSpawnInfo[botName];
            Console.WriteLine($"[VTS 练习] 检测到已知机器人 {botName} 重新连接，恢复管理");
            
            // 添加到管理列表
            if (!_managedBots.Contains(bot))
            {
                _managedBots.Add(bot);
            }
            _botSpawnInfos[bot] = spawnInfo;
            
            // 延迟配置，确保BOT完全加载
            Server.NextFrame(() =>
            {
                Server.NextFrame(() =>
                {
                    if (bot.IsValid && bot.PlayerPawn?.Value != null)
                    {
                        ConfigureBot(bot, spawnInfo.Position, spawnInfo.Rotation, spawnInfo.IsCrouched, spawnInfo.WeaponName);
                        Console.WriteLine($"[VTS 练习] 机器人 {botName} 重新连接并配置完成");
                    }
                });
            });
        }
    }

    /// <summary>
    /// 检查并清理无效的BOT引用
    /// </summary>
    public void CleanupInvalidBots()
    {
        var invalidBots = _managedBots.Where(bot => !bot.IsValid).ToList();
        foreach (var bot in invalidBots)
        {
            _managedBots.Remove(bot);
            _botSpawnInfos.Remove(bot);
            Console.WriteLine($"[VTS 练习] 清理无效的BOT引用");
        }
    }
}