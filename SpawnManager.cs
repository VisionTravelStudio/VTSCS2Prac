using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;
using System.Linq;

namespace VTSPrac;

/// <summary>
/// 出生点类型
/// </summary>
public enum SpawnType
{
    Best,
    Worst
}

/// <summary>
/// 出生点信息
/// </summary>
public class SpawnPoint
{
    public Vector Position { get; set; }
    public QAngle Angles { get; set; }
    public CsTeam Team { get; set; }
    public int Index { get; set; }
    public bool IsEnabled { get; set; } = true;

    public SpawnPoint(Vector position, QAngle angles, CsTeam team, int index)
    {
        Position = position;
        Angles = angles;
        Team = team;
        Index = index;
    }
}

/// <summary>
/// 出生点管理器
/// </summary>
public class SpawnManager
{
    private readonly Dictionary<CsTeam, List<SpawnPoint>> _spawnPoints = new();
    private readonly Random _random = new();
    private string _currentMapName = "";

    public void Initialize()
    {
        // 获取当前地图名称
        _currentMapName = Server.MapName;
        LoadSpawnPoints();
    }

    /// <summary>
    /// 加载地图出生点
    /// </summary>
    private void LoadSpawnPoints()
    {
        _spawnPoints.Clear();
        _spawnPoints[CsTeam.CounterTerrorist] = new List<SpawnPoint>();
        _spawnPoints[CsTeam.Terrorist] = new List<SpawnPoint>();

        try
        {
            // 查找 CT 出生点
            var ctSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoPlayerCounterterrorist>("info_player_counterterrorist");
            int ctIndex = 1;
            foreach (var spawn in ctSpawns)
            {
                if (spawn?.AbsOrigin != null && spawn.AbsRotation != null)
                {
                    var spawnPoint = new SpawnPoint(
                        new Vector(spawn.AbsOrigin.X, spawn.AbsOrigin.Y, spawn.AbsOrigin.Z),
                        new QAngle(spawn.AbsRotation.X, spawn.AbsRotation.Y, spawn.AbsRotation.Z),
                        CsTeam.CounterTerrorist,
                        ctIndex++
                    );
                    _spawnPoints[CsTeam.CounterTerrorist].Add(spawnPoint);
                }
            }

            // 查找 T 出生点
            var tSpawns = Utilities.FindAllEntitiesByDesignerName<CInfoPlayerTerrorist>("info_player_terrorist");
            int tIndex = 1;
            foreach (var spawn in tSpawns)
            {
                if (spawn?.AbsOrigin != null && spawn.AbsRotation != null)
                {
                    var spawnPoint = new SpawnPoint(
                        new Vector(spawn.AbsOrigin.X, spawn.AbsOrigin.Y, spawn.AbsOrigin.Z),
                        new QAngle(spawn.AbsRotation.X, spawn.AbsRotation.Y, spawn.AbsRotation.Z),
                        CsTeam.Terrorist,
                        tIndex++
                    );
                    _spawnPoints[CsTeam.Terrorist].Add(spawnPoint);
                }
            }

            Console.WriteLine($"[VTS Prac] 已加载 {_spawnPoints[CsTeam.CounterTerrorist].Count} 个 CT 出生点");
            Console.WriteLine($"[VTS Prac] 已加载 {_spawnPoints[CsTeam.Terrorist].Count} 个 T 出生点");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VTS Prac] 加载出生点时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取指定队伍的出生点
    /// </summary>
    public List<SpawnPoint> GetSpawnPoints(CsTeam team)
    {
        return _spawnPoints.GetValueOrDefault(team, new List<SpawnPoint>());
    }

    /// <summary>
    /// 根据索引获取出生点
    /// </summary>
    public SpawnPoint? GetSpawnPointByIndex(CsTeam team, int index)
    {
        var spawns = GetSpawnPoints(team);
        return spawns.FirstOrDefault(sp => sp.Index == index);
    }

    /// <summary>
    /// 获取随机出生点
    /// </summary>
    public SpawnPoint? GetRandomSpawnPoint(CsTeam team)
    {
        var spawns = GetSpawnPoints(team).Where(sp => sp.IsEnabled).ToList();
        if (spawns.Count == 0) return null;
        
        return spawns[_random.Next(spawns.Count)];
    }

    /// <summary>
    /// 获取最佳/最差出生点（基于距离敌方出生点的距离）
    /// </summary>
    public SpawnPoint? GetSpawnPointByType(CsTeam team, SpawnType type)
    {
        var spawns = GetSpawnPoints(team).Where(sp => sp.IsEnabled).ToList();
        if (spawns.Count == 0) return null;

        var enemyTeam = team == CsTeam.CounterTerrorist ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
        var enemySpawns = GetSpawnPoints(enemyTeam);
        
        if (enemySpawns.Count == 0) return spawns.First();

        // 计算每个出生点到敌方出生点的最短距离
        var spawnDistances = spawns.Select(spawn =>
        {
            var minDistance = enemySpawns.Min(enemy => 
                Math.Sqrt(Math.Pow(spawn.Position.X - enemy.Position.X, 2) + 
                         Math.Pow(spawn.Position.Y - enemy.Position.Y, 2) + 
                         Math.Pow(spawn.Position.Z - enemy.Position.Z, 2)));
            return new { Spawn = spawn, Distance = minDistance };
        }).ToList();

        // 根据类型返回最佳或最差出生点
        return type == SpawnType.Best 
            ? spawnDistances.OrderByDescending(x => x.Distance).First().Spawn
            : spawnDistances.OrderBy(x => x.Distance).First().Spawn;
    }

    /// <summary>
    /// 传送玩家到出生点
    /// </summary>
    public bool TeleportToSpawn(CCSPlayerController player, SpawnPoint spawnPoint)
    {
        if (player?.PlayerPawn?.Value == null) return false;

        try
        {
            var playerPawn = player.PlayerPawn.Value;
            
            // 确保玩家在地面上，稍微抬高一点避免卡在地面里
            var adjustedPosition = new Vector(
                spawnPoint.Position.X, 
                spawnPoint.Position.Y, 
                spawnPoint.Position.Z + 5.0f
            );
            
            // 传送玩家
            playerPawn.Teleport(adjustedPosition, spawnPoint.Angles, new Vector(0, 0, 0));
            
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
                    
                    // 确保玩家站立状态
                    if (playerPawn.MovementServices is CCSPlayer_MovementServices ccsMovement)
                    {
                        // 重置移动状态
                        ccsMovement.DuckAmount = 0.0f;
                        ccsMovement.DuckSpeed = 0.0f;
                    }
                    
                    // 更新玩家的网络状态
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
            Console.WriteLine($"[VTS Prac] 传送玩家到出生点时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取玩家当前队伍
    /// </summary>
    public CsTeam GetPlayerTeam(CCSPlayerController player)
    {
        return (CsTeam)player.TeamNum;
    }

    /// <summary>
    /// 获取队伍显示名称
    /// </summary>
    public string GetTeamDisplayName(CsTeam team)
    {
        return team switch
        {
            CsTeam.CounterTerrorist => "CT",
            CsTeam.Terrorist => "T",
            _ => "未知"
        };
    }
}
