using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using VTSPrac.Database;

namespace VTSPrac.Localization;

public class LocalizationManager
{
    private readonly Dictionary<ulong, string> _playerLanguages = new();
    private readonly BasePlugin _plugin;
    private readonly DatabaseManager _databaseManager;

    public LocalizationManager(BasePlugin plugin, DatabaseManager databaseManager)
    {
        _plugin = plugin;
        _databaseManager = databaseManager;
    }

    public void SetPlayerLanguage(ulong steamId, string language)
    {
        _playerLanguages[steamId] = language;
    }

    public string GetPlayerLanguage(ulong steamId)
    {
        return _playerLanguages.GetValueOrDefault(steamId, "en");
    }

    public void RemovePlayer(ulong steamId)
    {
        _playerLanguages.Remove(steamId);
    }

    public string GetLocalizedText(CCSPlayerController player, string key, params object[] args)
    {
        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId.HasValue && _playerLanguages.ContainsKey(steamId.Value))
        {
            var playerLang = _playerLanguages[steamId.Value];
            if (playerLang == "zh")
            {
                return GetChineseTranslation(key, args);
            }
        }
        return _plugin.Localizer[key, args];
    }

    private string GetChineseTranslation(string key, params object[] args)
    {
        var translations = new Dictionary<string, string>
        {
            ["stats.title"] = "玩家统计信息",
            ["stats.currentsession.minutes"] = "本次游戏时间：{0} 分钟",
            ["stats.currentsession.hours"] = "本次游戏时间：{0}小时 {1}分钟",
            ["stats.totalplaytime.minutes"] = "总游戏时间：{0} 分钟",
            ["stats.totalplaytime.hours"] = "总游戏时间：{0}小时 {1}分钟",
            ["stats.totalsessions"] = "总游戏次数：{0}",
            ["rethrow.not_found"] = "❌ 没有找到第 {0} 个投掷记录",
            ["rethrow.success"] = "✅ 重投第 {0} 个道具: {1}",
            ["player.welcome"] = "欢迎 {0}！",
            ["player.nodata"] = "未找到玩家数据",
            ["commands.hello"] = "你好 {0}！",
            ["commands.help"] = "可用命令列表",
            ["playtime.current.hours"] = "当前会话时间：{0}小时 {1}分钟",
            ["playtime.current.minutes"] = "当前会话时间：{0}分钟",
            ["playtime.total.hours"] = "总游戏时间：{0}小时 {1}分钟",
            ["playtime.total.minutes"] = "总游戏时间：{0}分钟"
        };

        if (translations.TryGetValue(key, out var translation))
        {
            return string.Format(translation, args);
        }
        return key; // Fallback
    }

    public bool IsValidLanguage(string languageCode)
    {
        return languageCode == "en" || languageCode == "zh";
    }

    public async void UpdatePlayerLanguageAsync(ulong steamId, string language)
    {
        _playerLanguages[steamId] = language;
        await _databaseManager.UpdatePlayerLanguageAsync(steamId, language);
    }

    public bool HasPlayerLanguageInMemory(ulong steamId)
    {
        return _playerLanguages.ContainsKey(steamId);
    }
}
