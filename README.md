# GongDou STS2 Challenge Mod

## 2026-05-25 第一题最终口径

后续设计、审计和实现第一题，只读取并遵守：

`D:\Projects\SlayTheSpire2ChallengeMod\docs\difficulty1_final_puzzle.md`

旧文件 `D:\Projects\SlayTheSpire2ChallengeMod\docs\尖塔残局01-钙化邪教徒的第一课.md` 已废弃，不再作为实现或题面来源。核心定稿如下：

- 题目：`石化狂信徒的第一课`（内部真实底模：`CalcifiedCultist`）
- 卡池：`StrikeIronclad x5, PerfectedStrike x2, Bash x1, IronWave x1, ShrugItOff x1, DefendIronclad x1, BodySlam x1, Uppercut x1`
- 参数窗口：`55 HP / 玩家 8 HP / 敌人节奏 0-9-11-13`
- 选择数量：固定 `8` 张
- 药水 / 遗物：`0 / 0`
- 全池审计：`case_count = 162`，`viable_count = 151`，`stable_count = 0`
- 终稿列出的三条代表线：
  - 快解：`打击 x5, 完美打击 x2, 重击`，2 回合击杀 `57.1429%`
  - 主解：`打击 x3, 完美打击 x2, 重击, 铁斩波, 全身撞击`，3 回合击杀 `62.5000%`
  - 稳线：`打击 x5, 铁斩波, 耸肩无视, 防御`，4 回合击杀 `96.7262%`

当前第一题不得使用其他旧口径。

## 2026-05-26 第二题最终口径

第二题只读取并遵守：

`D:\Projects\SlayTheSpire2ChallengeMod\docs\difficulty2_final_puzzle.md`

核心定稿如下：

- 题目：`药水池与护甲门槛`
- 玩家：`14 HP / 3 能量 / 每回合抽 5`
- 敌人：`99 HP`，第 1 回合架盾充能，回合结束获得 `14` 点保留护甲；第 2/3/4 回合攻击 `10/18/24`
- 卡池：14 张候选，固定选 `6` 张
- 药水：`FirePotion / VulnerablePotion / WeakPotion` 三选一
- 遗物：无
- 改牌：`BallLightning`、`QuickSlash`、`DaggerThrow` 均按题面“（改）”效果结算
- 审计口径：`case_count = 1713`，`stable_count = 0`，最高总成功率 `95.8667%`
- 本地枚举脚本：`scripts\enumerate-difficulty2-armor-threshold.py`

第二题不再使用旧 `铁浪回声` 或 `毒刃过载` 口径。

当前流程：

- 客户端启动挑战后，MOD 消费 `gongdou.mod.ipc.v1` 启动上下文。
- 游戏内直接显示准备界面，玩家在游戏内选择卡牌；难度 1 第一题不提供药水和遗物。
- 准备界面确认后，MOD 创建铁甲战士挑战 Run，并进入 `石化狂信徒` 战斗。
- 基础通关条件：第 4 回合结束前杀死敌人。
- 录像从游戏内准备界面开始，结束后由客户端根据服务端 `needRecording` 返回决定是否上传。
- 排行榜首版按 `timeMs` 通关时间升序排名，后续可改成回合优先复合分。

## Build

```powershell
cd D:\Projects\SlayTheSpire2ChallengeMod
.\scripts\build.ps1 -Configuration Release -STS2GameDir 'D:\Steam\steamapps\common\Slay the Spire 2'
```

## Frieren character mod

自定义角色 MOD 独立在：

`D:\Projects\SlayTheSpire2ChallengeMod\src\GongdouSts2FrierenMod`

它和残局挑战 MOD 隔离，拥有独立：

- `mod_manifest.json`
- 初始化入口：`GongdouSts2FrierenMod`
- 角色：`FrierenCharacter`
- 卡池：`FrierenCardPool`
- 遗物池：`FrierenRelicPool`
- 药水池：`FrierenPotionPool`
- 机制 Power：`解析`、`隐匿魔力`、`解放`、`回忆`，以及普通魔法、长咏唱、保留、Boss 替换遗物等配套 Power
- 角色注册、自定义卡图 / 遗物 / 药水贴图 patch；人物骨骼、能量计数器、地图标记等仍集中 fallback 到 Ironclad 资源

当前 `0.1.8` 已按 `docs\frieren_character_design.md` 与美术包落实完整芙莉莲内容：

- 起始牌：`打击 x4`、`防御 x4`、`基础杀人魔法 x1`、`魔力抑制 x1`
- 奖励卡池：`80` 张常规奖励牌 + `2` 张 Ancient 牌
- 生成牌 / 状态：`5` 张
- 遗物：`9` 个常规遗物 + `1` 个 Boss 替换遗物
- 药水：`5` 瓶
- 图片资源：卡牌 `89` 张、遗物 `10` 张、药水 `5` 张，全部从 `docs\芙莉莲完整美术资源方图800最终包_20260527` 接入
- 常规奖励池不直接投放 Ancient 牌；起始牌、生成牌、状态牌也不会混进常规奖励池

芙莉莲逻辑必须继续留在 `GongdouSts2FrierenMod`，不要把自定义角色逻辑塞进 `GongdouSts2ChallengeMod`。

构建：

```powershell
cd D:\Projects\SlayTheSpire2ChallengeMod
.\scripts\build-frieren.ps1 -Configuration Release -STS2GameDir 'D:\Steam\steamapps\common\Slay the Spire 2'
```

安装到本机 STS2 mods 目录：

```powershell
cd D:\Projects\SlayTheSpire2ChallengeMod
.\scripts\install-frieren.ps1 -Configuration Release -STS2GameDir 'D:\Steam\steamapps\common\Slay the Spire 2'
```

资源与模型覆盖审计：

```powershell
cd D:\Projects\SlayTheSpire2ChallengeMod
python .\scripts\verify-frieren-assets.py --json .\artifacts\frieren-assets-audit.json
```

验收关键计数：`cardPngCount=89`、`relicPngCount=10`、`potionPngCount=5`、`standardRewardCardTypeCount=80`、`ancientCardTypeCount=2`、`allCardTypeCount=91`。

## Install

```powershell
cd D:\Projects\SlayTheSpire2ChallengeMod
.\scripts\install.ps1 -Configuration Release -STS2GameDir 'D:\Steam\steamapps\common\Slay the Spire 2'
```

安装脚本只复制：

- `GongdouSts2ChallengeMod.dll`
- `Gongdou_STS2_Challenge.json`

不删除 mods 目录中的任何文件。

## Backend bootstrap

需要后台 Admin JWT：

```powershell
$env:GONGDOU_ADMIN_TOKEN = '<admin jwt>'
.\scripts\bootstrap-backend.ps1
```

脚本会创建或复用：

- 游戏：`杀戮尖塔2`
- 频道：`杀戮尖塔2`
- 预设：`尖塔残局（10关）`
- 排行榜：`尖塔残局 01：石化狂信徒的第一课`

## Puzzle enumeration

第 1 关可通资源组合枚举脚本：

```powershell
cd D:\Projects\SlayTheSpire2ChallengeMod
$env:PYTHONIOENCODING = 'utf-8'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
python .\scripts\enumerate-difficulty1-cultist.py --json .\artifacts\difficulty1-cultist-enumeration.json
```

脚本会先校验 `D:\Steam\steamapps\common\Slay the Spire 2` 中的 `sts2.dll` 与 `SlayTheSpire2.pck`，确认题面使用的英文模型名和中文显示名真实存在，再枚举资源组合。

