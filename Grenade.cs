using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace VTSPrac;

public enum GrenadeType
{
    Unknown = 0,
    Smoke = 1,      // 烟雾弹
    Flashbang = 2,  // 闪光弹
    Incendiary = 3, // 火焰弹
    HEGrenade = 4,  // 手雷
    Decoy = 5       // 诱饵弹
}

public class GrenadeThrowRecord
{
    public Vector ThrowPosition { get; set; }  // 投掷位置
    public QAngle ThrowAngles { get; set; }    // 投掷角度
    public Vector ThrowVelocity { get; set; }  // 投掷速度
    public Vector PlayerPosition { get; set; } // 玩家位置
    public QAngle PlayerAngles { get; set; }   // 玩家角度
    public GrenadeType GrenadeType { get; set; } // 道具类型
    public string GrenadeName { get; set; }    // 道具名称
    public DateTime ThrowTime { get; set; }    // 投掷时间
    public int Index { get; set; }             // 索引编号

    public GrenadeThrowRecord(Vector throwPos, QAngle throwAngles, Vector throwVel, Vector playerPos, QAngle playerAngles, GrenadeType type, string name, int index)
    {
        ThrowPosition = throwPos;
        ThrowAngles = throwAngles;
        ThrowVelocity = throwVel;
        PlayerPosition = playerPos;
        PlayerAngles = playerAngles;
        GrenadeType = type;
        GrenadeName = name;
        ThrowTime = DateTime.Now;
        Index = index;
    }

    /// <summary>
    /// 传送玩家到投掷位置
    /// </summary>
    public void TeleportToThrowPosition(CCSPlayerController player)
    {
        if (player.PlayerPawn?.Value != null)
        {
            player.PlayerPawn.Value.Teleport(PlayerPosition, PlayerAngles, new Vector(0, 0, 0));
        }
    }

    /// <summary>
    /// 直接投掷道具
    /// </summary>
    public void ThrowGrenade(CCSPlayerController player)
    {
        // 根据道具类型创建投掷物
        switch (GrenadeType)
        {
            case GrenadeType.Smoke:
                CreateSmokeGrenade(player);
                break;
            case GrenadeType.Flashbang:
                CreateFlashbang(player);
                break;
            case GrenadeType.Incendiary:
                CreateIncendiary(player);
                break;
            case GrenadeType.HEGrenade:
                CreateHEGrenade(player);
                break;
            case GrenadeType.Decoy:
                CreateDecoy(player);
                break;
        }
    }

    /// <summary>
    /// 延迟投掷道具
    /// </summary>
    public void ThrowGrenadeWithDelay(CCSPlayerController player, int delayMs)
    {
        // 使用定时器延迟投掷
        var timer = new System.Timers.Timer(delayMs);
        timer.Elapsed += (sender, e) =>
        {
            timer.Dispose();
            Server.NextFrame(() =>
            {
                if (player.IsValid)
                {
                    ThrowGrenade(player);
                }
            });
        };
        timer.Start();
    }

    private void CreateSmokeGrenade(CCSPlayerController player)
    {
        var entity = Utilities.CreateEntityByName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
        if (entity == null) return;
        
        SetupGrenadeProjectile(entity, player);
    }

    private void CreateFlashbang(CCSPlayerController player)
    {
        var entity = Utilities.CreateEntityByName<CBaseCSGrenadeProjectile>("flashbang_projectile");
        if (entity == null) return;
        
        SetupGrenadeProjectile(entity, player);
    }

    private void CreateIncendiary(CCSPlayerController player)
    {
        var entity = Utilities.CreateEntityByName<CBaseCSGrenadeProjectile>("molotov_projectile");
        if (entity == null) return;
        
        entity.SetModel("weapons/models/grenade/incendiary/weapon_incendiarygrenade.vmdl");
        SetupGrenadeProjectile(entity, player);
    }

    private void CreateHEGrenade(CCSPlayerController player)
    {
        var entity = Utilities.CreateEntityByName<CBaseCSGrenadeProjectile>("hegrenade_projectile");
        if (entity == null) return;
        
        SetupGrenadeProjectile(entity, player);
    }

