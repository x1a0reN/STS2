## GongDou STS2 Challenge MOD 项目规则

## 2026-05-25 最新第一题口径

- 设计、审核、实现第一题时，只读取并遵守 `D:\Projects\SlayTheSpire2ChallengeMod\docs\difficulty1_final_puzzle.md`。
- `D:\Projects\SlayTheSpire2ChallengeMod\docs\尖塔残局01-钙化邪教徒的第一课.md` 已废弃，不得再作为第一题来源。
- 第一题当前为：`石化狂信徒的第一课`，内部英文底模使用真实存在的 `CalcifiedCultist`。
- 玩家参数：铁甲战士，`8 HP / 3 能量 / 每回合抽 5`，固定选择 `8` 张卡，无药水、无遗物。
- 卡池：`StrikeIronclad x5`、`PerfectedStrike x2`、`Bash x1`、`IronWave x1`、`ShrugItOff x1`、`DefendIronclad x1`、`BodySlam x1`、`Uppercut x1`。
- 敌人参数：`55 HP`，行动节奏 `0/9/11/13`，第 4 回合结束前未击杀则失败。
- 第一题概率说明必须以 `scripts\enumerate-difficulty1-cultist.py` 的真实随机状态枚举结果为准，不能固定手牌顺序或假设必抽到关键牌。
- 当前枚举结果：`case_count = 162`、`viable_count = 151`、`stable_count = 0`；完整结果输出到 `artifacts\difficulty1-cultist-enumeration.json`。

## 2026-05-26 最新第二题口径

- 设计、审核、实现第二题时，只读取并遵守 `D:\Projects\SlayTheSpire2ChallengeMod\docs\difficulty2_final_puzzle.md`。
- 旧 `铁浪回声`、`毒刃过载`、`difficulty2_iron_wave_echo.md` 的旧内容不得再作为第二题来源。
- 第二题当前为：`药水池与护甲门槛`。
- 玩家参数：铁甲战士外壳，`14 HP / 3 能量 / 每回合抽 5`，固定选择 `6` 张卡，药水 `3 选 1`，无遗物。
- 敌人参数：`99 HP`；第 1 回合架盾充能，回合结束获得 `14` 点保留护甲；第 2/3/4 回合攻击 `10/18/24`。
- 卡池：`StrikeIronclad x3`、`DefendIronclad x2`、`Bash x1`、`Neutralize x1`、`BallLightning x2（改）`、`Survivor x1`、`QuickSlash x2（改）`、`DaggerThrow x1（改）`、`Clothesline x1`。
- 药水池：`FirePotion`、`VulnerablePotion`、`WeakPotion`，固定选择 `1` 瓶。
- 第二题概率说明必须以 `scripts\enumerate-difficulty2-armor-threshold.py` 的真实随机状态枚举结果或题面终审结果为准，不能固定手牌顺序。
- 当前终审结果：`case_count = 1713`、`stable_count = 0`、最高总成功率 `95.8667%`。

- 除非用户明确要求英文，否则回复使用简体中文。
- 代码标识符、命令、日志、报错信息保持原始语言。
- 不要修改《杀戮尖塔2》游戏原始文件；MOD 只通过官方 `mods` 目录加载。
- 不要执行删除类命令；如需清理旧产物，先说明替代方案并等待用户明确授权。

## 芙莉莲自定义角色 MOD

- 自定义角色 MOD 独立目录为 `D:\Projects\SlayTheSpire2ChallengeMod\src\GongdouSts2FrierenMod`。
- 不要把芙莉莲角色、卡牌、遗物、药水、Power 或角色注册逻辑写入 `GongdouSts2ChallengeMod`；两个 MOD 必须保持隔离。
- 芙莉莲设计来源为 `D:\Projects\SlayTheSpire2ChallengeMod\docs\frieren_character_design.md`。
- 当前芙莉莲 MOD 已按 `docs\frieren_character_design.md` 落实完整 82 张奖励牌、5 张生成牌 / 状态、10 个遗物、5 瓶药水与 91/10/5 图片资源；新增或修正卡牌时仍应优先继承 `FrierenCard` 并放入 `FrierenCardCatalog` / `FrierenCardPool`。
- 芙莉莲资源审计脚本为 `scripts\verify-frieren-assets.py`；修改卡牌、遗物、药水或图片接入后必须运行，确保卡图 `91`、遗物图 `10`、药水图 `5`，且缺失 / 未使用资源均为 `0`。
- 构建命令：`.\scripts\build-frieren.ps1 -Configuration Release -STS2GameDir 'D:\Steam\steamapps\common\Slay the Spire 2'`。
- 安装命令：`.\scripts\install-frieren.ps1 -Configuration Release -STS2GameDir 'D:\Steam\steamapps\common\Slay the Spire 2'`。

## Codex 专用后台账号与 Admin JWT

- 后台只使用一个 Codex 专用账号：`19999000050`。
- 禁止为了后台 bootstrap、预设、排行榜维护而反复创建新后台账号。
- Admin JWT 固定保存到当前 Windows 用户环境变量：
  - `HKCU\Environment\GONGDOU_ADMIN_TOKEN`
  - 过期时间记录到 `HKCU\Environment\GONGDOU_ADMIN_TOKEN_EXPIRES_AT`
- JWT 明文禁止写入仓库、脚本、文档、日志、提交信息或最终回复。
- `scripts\bootstrap-backend.ps1` 必须优先读取当前进程 `$env:GONGDOU_ADMIN_TOKEN`；为空时读取 `HKCU\Environment\GONGDOU_ADMIN_TOKEN`。
- 如果 token 过期或被服务端拒绝，只能复用 Codex 专用账号刷新/重签，不要创建新账号。

