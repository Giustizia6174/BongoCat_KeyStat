# Bongo Cat Key Stats · 按键统计

给 Steam 版 [Bongo Cat](https://store.steampowered.com/app/3419430) 增加键盘按键统计：游戏在后台（窗口失焦）时也会统计，数据跨启动累计，可在游戏设置里查看，并导出为离线 HTML 图表。

*Adds keyboard key statistics to Steam's Bongo Cat: counts keys even while the game is unfocused, persists across launches, shows a tab in the game's settings and exports an offline HTML report.*

## 特性

- 统计**键盘**每个键的点击次数与占比（按下沿计数，按住不放只算 1 次；不含鼠标和手柄）。
- **会话**与**累计**两种口径；另有按日、按小时的汇总。
- 设置窗口里新增「按键统计」页签：总数、今日、Top 5，以及带键盘热力图的完整面板。
- 导出单文件离线 `report.html`：热力图、Top 20、分组占比、30 天趋势、时段分布、可排序明细。
- 隐私：只保存各键的**次数**，不记录按键顺序和输入内容，不联网。数据是明文 JSON：`<游戏目录>\KeyStats\keystats.json`。

## 安装

1. 到 [Releases](../../releases) 下载 `BongoKeyStats-x.y.z.zip` 并解压到任意位置。
2. 需要 [.NET 9 运行时](https://dotnet.microsoft.com/download/dotnet/9.0)（补丁工具使用）。
3. 退出游戏，双击 `patch.bat`。会自动在 Steam 库中找到游戏；找不到时把游戏文件夹拖到 `patch.bat` 上。
4. 启动游戏，打开设置，找到「按键统计」页签。

卸载：双击 `restore.bat`（还原打补丁前的 DLL，删除模块；`KeyStats\` 里的数据不会被删除）。
**游戏更新后**：补丁可能被覆盖，重新运行 `patch.bat` 即可。

## 与其他 Mod 的关系

补丁只在 `Assembly-CSharp.dll` 的 `BongoCat.OSSpecific.GlobalKeyHook.Awake()` 开头插入一处受 `try/catch` 保护的调用，并把 `BongoKeyStats.dll` 放进 `Managed` 目录：

| 你的 DLL | 行为 |
|---|---|
| 官方原版 | 只增加按键统计 |
| 已被其他 Mod 修改过 | 同样只增加按键统计，其他 Mod 的代码保持原样 |

已在官方版与一个第三方 Mod 版上做过对比验证：补丁前后，**除 `GlobalKeyHook.Awake` 外所有方法的 IL 完全一致**。补丁顺序：先放入其他 Mod 的 DLL，最后运行本补丁。

## 从源码构建

需要 .NET SDK 9 和游戏安装目录（编译时引用其中的 Unity 程序集，仓库不含任何游戏文件）。

```powershell
$env:BONGOCAT_MANAGED = 'D:\SteamLibrary\steamapps\common\BongoCat\BongoCat_Data\Managed'
.\scripts\build.ps1      # 单元测试 + 编译 + 打包 -> release\BongoKeyStats-<VERSION>.zip
```

目录：`src/` 模块 · `Patcher/` 补丁工具（Mono.Cecil） · `Tests/` 单元测试 · `package/` 发布包内的脚本 · `scripts/` 构建脚本 · `docs/MAINTENANCE.md` 原理、游戏更新后的维护与排错。

## 已知限制

- 仅 Windows x64；已在 Unity 6000.2.8f1 / Mono 版本的游戏上验证。游戏更新若改动了 `GlobalKeyHook` 或设置窗口的节点名，补丁/页签可能需要调整，见维护文档。
- 游戏内完整面板为英文（避免游戏字体缺字）。
- 请自行承担使用风险；使用任何修改游戏文件的工具之前建议备份。本项目与 Bongo Cat 开发者及任何第三方 Mod 作者无关。

## 许可

MIT，见 [LICENSE](LICENSE)；第三方组件见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
