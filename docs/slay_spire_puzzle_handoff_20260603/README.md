# 尖塔残局出题交接包

生成日期：2026-06-03

这个包用于把当前“尖塔残局出题框架”、可安装技能、资料库、当前题目状态和审计资产交给下一位承接者。

## 2026-06-06 真实性与文案复核补充

本包后续出题必须优先解决“看起来像尖塔、实际不是尖塔”的文案问题。正式玩家题面可以使用自定义规则、机制和数值改动，但必须把“游戏原版”和“本题自定义/本题改动”标清楚。原版资源不能写错，自定义资源不能伪装成原版；哪怕是自定义文案，也必须使用《杀戮尖塔》式短句和清晰结算时机，不能写成口胡规则说明。

当前联网复核发现，不同资料源存在版本和数量差异：`slaythespire2.net/zh-CN/card` 标注 `Data Version: v0.106.1 (2026-05-22)` 且显示 549 张卡；`sts2.wiki/cards/` 显示 566 张卡；`www.stratgg.com/cards/` 显示 575 张卡。药水、遗物页也存在数量差异或翻译未完全覆盖。因此正式题目必须记录资料源、版本、核验日期和冲突处理结论；资料冲突时标为“待复核”，不得直接定稿。

本轮联网核验使用的主要入口：

- `https://slaythespire2.net/zh-CN/card`
- `https://slaythespire2.net/zh-CN/potion`
- `https://slaythespire2.net/zh-CN/relic`
- `https://sts2.wiki/cards/`
- `https://sts2.wiki/potions/`
- `https://sts2.wiki/relics/`
- `https://www.stratgg.com/cards/`
- STS1 风格与机制 fallback：`https://slay-the-spire.fandom.com/wiki/Potions`、`https://slaythespire.wiki.gg/wiki/Poison`、`https://slaythespire.wiki.gg/wiki/Orbs`、`https://slay-the-spire.fandom.com/wiki/Divinity`

写玩家题面时按原游戏短句式表达：`造成 X 点伤害。`、`获得 X 点格挡。`、`给予 X 层易伤/虚弱。`、`抽 X 张牌。`、`丢弃 X 张牌。`、`消耗。`。需要写自定义机制时，先给机制名、归属和触发时机，再用同样短句式描述效果。不要在玩家题面里写“压血线”“资源包”“最高成功率路线”等审计术语；分号能不用就不用，别把一张牌写成公文。

## 先读顺序

1. `00_先读交接说明/承接者先读.md`
2. `00_先读交接说明/当前状态与作废说明.md`
3. `00_先读交接说明/技能使用规则.md`
4. `01_框架要义与规则/尖塔残局出题框架要义.md`
5. `01_框架要义与规则/slay-spire-puzzle-control-skill_源文件/SKILL.md`
6. `01_框架要义与规则/slay-spire-puzzle-control-skill_源文件/references/audit-protocol.md`
7. `01_框架要义与规则/slay-spire-puzzle-control-skill_源文件/references/difficulty-scale.md`

## 目录说明

- `00_先读交接说明`：给承接者的记忆交接，不需要翻聊天记录。
- `01_框架要义与规则`：当前最重要的出题框架和本轮抽象出的控制 skill。
- `02_可安装技能`：本环境中和出题、审题、mod 交接相关的 skill 源文件。
- `03_资料库`：STS2 本地资料库、快照和摘要。
- `04_当前题目`：第一组 D1-D10 当前文档，以及第二组作废文档。
- `05_审计脚本与结果`：第一组审计脚本/结果；第二组仅作为反例和历史记录。
- `06_模板与承接清单`：后续继续出题、审计、交付时用的检查清单。

## 最重要结论

当前框架的核心目标不是“只有三种解法”，而是每题至少给出 3 个代表解法族，并把所有高成功率路线纳入可解释、可枚举、可控制范围。

正式题目不能用核心必选替代资源识别，不能默认敌人有牌堆，正常尖塔残局不能用固定优先级自动出牌来代替玩家决策。
