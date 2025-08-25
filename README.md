# VTS 跑图练习插件 (VTSPrac)
# 项目在进行完整重写

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-1.0.315-green.svg)](https://github.com/roflmuffin/CounterStrikeSharp)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

VTS 跑图练习插件是一个专为 Counter-Strike 2 设计的练习插件，基于 CounterStrikeSharp 框架开发。该插件为玩家提供了全面的跑图练习工具，包括BOT管理、道具投掷练习、传送功能等。

## 这个项目是我在几个小时内完成的，所以问题可能会很多！！！

制作这个插件是因为某美和某E的跑图功能欠缺且道具点位过时（个人观点）。注释什么的使用 Claude Sonnet 4 生成，我也不知道对不对

## 🎯 主要功能

### 🤖 BOT管理系统

- **智能BOT生成**: 支持指定位置、队伍、姿态生成BOT
- **BOT队伍管理**: 可指定BOT加入CT或T队伍
- **BOT姿态控制**: 支持蹲下、站立等不同姿态
- **BOT清理功能**: 一键清理所有BOT或指定BOT

### 💣 道具投掷练习

- **道具轨迹记录**: 自动记录道具投掷轨迹和参数
- **道具重放系统**: 可重放之前的道具投掷
- **多种道具支持**: 烟雾弹、闪光弹、手雷、火焰弹、诱饵弹
- **道具轨迹可视化**: 显示道具飞行轨迹和爆炸点

### 📍 传送系统

- **位置保存**: 保存和命名自定义传送点
- **快速传送**: 快速传送到保存的位置
- **出生点传送**: 传送到官方出生点位置
- **观察者模式**: 支持观察其他玩家

### ⚙️ 游戏模式管理

- **上帝模式**: 玩家无敌模式
- **观察者模式**: 观察其他玩家练习
- **游戏设置**: 自定义练习环境设置

### 📊 玩家数据统计

- **练习时间统计**: 记录玩家练习时长
- **个人设置**: 个性化设置保存
- **数据持久化**: 数据自动保存和加载

## TODO

### 道具投掷预设

### 计时器

### 插件功能设置

### 重载插件

### 地图切换

### 道具演示

...

## 🚀 快速开始

### 环境要求

- Counter-Strike 2 服务器
- CounterStrikeSharp 1.0.315+
- .NET 8.0 Runtime

### 安装步骤

1. **下载插件**

   ```bash
   git clone https://github.com/your-username/VTSPrac.git
   cd VTSPrac
   ```

2. **编译插件**

   ```bash
   dotnet build -c Release
   ```

3. **部署插件**
   - 将编译后的 `VTSPrac.dll` 复制到服务器的插件目录
   - 复制 `VTSPrac.json` 配置文件到相应目录
   - 复制 `prac.cfg` 到服务器配置目录

4. **重启服务器**

   ```bash
   # 重启CS2服务器以加载插件
   ```

## ⚙️ 配置说明

### 主配置文件 (VTSPrac.json)

```json
{
  "bot_prefix": "PracBot",      // BOT名称前缀
  "max_bots": 32,               // 最大BOT数量
  "allow_crouch_bots": true,    // 是否允许蹲下BOT
  "bot_difficulty": 1           // BOT难度 (0=简单, 1=普通, 2=困难, 3=专家)
}
```

### 游戏配置文件 (prac.cfg)

包含练习模式的服务器设置，如回合时间、经济设置等。

## 🎮 使用方法

详细的使用方法请参考 [GUIDE.md](GUIDE.md)

### 基本命令示例

- `.bot spawn mybot side ct at 100 200 300` - 在指定位置生成CT BOT
- `.tp save spawn` - 保存当前位置为传送点
- `.smoke` - 投掷烟雾弹练习
- `.god` - 开启/关闭上帝模式

## 📁 项目结构

```text
VTSPrac/
├── VTSPrac.cs              # 主插件类
├── Config.cs               # 配置管理
├── BotManager.cs           # BOT管理系统
├── BotCommands.cs          # BOT命令处理
├── SpawnManager.cs         # 出生点管理
├── TeleportManager.cs      # 传送系统
├── GameModeManager.cs      # 游戏模式管理
├── Grenade.cs              # 道具投掷系统
├── PlayerSettingsManager.cs # 玩家设置管理
├── VTSPrac.json           # 配置文件
├── prac.cfg               # 游戏配置
└── admins.cfg.example     # 管理员配置示例
```

## 🔧 开发

### 构建要求

- Visual Studio 2022 或 VS Code
- .NET 8.0 SDK
- CounterStrikeSharp API

### 构建命令

```bash
# 调试版本
dotnet build

# 发布版本
dotnet build -c Release
```

## 📝 变更日志

### v1.0.0

- 初始版本发布
- 完整的BOT管理系统
- 道具投掷练习功能
- 传送系统
- 基础游戏模式管理

## 🤝 贡献

欢迎提交 Issues 和 Pull Requests！

1. Fork 项目
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情

## 👥 作者

- **VTSDT Guangyun Zhou** - *初始开发*

## 🙏 致谢

- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) - 优秀的CS2插件框架
- [MatchZy](https://github.com/shobhit-pathak/MatchZy) - 提供了一些指令灵感
- [CS-advent](https://space.bilibili.com/3546687211572101) - 特殊致谢 感谢九爷在我凌晨写代码的时候提供了一些乐趣

## 📞 支持

这个项目是我在几个小时内完成的，所以问题可能会很多。如果你遇到问题或有建议，请：

1. 查看 [GUIDE.md](GUIDE.md) 获取详细使用说明
2. 在 [Issues](https://github.com/your-username/VTSPrac/issues) 中报告问题
3. 联系开发者获取技术支持[周讷](https://space.bilibili.com/1999540880)

---

⭐ 如果这个项目对你有帮助，请给个Star支持一下！