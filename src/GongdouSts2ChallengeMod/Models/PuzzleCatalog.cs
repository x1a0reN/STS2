namespace GongdouSts2ChallengeMod.Models;

public static class PuzzleCatalog
{
    private const string DocPath = "docs/D1-D10_final_puzzles.md";

    private static readonly Dictionary<int, Sts2PuzzleConfig> Configs = new()
    {
        [1] = Config(1, "difficulty1_petrified_cultist", "石化狂信徒的第一课", 8, 54, 7, 7, 0, 0,
            "从 13 张候选牌中选择 7 张作为起始牌组，击败敌人；不限回合，除非一方死亡。",
            "敌人：石化狂信徒，生命值 54。第 1/2/3/4 回合分别攻击 6/12/15/18，之后重复最后一个行动；不限回合，除非一方死亡。",
            [Act(1, 6), Act(2, 12), Act(3, 15), Act(4, 18)]),

        [2] = Config(2, "difficulty2_armor_threshold", "药水池与护甲门槛", 15, 72, 6, 6, 1, 0,
            "从 14 张候选牌中选择 6 张，并从 3 种药水中选择 1 种，击败带保留格挡的敌人；不限回合，除非一方死亡。",
            "敌人：护甲门槛哨卫，生命值 72。第 1 回合攻击 6 并获得 14 点保留格挡；第 2/3/4 回合攻击 20/26/30，之后重复最后一个行动；不限回合，除非一方死亡。",
            [Act(1, 6, armor: 14), Act(2, 20), Act(3, 26), Act(4, 30)]),

        [3] = Config(3, "difficulty3_cunning_bell", "双奇巧铃鸣与伪刃破绽", 24, 92, 8, 8, 1, 1,
            "按限制选择 8 张牌、1 瓶药水和 1 个遗物；每回合抽 6 张牌，围绕奇巧弃牌、铃鸣两牌上限和连环破绽击败敌人；不限回合，除非一方死亡。",
            "敌人：伪刃铃卫，生命值 92。第 1/2/3/4 回合攻击 10/25/26/34，之后重复最后一个行动；不限回合，除非一方死亡。",
            [Act(1, 10), Act(2, 25), Act(3, 26), Act(4, 34)],
            [
                Req("预备（改）与生存者（改）最多选择 1 张", ["D3_Prepared", "D3_Survivor"], max: 1)
            ]),

        [4] = Config(4, "difficulty4_burn_countdown", "灼伤洗牌与消耗窗口", 34, 126, 9, 9, 1, 0,
            "从18张候选牌中选择9张作为起始牌组，从3种药水中选择1种带入战斗。状态牌或诅咒牌进入你的牌堆后，顺劈斩（改）稳定解锁强化。不限回合，除非一方死亡。",
            "敌人：灼伤洗牌守卫，生命值126。第1/2/3/4/5回合攻击10/17/24/32/44。第1/2/3回合结束分别将1/1/2张灼伤加入玩家弃牌堆。之后重复第5回合行动。不限回合，除非一方死亡。",
            [Act(1, 10), Act(2, 17), Act(3, 24), Act(4, 32), Act(5, 44)]),

        [5] = Config(5, "difficulty5_void_lock", "人工制品虚空锁", 28, 88, 10, 10, 1, 1,
            "按限制选择 10 张牌、1 瓶药水和 1 个遗物；处理人工制品、虚空、药水相位和虚空坍缩；不限回合，除非一方死亡。",
            "敌人：虚空锁哨卫，生命值 88，初始 3 层人工制品。第 1-5 回合攻击 11/19/27/33/48，部分回合获得保留格挡，之后重复最后一个行动；不限回合，除非一方死亡。",
            [Act(1, 11), Act(2, 19, armor: 14), Act(3, 27), Act(4, 33, armor: 16), Act(5, 48)],
            [
                Req("痛击与上勾拳（改）最多选择 1 张", ["Bash", "D5_Clothesline"], max: 1)
            ]),

        [6] = Config(6, "difficulty6_stance_breach", "平静破绽与怒火越界", 29, 94, 10, 10, 1, 1,
            "按限制选择10张牌、1瓶药水和1个遗物；利用姿态、平静返能、愤怒双刃和平静破绽完成斩杀；不限回合，除非一方死亡。",
            "敌人：怒火越界者，生命值94。第1/2/3/4/5回合攻击12/19/26/34/48；第2/4回合结束分别获得14/22点保留格挡，之后重复第5回合行动；不限回合，除非一方死亡。",
            [Act(1, 12), Act(2, 19, armor: 14), Act(3, 26), Act(4, 34, armor: 22), Act(5, 48)]),

        [7] = Config(7, "difficulty7_poison_antidote", "解毒腺与毒雾连锁", 32, 122, 11, 11, 1, 1,
            "按限制选择11张牌、1瓶药水和1个遗物；围绕毒、催化、毒雾、解毒腺和毒转格挡完成斩杀；不限回合，除非一方死亡。",
            "敌人：解毒腺异兽，生命值122。第1/2/3/4/5回合攻击11/20/28/36/50；第2回合毒伤害后解毒6层并在回合结束获得14点保留格挡；第4回合毒伤害后解毒9层并在回合结束获得22点保留格挡；之后重复第5回合行动；不限回合，除非一方死亡。",
            [Act(1, 11), Act(2, 20, armor: 14), Act(3, 28), Act(4, 36, armor: 22), Act(5, 50)]),

        [8] = Config(8, "difficulty8_orb_overload", "绝缘破裂与黑球过载", 40, 75, 12, 12, 1, 1,
            "按限制选择12张牌、1瓶药水和1个遗物。战斗开始时生成1个闪电充能球和1个冰霜充能球。管理充能球槽、黑暗储值、冰霜绝缘破裂、集中与循环。不限回合，除非一方死亡。",
            "敌人：绝缘破裂体，生命值75。第1-5回合攻击13/22/31/40/55；第2、4回合结束分别获得24/34点保留格挡，之后重复最后一个行动；不限回合，除非一方死亡。",
            [Act(1, 13), Act(2, 22, armor: 24), Act(3, 31), Act(4, 40, armor: 34), Act(5, 55)]),

        [9] = Config(9, "difficulty9_divinity_mirror", "破镜神格与护甲反射", 38, 112, 13, 13, 1, 1,
            "按限制选择13张镜像牌、1瓶药水和1个遗物；每回合镜像抽取4张，并在回合开始稳定获得真言；利用镜像抽取、真言、神格和护甲反射；不限回合，除非一方死亡。",
            "敌人：破镜神格像，生命值112。第1/2/3回合攻击15/24/34；第2回合结束获得24点保留格挡，之后重复第3回合行动；不限回合，除非一方死亡。",
            [Act(1, 15), Act(2, 24, armor: 24), Act(3, 34)]),

        [10] = Config(10, "difficulty10_time_rift", "时间裂隙与三相斩杀", 40, 78, 16, 16, 1, 1,
            "按限制选择16张裂隙牌、1瓶药水和1个遗物。每回合从裂隙牌组中抽取4张牌，并通过裂隙调律稳定获得充能与标记。处理相位门槛、充能、标记、回声、延迟伤害与过热。不限回合，除非一方死亡。",
            "敌人：时间裂隙核心，生命值78。第1回合攻击18点。第2回合攻击28点，回合结束时获得32点保留格挡。第3回合攻击38点，回合结束时敌人失去3层标记。第4回合起攻击52点，之后重复此行动。不限回合，除非一方死亡。",
            [Act(1, 18), Act(2, 28, armor: 32), Act(3, 38), Act(4, 52)])
    };

