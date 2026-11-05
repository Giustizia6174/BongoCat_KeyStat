# Bongo Cat「按键统计」Mod 维护文档

> 基准版本：Unity 6000.2.8f1 / Mono；最后验证：2026-11-04（游戏内已确认可用）。
> 目标：游戏更新后，用最少的步骤和排查成本重新装上同样的功能。

---

## 1. 一眼看懂：这个 Mod 是什么

- **独立模块 `BongoKeyStats.dll`**（源码 `D:\BongoKeyStatsDev\src`），放进 `BongoCat_Data\Managed\`。
- **只对游戏 DLL 做一处改动**：`Assembly-CSharp.dll` 里 `BongoCat.OSSpecific.GlobalKeyHook.Awake()` 的开头插入
  `try { BongoKeyStats.Bootstrap.Init(); } catch (Exception) { }`。
  补丁工具 `Patcher`（Mono.Cecil）自动完成，可重复运行，自动备份。
- **不依赖** Mod 作者（XV40）的混淆代码，也不依赖游戏的 `OnKeyPressed`（它只传按键个数，不带键码）。
- 计数：模块自己开后台线程，每 10ms 轮询 `GetAsyncKeyState`（VK 8–254，排除鼠标和泛型 Shift/Ctrl/Alt），松开→按下算一次，失焦也统计。
- 数据：`<游戏目录>\KeyStats\keystats.json`（+`.bak`、`report.html`），30 秒保存一次、退出时保存；游戏目录不可写时退回 `LocalLow`。
- 界面：设置窗口新增「按键统计」页签（克隆 `OtherSettings` 页签的外观），页签页面可打开完整面板（热力图 + Top10）和导出 HTML。

## 2. 游戏更新后的标准流程（正常情况 2 分钟）

1. Steam 更新通常会把 `Assembly-CSharp.dll` 覆盖为官方原版，补丁随之丢失（`BongoKeyStats.dll` 可能还在）。
2. 如果同时使用其他修改过 `Assembly-CSharp.dll` 的 Mod：先放入它的 DLL，**再**运行补丁（补丁必须是最后一步，否则会被覆盖）。补丁对官方版和被其他 Mod 改过的版本都适用，只改 `GlobalKeyHook.Awake` 一处。
3. 退出游戏，双击 `patch.bat`（自动找到 Steam 安装目录；已打过补丁时只刷新 `BongoKeyStats.dll`）。
4. 看窗口输出：
   - `Patched ... Installed BongoKeyStats.dll` → 成功，启动游戏验证；
   - `ERROR: type ... not found` / `method ... not found` → 游戏改了注入点，见第 4 节；
   - `uses members missing from the game's runtime` → 见第 5 节。
5. 验证（任意一项失败见第 6 节）：
   - `Player.log`（`%USERPROFILE%\AppData\LocalLow\Irox Games\BongoCat\Player.log`）含 `[BongoKeyStats] loaded data dir` 和 `settings tab added`；
   - 设置窗口 XV40 Mod 下面有「按键统计」；
   - 在后台按 A 10 次，页签里 A=10；按住不放只计 1 次。
6. 回滚：`restore.bat`（恢复 `Assembly-CSharp.dll.keystats-original`，删除模块；数据不动）。

> `Assembly-CSharp.dll.keystats-original` 每次打补丁前都会被刷新为**当时未打补丁的 DLL**，所以它总是最近一份可回滚的版本。

## 3. 文件清单

| 路径 | 作用 |
|---|---|
| `src/Bootstrap.cs` | 入口 `Init()`；创建计数线程、常驻对象；保存/导出；任何异常都只停用模块 |
| `src/KeyPoller.cs` | `GetAsyncKeyState` 轮询 + 边沿检测 + 定时保存回调 |
| `src/KeyCounter.cs` | 会话/累计/按日/按小时计数，JSON 序列化与加载（线程安全） |
| `src/StatsStore.cs` | 原子保存（tmp→校验→bak→覆盖）、损坏时回退 `.bak` |
| `src/Json.cs` | 极简 JSON 读写（不依赖任何库） |
| `src/KeyNames.cs` | VK→显示名、分组、键盘布局坐标（热力图） |
| `src/SettingsTab.cs` | 设置窗口页签（克隆页签按钮 + 页面） |
| `src/StatsUi.cs` | 完整面板（uGUI + TMP，叠加在独立 Overlay Canvas） |
| `src/HtmlReportWriter.cs` + `src/report_template.html` | HTML 报告；模板经 `scripts/gen-template.ps1` 生成 `src/ReportTemplate.cs`（**改模板后必须重新运行**） |
| `Patcher/Program.cs` | 补丁工具：定位 `Awake`、插入 try/catch 调用、校验引用、备份、`--check`、`--restore` |
| `Tests/` | 数据层与报告的单元测试（`dotnet run --project Tests -c Release`） |
| `package/` | 发布包里的 `patch.bat`、`restore.bat`、`README.txt`（`scripts/build.ps1` 会把它们和构建产物打成 zip） |