    private void CreateDecoy(CCSPlayerController player)
    {
        var entity = Utilities.CreateEntityByName<CBaseCSGrenadeProjectile>("decoy_projectile");
        if (entity == null) return;
        
        SetupGrenadeProjectile(entity, player);
    }

    private void SetupGrenadeProjectile(CBaseCSGrenadeProjectile entity, CCSPlayerController player)
    {
        if (entity == null || player.PlayerPawn?.Value == null) return;

        // 设置基本属性
        entity.Elasticity = 0.33f;
        entity.IsLive = false;
        entity.DmgRadius = 350.0f;
        entity.Damage = 99.0f;
        
        // 设置位置和速度
        entity.InitialPosition.X = ThrowPosition.X;
        entity.InitialPosition.Y = ThrowPosition.Y;
        entity.InitialPosition.Z = ThrowPosition.Z;
        entity.InitialVelocity.X = ThrowVelocity.X;
        entity.InitialVelocity.Y = ThrowVelocity.Y;
        entity.InitialVelocity.Z = ThrowVelocity.Z;
        
        // 传送到位置并设置角度和速度
        entity.Teleport(ThrowPosition, ThrowAngles, ThrowVelocity);
        
        // 生成实体
        entity.DispatchSpawn();
        
        // 设置标识和所有者
        entity.Globalname = "custom";
        entity.TeamNum = player.TeamNum;
        entity.Thrower.Raw = player.PlayerPawn.Raw;
        entity.OriginalThrower.Raw = player.PlayerPawn.Raw;
        entity.OwnerEntity.Raw = player.PlayerPawn.Raw;
        
        // 激活投掷物
        entity.AcceptInput("FireUser1", player, player);
        entity.AcceptInput("InitializeSpawnFromWorld");
    }
}

public class PlayerGrenadeManager
{
    private readonly Dictionary<ulong, List<GrenadeThrowRecord>> _playerGrenadeHistory = new();
    private static readonly string ChatPrefix = $"{ChatColors.Red}[VTS Prac]{ChatColors.Default}";

    public void AddGrenadeThrow(CCSPlayerController player, Vector throwPos, QAngle throwAngles, Vector throwVel, Vector playerPos, QAngle playerAngles, string grenadeName)
    {
        if (player?.SteamID == null) return;

        Console.WriteLine($"[VTS Prac Debug] 尝试添加道具记录 - 玩家: {player.PlayerName}, 道具名称: {grenadeName}");

        var grenadeType = GetGrenadeTypeFromName(grenadeName);
        if (grenadeType == GrenadeType.Unknown) 
        {
            Console.WriteLine($"[VTS Prac Debug] 未识别的道具类型: {grenadeName}");
            return;
        }

        if (!_playerGrenadeHistory.ContainsKey(player.SteamID))
        {
            _playerGrenadeHistory[player.SteamID] = new List<GrenadeThrowRecord>();
        }

        var history = _playerGrenadeHistory[player.SteamID];
        var index = 1; // 新道具总是索引1
        var record = new GrenadeThrowRecord(throwPos, throwAngles, throwVel, playerPos, playerAngles, grenadeType, grenadeName, index);
        
        // 在开头插入新记录，保持索引1为最新
        history.Insert(0, record);
        
        // 重新编号：索引1是最新的，索引递增
        for (int i = 0; i < history.Count; i++)
        {
            history[i].Index = i + 1;
        }
        
        // 限制历史记录数量，保持最近的50个
        if (history.Count > 50)
        {
            history.RemoveAt(history.Count - 1); // 移除最老的记录
        }

        Console.WriteLine($"[VTS Prac] 玩家 {player.PlayerName} 投掷了 {grenadeName}，记录索引: {index}，类型: {grenadeType}");
    }

    public List<GrenadeThrowRecord> GetPlayerHistory(CCSPlayerController player)
    {
        if (player?.SteamID == null) return new List<GrenadeThrowRecord>();
        
        return _playerGrenadeHistory.ContainsKey(player.SteamID) 
            ? _playerGrenadeHistory[player.SteamID] 
            : new List<GrenadeThrowRecord>();
    }