    private static readonly Dictionary<int, ResourcePool> Resources = new()
    {
        [1] = Pool(
            [Item("StrikeIronclad", "打击", "造成6点伤害。", 4), Item("DefendIronclad", "防御", "获得5点格挡。", 3), Item("Bash", "痛击", "造成8点伤害。\n给予2层易伤。"), Item("PerfectedStrike", "完美打击", "造成6点伤害。\n你每有一张名字中含有“打击”的牌，伤害+2。"), Item("D1_HeavyHammer", "重锤（改）", "造成23点伤害。"), Item("BodySlam", "全身撞击", "造成你当前格挡值的伤害。"), Item("IronWave", "铁斩波", "获得5点格挡。\n造成5点伤害。"), Item("FlyingSword", "飞剑回旋镖", "随机对敌人造成3点伤害3次。")]),

        [2] = Pool(
            [Item("StrikeIronclad", "打击", "造成6点伤害。", 3), Item("DefendIronclad", "防御", "获得5点格挡。", 2), Item("Bash", "痛击", "造成8点伤害。\n给予2层易伤。"), Item("Neutralize", "中和", "造成3点伤害。\n给予1层虚弱。"), Item("D2_BallLightning", "球状闪电（改）", "造成5点伤害。若敌人有格挡，改为造成11点伤害。", 2), Item("Survivor", "生存者", "获得8点格挡。"), Item("D2_QuickSlash", "切割（改）", "造成7点伤害。若敌人有易伤，改为造成11点伤害。", 2), Item("DaggerThrow", "投掷匕首（改）", "造成9点伤害。"), Item("D2_Clothesline", "上勾拳（改）", "造成12点伤害。\n给予敌人2层虚弱。")],
            [Item("FirePotion", "火焰药水", "造成20点伤害。"), Item("VulnerablePotion", "易伤药水", "给予3层易伤。"), Item("WeakPotion", "虚弱药水", "给予2层虚弱。")]),

        [3] = Pool(
            [Item("Bash", "痛击", "造成8点伤害。\n给予2层易伤。"), Item("D3_Prepared", "预备（改）", "打出时弃掉当前手牌中按优先级最靠前的1张。{DiscardTargetLine}"), Item("D3_Survivor", "生存者（改）", "获得5点格挡。\n弃掉当前手牌中按优先级最靠前的1张。{DiscardTargetLine}"), Item("D3_Finisher", "终结（改）", "造成8点伤害。\n本战斗每有1张奇巧牌被弃掉并免费打出，额外造成4点伤害。"), Item("D3_BackstabCunning", "背刺（改）", "[gold]奇巧[/gold]：改为造成10点伤害。\n造成6点伤害。", 2), Item("DaggerThrow", "投掷匕首（改）", "造成9点伤害。"), Item("D3_Feint", "佯刺（改）", "造成7点伤害。若本回合有牌因奇巧打出，改为造成11点伤害。", 2), Item("Neutralize", "中和", "造成3点伤害。\n给予1层虚弱。"), Item("StrikeIronclad", "打击", "造成6点伤害。", 3), Item("DefendIronclad", "防御", "获得5点格挡。", 2), Item("D3_ShadowStep", "影步（改）", "[gold]奇巧[/gold]：改为造成7点伤害并获得4点格挡。\n造成5点伤害，获得3点格挡。", 2), Item("D3_VoidBlade", "虚刃（改）", "造成4点伤害。", 2)],
            [Item("D3_CunningPotion", "奇巧药水", "按弃牌优先级弃掉1张手牌。"), Item("VulnerablePotion", "易伤药水", "给予3层易伤。"), Item("FirePotion", "火焰药水", "造成20点伤害。")],
            [Item("D3_SharpDice", "锋利骰子", "奇巧免费打出背刺（改）且无格挡时伤害+2。"), Item("D3_ReturnHolster", "折返皮套", "每回合第一次弃牌后获得1点能量。"), Item("D3_HollowCharm", "空心护符", "每回合第一次弃牌后获得6点格挡。")]),

        [4] = Pool(
            [Item("Bash", "痛击", "造成8点伤害。\n给予2层易伤。"), Item("Uppercut", "上勾拳", "造成13点伤害。\n给予1层虚弱。\n给予1层易伤。"), Item("BurningPact", "燃烧契约", "消耗1张手牌。\n抽2张牌。"), Item("TrueGrit", "坚毅", "获得7点格挡。\n消耗1张随机手牌。"), Item("SecondWind", "重振精神", "消耗所有非攻击牌。\n每消耗1张牌，获得5点格挡。"), Item("FeelNoPain", "无惧疼痛", "每当一张牌被消耗，获得3点格挡。"), Item("D4_FireBreathing", "火焰吐息", "每当抽到状态牌或诅咒牌时，对所有敌人造成6点伤害。"), Item("D4_Evolve", "进化", "每当抽到状态牌时，抽1张牌。"), Item("D4_WildStrike", "狂野打击", "造成12点伤害，将1张伤口洗入抽牌堆。"), Item("D4_RecklessCharge", "鲁莽冲锋", "造成7点伤害，将1张晕眩洗入抽牌堆。"), Item("PommelStrike", "剑柄打击", "造成9点伤害。\n抽1张牌。"), Item("ShrugItOff", "耸肩无视", "获得8点格挡。\n抽1张牌。"), Item("IronWave", "铁斩波", "获得5点格挡。\n造成5点伤害。"), Item("D4_Cleave", "顺劈斩（改）", "造成8点伤害。\n若本战斗有状态牌或诅咒牌进入过你的牌堆，改为造成12点伤害。"), Item("StrikeIronclad", "打击", "造成6点伤害。", 2), Item("DefendIronclad", "防御", "获得5点格挡。", 2)],
            [Item("D4_FirePotion", "火焰药水", "造成20点固定伤害。不受易伤加成。"), Item("EnergyPotion", "能量药水", "本回合获得2点能量。"), Item("GhostInAJar", "幽灵药水", "本回合获得1层无实体。")]),

        [5] = Pool(
            [Item("Bash", "痛击", "造成8点伤害。\n给予2层易伤。"), Item("Uppercut", "上勾拳", "造成13点伤害。\n给予1层虚弱。\n给予1层易伤。"), Item("Neutralize", "中和", "造成3点伤害。\n给予1层虚弱。"), Item("D5_Clothesline", "上勾拳（改）", "造成12点伤害。\n给予敌人2层虚弱。"), Item("D5_VoidRend", "虚空裂解（改）", "造成8点伤害。\n若手牌或弃牌堆中有虚空，消耗其中1张虚空，改为造成22点伤害。"), Item("DaggerThrow", "投掷匕首（改）", "造成9点伤害。"), Item("D5_QuickSlash", "切割（改）", "造成7点伤害。若敌人没有人工制品，改为造成11点伤害。", 2), Item("D5_BallLightning", "球状闪电（改）", "造成5点伤害。若手牌或弃牌堆中有虚空，改为造成12点伤害。", 2), Item("D5_ColdSnap", "寒流（改）", "造成6点伤害。\n获得4点格挡。"), Item("FlyingSword", "飞剑回旋镖", "随机对敌人造成3点伤害3次。"), Item("D5_Recycle", "充电（改）", "获得5点格挡。\n若手牌或弃牌堆中有虚空，消耗其中1张虚空，获得1点能量，额外获得8点格挡，并给予敌人1层虚弱。"), Item("StrikeIronclad", "打击", "造成6点伤害。", 3), Item("DefendIronclad", "防御", "获得5点格挡。", 3), Item("D5_Feint", "佯攻（改）", "造成4点伤害。")],
            [Item("D2_FirePotion", "火焰药水", "造成20点固定伤害。"), Item("D5_BarrierPotion", "破障药水", "移除人工制品并给予2层易伤。"), Item("D5_EnergyPotion", "能量药水", "本回合获得3点能量和20点格挡。")],
            [Item("D5_Shuriken", "手里剑", "每回合第3次造成攻击伤害后，获得1点力量。"), Item("D5_Anchor", "锚", "第1回合开始时获得10点格挡。"), Item("D5_VoidLens", "虚空透镜", "每回合第一次用虚空裂解（改）消耗虚空时，该次伤害额外增加24点。")]),

        [6] = Pool(
            [Item("D6_Eruption", "痛击（改）", "造成9点伤害。\n进入愤怒。"), Item("D6_Vigilance", "防御（改）", "获得8点格挡。\n进入平静。"), Item("D6_EmptyFist", "切割（改）", "造成9点伤害。\n进入普通。"), Item("D6_EmptyBody", "防御（改）", "获得8点格挡。\n进入普通。"), Item("D6_FollowUp", "切割（改）", "造成4点伤害。\n如果本回合改变过姿态，改为造成8点伤害。", 2), Item("D6_WheelKick", "上勾拳（改）", "造成15点伤害。"), Item("D6_Offering", "祭品（改）", "造成5点伤害。"), Item("D6_CutThroughFate", "切割（改）", "造成6点伤害。若你处于平静，改为造成11点伤害。", 2), Item("D6_BowlingBash", "全身撞击（改）", "造成8点伤害。\n若敌人有格挡，改为造成12点伤害。"), Item("D6_Protect", "防御（改）", "获得11点格挡。"), Item("StrikeIronclad", "打击", "造成6点伤害。", 3), Item("DefendIronclad", "防御", "获得5点格挡。", 3), Item("D6_Halt", "防御（改）", "获得4点格挡。\n若你处于愤怒，改为获得9点格挡。", 2)],
            [Item("D6_RagePotion", "怒火药水", "本回合获得1点能量并进入愤怒。"), Item("D6_CalmPotion", "平静药水", "获得6点格挡并进入平静。"), Item("D6_FirePotion", "火焰药水", "造成18点固定伤害。")],
            [Item("D6_VioletLotus", "紫莲花", "你在平静姿态中打出的攻击额外造成8点伤害。离开平静时改为获得4点能量。"), Item("D6_RageCharm", "怒焰护符", "每回合第一次进入愤怒时，获得7点格挡。"), Item("D6_StanceSeal", "姿态刻印", "每回合第一次改变姿态后，你本回合下一张攻击额外造成5点伤害。")]),

        [7] = Pool(
            [Item("D7_DeadlyPoison", "致命毒药", "给予5层中毒。", 2), Item("D7_BouncingFlask", "弹跳药瓶（改）", "给予敌人8层毒。"), Item("D7_PoisonedStab", "带毒刺击（改）", "造成6点伤害。\n给予敌人3层毒。", 2), Item("D7_Catalyst", "致命毒药（改）", "使敌人当前毒层翻倍。"), Item("D7_NoxiousFumes", "毒雾（改）", "本场战斗中，每回合开始时给予敌人3层毒。"), Item("D7_Bane", "带毒刺击（改）", "造成7点伤害。\n若敌人有毒，改为造成14点伤害。", 2), Item("D7_Predator", "猎杀者（改）", "造成15点伤害。"), Item("DaggerThrow", "投掷匕首（改）", "造成9点伤害。"), Item("D7_LegSweep", "扫腿", "给予2层虚弱。\n获得11点格挡。"), Item("D7_CloakAndDagger", "斗篷与匕首（改）", "获得6点格挡。\n造成4点伤害。"), Item("StrikeIronclad", "打击", "造成6点伤害。", 3), Item("DefendIronclad", "防御", "获得5点格挡。", 3), Item("D7_Backflip", "后空翻（改）", "获得7点格挡。"), Item("D7_Caltrops", "铁蒺藜（改）", "本场战斗中，每当敌人攻击时，造成6点反击伤害。")],
            [Item("D7_PoisonPotion", "毒药药水", "回合开始时使用，给予敌人10层毒。"), Item("D7_CatalystPotion", "催化药水", "回合开始时使用，使敌人当前毒层翻倍。"), Item("D4_GhostPotion", "幽灵药水", "回合开始时使用，本回合受到的攻击伤害减半，向下取整。")],
            [Item("D7_SnakeSkull", "蛇颅", "每次给予敌人毒时，额外给予1层毒。"), Item("D7_PoisonFunnel", "毒液漏斗", "毒层不会自然减少。敌人的解毒量减少4层。"), Item("D7_RingOfNeedles", "针戒", "每回合第一次用攻击牌命中有毒的敌人时，该攻击额外造成6点伤害。")]),

        [8] = Pool(
            [Item("D8_Zap", "电击（改）", "生成1个闪电充能球。\n若你没有充能球，额外生成1个闪电充能球。", 2), Item("D8_Dualcast", "双重释放（改）", "连续释放最左侧充能球2次。"), Item("D8_Darkness", "漆黑（改）", "生成1个黑暗充能球。", 2), Item("D8_Recursion", "双重释放（改）", "释放最左侧充能球。\n以原储值重新生成该充能球。"), Item("D8_Loop", "循环（改）", "本场战斗获得循环。"), Item("D8_Chill", "冰寒（改）", "生成1个冰霜充能球。"), Item("D8_ColdSnap", "寒流（改）", "造成6点伤害。\n生成1个冰霜充能球。", 2), Item("D8_BallLightningOrb", "球状闪电（改）", "造成7点伤害。\n生成1个闪电充能球。", 2), Item("D8_Melter", "光束射线（改）", "造成10点伤害。\n该伤害无视敌人格挡。"), Item("D8_Streamline", "精简（改）", "造成15点伤害。"), Item("StrikeIronclad", "打击", "造成6点伤害。", 3), Item("DefendIronclad", "防御", "获得5点格挡。", 3), Item("D8_Leap", "飞跃", "获得9点格挡。", 2), Item("D8_Coolheaded", "冷静头脑（改）", "获得5点格挡。\n生成1个冰霜充能球。", 2)],
            [Item("D8_FocusPotion", "集中药水", "回合开始时使用，本场战斗集中+2。"), Item("D8_DarkPotion", "黑暗药水", "回合开始时使用，生成1个黑暗充能球。若携带暗核，初始储值为18，否则为14。"), Item("D8_EvokePotion", "释放药水", "回合开始时使用，连续释放最左侧充能球2次。")],
            [Item("D8_DataDisk", "数据磁盘", "战斗开始时集中+1。"), Item("D8_GoldCable", "镀金线缆", "回合结束时，最左侧充能球额外触发1次被动效果。"), Item("D8_DarkCore", "暗核", "漆黑（改）生成的黑暗充能球初始储值改为10。黑暗充能球每次被动储值额外+2。")]),

        [9] = Pool(
            [Item("D9_Devotion", "崇拜（改）", "获得4点真言。"), Item("D9_Prostrate", "护驾！！！（改）", "获得4点格挡。\n获得2点真言。", 2), Item("D9_Prayer", "引导之星（改）", "获得5点格挡。\n获得3点真言。", 2), Item("D9_Worship", "崇拜（改）", "获得5点真言。", 2), Item("D9_Brilliance", "辐射（改）", "造成8点伤害。\n当前每有1点真言，伤害增加2点。", 2), Item("D9_Ragnarok", "七星（改）", "连续4次造成5点伤害。"), Item("D9_Judgment", "王之凝视（改）", "若敌人生命不高于30，直接击杀。\n否则造成5点伤害。\n敌人获得16点格挡。"), Item("D9_CarveReality", "淬炼刀刃（改）", "造成6点伤害。", 2), Item("D9_Smite", "切割（改）", "造成12点伤害。", 2), Item("D9_Offering", "祭品（改）", "造成5点伤害。", 2), Item("D9_Sanctity", "中子护盾（改）", "获得8点格挡。\n若当前真言至少5，额外获得4点格挡。", 2), Item("D9_Wallop", "锤子时间（改）", "获得13点格挡。", 2), Item("StrikeIronclad", "打击", "造成6点伤害。", 3), Item("DefendIronclad", "防御", "获得5点格挡。", 2), Item("D9_EmptyBody", "防御（改）", "获得7点格挡。\n若当前处于神格，获得1点能量并离开神格。", 2)],
            [Item("D9_DivinityPotion", "神格药水", "回合开始时使用，本回合直接进入神格。"), Item("D9_MantraPotion", "真言药水", "回合开始时使用，获得6点真言。"), Item("D9_MirrorBreakPotion", "破镜药水", "回合开始时使用，敌人失去20点格挡，最低降至0。")],
            [Item("D9_Damaru", "达玛鲁", "每回合开始时获得2点真言。"), Item("D9_Scripture", "经文残页", "每次获得真言时，额外获得1点真言。"), Item("D9_SundialShard", "日晷碎片", "一场战斗中第一次进入神格后，你的下一次攻击额外造成15点伤害。")]),

        [10] = Pool(
            [
                Item("D10_TimeSeal", "时间刻印（改）", "获得3点充能。"),
                Item("D10_ChargeStance", "充能姿态（改）", "获得1点充能。获得3点格挡。", 3),
                Item("D10_RiftMark", "裂隙标记（改）", "给予敌人2层标记。如果你拥有棱镜碎片，改为3层。", 3),
                Item("D10_EchoStrike", "回声打击（改）", "造成7点伤害。每点充能使伤害增加2点。敌人每有1层标记，使伤害增加1点。", 3),
                Item("D10_EchoForm", "回声形态（改）", "获得1层回声。", 2),
                Item("D10_DelayedBlast", "延迟爆破（改）", "下回合开始时，对敌人造成18点延迟伤害。敌人每有1层标记，使该伤害增加4点。", 2),
                Item("D10_OverloadRay", "过载射线（改）", "造成12点伤害。每点充能使伤害增加3点。获得2层过热。", 2),
                Item("D10_VentHeat", "排热（改）", "失去2层过热。获得6点格挡。", 2),
                Item("D10_PhaseBarrier", "相位屏障（改）", "获得8点格挡。每点充能使格挡增加1点，最多增加6点。", 3),
                Item("D10_FocusCalibrate", "聚焦校准（改）", "获得2点充能。给予敌人1层标记。如果你拥有棱镜碎片，改为2层。", 2),
                Item("D10_FinalCommand", "终局指令（改）", "基础斩杀门槛为32点。敌人每有1层标记，斩杀门槛提高4点。如果敌人的生命值不高于斩杀门槛，直接击杀。否则，造成8点伤害。敌人每有1层标记，使伤害增加1点。"),
                Item("D10_MirrorPreview", "镜像预演（改）", "获得1层回声。获得1点充能。", 2),
                Item("D10_BurningShot", "燃烧射击（改）", "造成6点伤害。敌人每有1层标记，使伤害增加1点。获得1层过热。", 3),
                Item("D10_CoolingLoop", "冷却回路（改）", "获得5点格挡。获得1点充能。失去1层过热。", 2),
                Item("D10_IdleProgram", "空转程序（改）", "获得2点格挡。如果你有过热，敌人失去1层标记。", 4),
                Item("D10_SpikeMark", "尖刺标记（改）", "给予敌人1层标记。如果你拥有棱镜碎片，改为2层。造成5点伤害。", 3),
                Item("StrikeIronclad", "打击", "造成6点伤害。", 4),
                Item("DefendIronclad", "防御", "获得5点格挡。", 4)
            ],
            [
                Item("D10_TimePotion", "时间药水", "只能在回合开始时使用。获得6点充能。获得1层回声。第1回合起可以击杀敌人。"),
                Item("D10_EchoPotion", "回声药水", "只能在回合开始时使用。获得2层回声。第2回合起可以击杀敌人。"),
                Item("D10_ShatterArmorPotion", "碎甲药水", "只能在回合开始时使用。移除敌人最多30点保留格挡。给予敌人2层标记。第3回合起可以击杀敌人。")
            ],
            [
                Item("D10_WatchCore", "怀表核心", "每回合开始时，获得1点充能。"),
                Item("D10_Resonator", "谐振器", "每场战斗中，第一次打出延迟爆破时，其延迟伤害增加12点。"),
                Item("D10_PrismShard", "棱镜碎片", "每当你给予敌人标记时，多给予1层。敌人拥有至少4层标记时，你的攻击额外造成4点伤害。")
            ])
    };