重新构建（需要 .NET SDK 9 和游戏安装目录，用于引用 Unity 程序集）：
```powershell
$env:BONGOCAT_MANAGED = 'D:\SteamLibrary\steamapps\common\BongoCat\BongoCat_Data\Managed'
.\scripts\build.ps1        # 生成模板、跑测试、编译、打包 -> release\BongoKeyStats-<VERSION>.zip
```
改过 `src/report_template.html` 后 `build.ps1` 会自动重新生成 `src/ReportTemplate.cs`。

## 4. 与游戏版本耦合的点（更新后只需检查这些）

| 依赖项 | 位置 | 失效表现 | 处理 |
|---|---|---|---|
| 类型 `BongoCat.OSSpecific.GlobalKeyHook`，方法 `Awake()`（无参） | `Patcher/Program.cs` 顶部常量 `TargetType`/`TargetMethod` | 补丁报 `type/method not found` | 在反编译结果里找一个**只执行一次的 MonoBehaviour 的 `Awake`/`Start`**（它在游戏启动时就存在）；改两个常量即可。`Start` 是协程时不要用 |
| 设置窗口节点：`SettingsView`（其下有 `TabButtons`、`ScrollView`） | `SettingsTab.cs` `TryBuildTab` | 日志无 `settings tab added`，也没有错误 | 重新导出界面树（附录 A），找到新名字，改 `Find("...")` 的字符串 |
| 模板页签 `TabButtons/OtherSettings`、定位页签 `XV40ModSettings` | 同上 | 没有页签 / 位置不对 | `XV40ModSettings` 不存在会自动放最后；`OtherSettings` 不存在会抛错并停用页签（不影响计数） |
| 正文样式来源 `Content/General` 下的 TMP 文本 | 同上 | 页面字体/颜色异常 | 找不到会退回到 `Content` 下任意 TMP 文本 |
| 中文字形 | 页签页面用游戏自己的 TMP 字体 | 方块字 | 完整面板仍是英文；页签页面字体取自游戏，若新版缺字则改文案为英文 |

> 游戏自带的 `GlobalKeyHook`、`WinKeyHook` 的实现本模块**不依赖**，它们怎么改都不影响计数。

## 5. 必须记住的环境陷阱（最耗时的坑，已经踩过）

1. **游戏的 Mono 做了代码裁剪，标准库里很多方法不存在。**
   已确认不存在或不可靠的：`AppDomain.ProcessExit`、`File.Move`、`File.ReadAllText(path, Encoding)`、`File.WriteAllText(path, text, Encoding)`、`Directory.GetParent`、`Assembly.GetManifestResourceStream`、`Console.WriteLine(string)`、`UnityEvent.GetPersistentEventCount` 等。
   可用的替代：`File.Copy(.., true)`+`File.Delete`、`File.ReadAllText(path)`/`WriteAllText(path, text)`（默认 UTF-8）、`Path.GetDirectoryName(Application.dataPath)`、把 HTML 模板编进 C# 常量、`Application.OpenURL` 打开报告。
   **补丁工具会自动检查**：模块用到了游戏里不存在的成员，直接拒绝打补丁并列出缺哪些。新增代码后如果出现该报错，换成上面这类 API。