    public GrenadeThrowRecord? GetLastGrenade(CCSPlayerController player)
    {
        var history = GetPlayerHistory(player);
        return history.FirstOrDefault(); // 第一个就是最新的
    }

    public GrenadeThrowRecord? GetGrenadeByIndex(CCSPlayerController player, int index)
    {
        var history = GetPlayerHistory(player);
        return history.FirstOrDefault(g => g.Index == index);
    }

    public GrenadeThrowRecord? GetGrenadeByType(CCSPlayerController player, GrenadeType type)
    {
        var history = GetPlayerHistory(player);
        return history.FirstOrDefault(g => g.GrenadeType == type); // 第一个就是最新的
    }

    public void ClearPlayerHistory(CCSPlayerController player)
    {
        if (player?.SteamID == null) return;
        
        if (_playerGrenadeHistory.ContainsKey(player.SteamID))
        {
            _playerGrenadeHistory[player.SteamID].Clear();
        }
    }

    public GrenadeType GetGrenadeTypeFromName(string grenadeName)
    {
        var lowerName = grenadeName.ToLower();
        return lowerName switch
        {
            "weapon_smokegrenade" => GrenadeType.Smoke,
            "smokegrenade" => GrenadeType.Smoke,
            "smoke" => GrenadeType.Smoke,
            
            "weapon_flashbang" => GrenadeType.Flashbang,
            "flashbang" => GrenadeType.Flashbang,
            "flash" => GrenadeType.Flashbang,
            
            "weapon_incgrenade" => GrenadeType.Incendiary,
            "weapon_molotov" => GrenadeType.Incendiary,
            "incgrenade" => GrenadeType.Incendiary,
            "molotov" => GrenadeType.Incendiary,
            "incendiary" => GrenadeType.Incendiary,
            
            "weapon_hegrenade" => GrenadeType.HEGrenade,
            "hegrenade" => GrenadeType.HEGrenade,
            "he" => GrenadeType.HEGrenade,
            
            "weapon_decoy" => GrenadeType.Decoy,
            "decoy" => GrenadeType.Decoy,
            
            _ => GrenadeType.Unknown
        };
    }

    public string GetGrenadeDisplayName(GrenadeType type)
    {
        return type switch
        {
            GrenadeType.Smoke => "烟雾弹",
            GrenadeType.Flashbang => "闪光弹",
            GrenadeType.Incendiary => "火焰弹",
            GrenadeType.HEGrenade => "手雷",
            GrenadeType.Decoy => "诱饵弹",
            _ => "未知道具"
        };
    }

    public string GetGrenadeWeaponName(GrenadeType type)
    {
        return type switch
        {
            GrenadeType.Smoke => "weapon_smokegrenade",
            GrenadeType.Flashbang => "weapon_flashbang",
            GrenadeType.Incendiary => "weapon_incgrenade",
            GrenadeType.HEGrenade => "weapon_hegrenade",
            GrenadeType.Decoy => "weapon_decoy",
            _ => ""
        };
    }

    /// <summary>
    /// 显示玩家道具投掷历史
    /// </summary>
    public void ShowGrenadeHistory(CCSPlayerController player, int count = 10)
    {
        var history = GetPlayerHistory(player);
        if (history.Count == 0)
        {
            player.PrintToChat($"{ChatPrefix} 您还没有道具投掷记录。");
            return;
        }

        player.PrintToChat($"{ChatPrefix} {ChatColors.Yellow}您的道具投掷历史（最近{Math.Min(count, history.Count)}条）：");
        
        // 取最新的count条记录
        var recentHistory = history.Take(count).ToList();
        
        for (int i = 0; i < recentHistory.Count; i++)
        {
            var record = recentHistory[i];
            var timeStr = record.ThrowTime.ToString("HH:mm:ss");
            var grenadeDisplayName = GetGrenadeDisplayName(record.GrenadeType);
            
            player.PrintToChat($"{ChatPrefix} {ChatColors.Green}[{record.Index}]{ChatColors.Default} {grenadeDisplayName} - {timeStr}");
        }
    }
}