    public static Sts2PuzzleConfig DifficultyOneConfig => Configs[1];

    public static Sts2PuzzleConfig Resolve(Sts2PuzzleConfig? backend)
    {
        var stage = backend?.StageIndex ?? 1;
        if (Configs.TryGetValue(stage, out var byStage))
        {
            return byStage;
        }

        if (!string.IsNullOrWhiteSpace(backend?.PuzzleId))
        {
            var byId = Configs.Values.FirstOrDefault(c =>
                string.Equals(c.PuzzleId, backend.PuzzleId, StringComparison.OrdinalIgnoreCase));
            if (byId != null)
            {
                return byId;
            }
        }

        return Configs[1];
    }

    public static ResourcePool ResolveResources(Sts2PuzzleConfig? backend)
    {
        return Resources[Resolve(backend).StageIndex];
    }

    public static IEnumerable<Sts2PuzzleConfig> AllConfigs => Configs.OrderBy(pair => pair.Key).Select(pair => pair.Value);

    public static ResourcePool ResourcesForStage(int stageIndex) => Resources[stageIndex];

    private static Sts2PuzzleConfig Config(
        int stage,
        string id,
        string name,
        int playerHp,
        int enemyHp,
        int minCards,
        int maxCards,
        int maxPotions,
        int maxRelics,
        string win,
        string enemyInfo,
        List<EnemyActionConfig> actions,
        List<SelectionConstraintConfig>? constraints = null)
    {
        return new Sts2PuzzleConfig
        {
            StageIndex = stage,
            StageCount = 10,
            RulesVersion = 14,
            PuzzleId = id,
            PuzzleName = name,
            PuzzleDoc = DocPath,
            WinConditionText = win,
            EnemyInfoText = enemyInfo,
            SelectionRulesText = win,
            KeyRulesText = enemyInfo,
            Player = new PlayerConfig
            {
                Character = "Ironclad",
                StartingHp = playerHp,
                MaxHp = playerHp,
                MaxEnergy = 3,
                DrawPerTurn = stage == 3 ? 6 : stage >= 9 ? 4 : 5
            },
            Enemy = new EnemyConfig
            {
                Id = id,
                Name = name,
                BaseHp = enemyHp,
                ThinDeckPenalty = new ThinDeckPenalty { MinimumCards = 0, HpPerMissingCard = 0 }
            },
            MinCards = minCards,
            MaxCards = maxCards,
            MaxPotions = maxPotions,
            MaxRelics = maxRelics,
            EnemyActions = actions,
            SelectionConstraints = constraints ?? []
        };
    }