2. **补丁工具在 .NET 9 上运行，不要用 `ImportReference(typeof(...))` 导入 BCL 类型**，否则会引用 `System.Private.CoreLib`，游戏里 `FileNotFoundException`。当前 `catch` 类型是手动构造的 `mscorlib` 引用，勿改回去。
3. **注入必须包 try/catch**：`Awake` 里一旦抛异常，`GlobalKeyHook.Instance` 不会被赋值，游戏本体会连锁报错。
4. **界面克隆必须在“未激活的容器”下做**：先 `Instantiate` 到一个 `SetActive(false)` 的临时对象下，替换 `Button`、删除 `LocalizeStringEvent`，再挂回 `TabButtons`，否则 `ButtonAnimator` 等组件的 `Awake` 会缓存到旧 `Button`。
5. **点击穿透**：游戏用射线检测判断鼠标是否在 UI 上（`UI` 或 `Ignore Raycast` 图层）。我们创建的对象设为 `UI` 图层。
6. **文件编码/转义**（工具链坑）：用脚本或 shell heredoc 写 `.bat`/`.cs` 时，`\`、`>nul`、`\u`、`\n` 可能被改写（曾把 `>nul` 变成 `>/dev/null`）。写完要 `grep` 检查，必要时直接用编辑器写文件。

## 6. 故障速查

| 现象 | 先看什么 | 常见原因 |
|---|---|---|
| 游戏启动即报错/猫不动 | `Player.log` 里的异常栈 | 补丁里用了裁剪掉的 API（第 5.1）或 `CoreLib` 引用（5.2）；先 `restore.bat` 恢复 |
| `Player.log` 没有 `[BongoKeyStats]` | `patch.bat` 输出；`Patcher.dll <Managed路径> --check` | 补丁没打上 / 被 Steam 覆盖 / 注入点变了 |
| 有 `loaded data dir`，没有页签 | 日志是否有 `settings tab failed:` | 节点名变了（第 4 节）；用附录 A 重新导出界面树 |
| 页签点了没反应 / 页面空白 | 日志 `tab click error` | `Button`/克隆流程被游戏改动 |
| 计数不对 | 对照验收标准（见下） | 轮询阻塞？（日志 `poller error`） |
| 导出 HTML 没反应 | 日志是否有 `report written` / `export failed` | `Application.OpenURL` 被系统拦截时，手动打开 `KeyStats\report.html` |
| 数据丢失 | `KeyStats\keystats.json.bak` | 写入中途被强杀，最多丢 30 秒 |

验收（每次更新后做一遍）：后台按 A×10、B×5 → A=10、B=5、占比 66.7%/33.3%；按住 A 3 秒只计 1 次；点鼠标/手柄不变；重启后累计延续、会话归零。

## 7. 为省算力的做法（给下一次开发者/AI）

- **不要重做逆向。** 只需确认第 4 节的几个名字是否还在：
  `ilspycmd -p -o D:\BongoCatDecomp "<Managed>\Assembly-CSharp.dll" -r "<Managed>"`，然后 `grep` `class GlobalKeyHook`、`void Awake`。
- **界面结构不要靠猜**：用附录 A 的诊断导出 `ui_dump.txt` 再改字符串；一次导出 2 万多行，只 `grep` 需要的节点名。
- 直接读 `Player.log` 排错，比让用户贴更快；用户只需要“重新打补丁→开一次设置→关游戏”。
- 补丁工具已内置：幂等检测、备份、引用校验、失败不写入。不要手动改游戏 DLL。
- 数据格式有 `schema` 字段（当前 1）；改格式时在 `KeyCounter.LoadJson` 里做兼容。

## 附录 A：重新导出设置界面树（诊断）

在 `SettingsTab.cs` 的 `TryBuildTab` 找到 `sv` 后临时加入，打开一次设置即可在 `KeyStats\ui_dump.txt` 得到整棵 Canvas 树（用完删除）：

```csharp
var sb = new System.Text.StringBuilder();
foreach (var cv in Resources.FindObjectsOfTypeAll<Canvas>())
{
    if (cv.transform.parent != null || !cv.gameObject.scene.IsValid()) continue;
    sb.AppendLine("=== CANVAS " + cv.name);
    Walk(cv.transform, 0, sb);
}
System.IO.File.WriteAllText(System.IO.Path.Combine(Bootstrap.Store.Dir, "ui_dump.txt"), sb.ToString());

static void Walk(Transform t, int d, System.Text.StringBuilder sb)
{
    if (d > 16) return;
    sb.Append(' ', d * 2).Append(t.name).Append(t.gameObject.activeSelf ? "" : " [inactive]").Append("  <");
    foreach (var c in t.GetComponents<Component>()) if (c != null) sb.Append(c.GetType().Name).Append(',');
    sb.AppendLine(">");
    for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), d + 1, sb);
}
```

当前版本的关键结构（2026-11-04）：
```
MainCanvas ... SettingsView
  TabButtons (VerticalLayoutGroup, Tabs)
    GameButtons / XV40ModSettings / GeneralSettings / SystemSettings / VisualSettings /
    InteractionSettings / InputSettings / PerformanceSettings / OtherSettings   (各为 Button + Image + 子 Text (TMP))
  ScrollView/Viewport/Content/{XV40 Mod Settings, General, System, Visuals, Interaction, Input, Performance, Other}
```
`Tabs` 组件：选中页签 = 该页签 `Button.interactable=false`；我们的页签通过“隐藏 ScrollView + 显示自己的页面”实现，不改 `Tabs` 的内部状态。

## 附录 B：关键决定记录

- 方案 A（独立模块 + 单点 IL 注入）；不用 BepInEx，不重建整个 DLL。
- 事件源改为自己轮询（游戏事件不带键码）。
- 面板位置：设置窗口页签（取代了最初的屏幕右上角浮动按钮）。
- 数据位置：游戏目录 `KeyStats\`，不可写时退回 `LocalLow`。
- 仅键盘；不统计鼠标和手柄；不联网。
