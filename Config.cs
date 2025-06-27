using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace VTSPrac;

public class PracticeConfig : BasePluginConfig
{
    [JsonPropertyName("bot_prefix")]
    public string BotPrefix { get; set; } = "PracBot";
    
    [JsonPropertyName("max_bots")]
    public int MaxBots { get; set; } = 32;
    
    [JsonPropertyName("allow_crouch_bots")]
    public bool AllowCrouchBots { get; set; } = true;
    
    [JsonPropertyName("bot_difficulty")]
    public int BotDifficulty { get; set; } = 1; // 0=简单, 1=普通, 2=困难, 3=专家
}