    private static EnemyActionConfig Act(int turn, int damage, int armor = 0, bool fail = false)
    {
        return new EnemyActionConfig
        {
            Turn = turn,
            Damage = damage,
            ArmorGain = armor,
            FailIfAlive = fail,
            FailureReason = fail ? $"turn_{turn}_deadline" : "",
            Type = damage > 0 ? "attack" : "buff",
            Description = armor > 0 ? $"攻击{damage}点。回合结束获得{armor}点保留格挡。" : $"攻击{damage}点。"
        };
    }

    private static SelectionConstraintConfig Req(string description, string[] cardIds, int min = -1, int max = -1, int exact = -1)
    {
        return new SelectionConstraintConfig
        {
            Description = description,
            CardIds = cardIds,
            MinCount = min,
            MaxCount = max,
            ExactCount = exact
        };
    }

    private static ResourcePool Pool(ResourceItem[] cards, ResourceItem[]? potions = null, ResourceItem[]? relics = null)
    {
        return new ResourcePool
        {
            CardPool = cards.ToList(),
            PotionPool = (potions ?? []).ToList(),
            RelicPool = (relics ?? []).ToList()
        };
    }

    private static ResourceItem Item(string id, string name, string description, int maxCopies = 1)
    {
        return new ResourceItem { Id = id, Name = name, Description = CleanCardDescription(description), MaxCopies = maxCopies };
    }

    private static string CleanCardDescription(string description)
    {
        return string.Join(
            "\n",
            description
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => NormalizeChallengeDescriptionLine(RemoveLeadingCost(line)))
                .Where(line => line.Length > 0));
    }

    private static string NormalizeChallengeDescriptionLine(string line)
    {
        var normalized = line
            .Replace("；", "。", StringComparison.Ordinal)
            .Replace(";", "。", StringComparison.Ordinal)
            .Trim();

        var suffixes = new[]
        {
            "。不抽牌、不弃牌。",
            "。不抽牌，不弃牌。",
            "。不抽牌。",
            "。不弃牌。",
            "。不保留。",
            "。不消耗。",
            "。消耗。"
        };

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var suffix in suffixes)
            {
                if (normalized.EndsWith(suffix, StringComparison.Ordinal))
                {
                    normalized = normalized[..^suffix.Length] + "。";
                    changed = true;
                }
            }
        }

        return normalized;
    }

    private static string RemoveLeadingCost(string line)
    {
        var trimmed = line.TrimStart().Replace(" ", "", StringComparison.Ordinal).Replace("\t", "", StringComparison.Ordinal);
        var costPrefixes = new[] { "0费，", "1费，", "2费，", "3费，", "X费，", "x费，", "0费、", "1费、", "2费、", "3费、", "X费、", "x费、" };
        foreach (var prefix in costPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return trimmed[prefix.Length..];
            }
        }

        return trimmed;
    }
}
