# 难度 2 缩尺原型与家族预选

本文件不直接给出难度 2 正式题稿，而是回答一个更前置的问题：

**在当前已经验证过的难度 1 家族里，哪一条最适合继续升到难度 2？**

按要义当前定义，难度 2 的关键特征是：

1. 2 个机制交互
2. 至少 1 个敌人窗口或资源位取舍
3. 2-3 张强干扰牌会把人往伪路线引过去
4. 推荐回合深度 `1-3`
5. 目标是让玩家开始区分“看起来能用”和“算完真能用”

当前这一步先做**缩尺原型**，不直接做正式题稿。  
也就是说，先验证“机制交互和干扰形状是否对”，再决定要不要把资源规模扩到难度 2 的正式范围。

## 1. 当前最值得升难度 2 的种子家族

在现有五层题稿/题源里，最适合继续往难度 2 升的，不是默认备选的 `Twin Strike` 家族，而是第六家族的稳前沿分支：

- `Strike x6, Defend x2, Bash x1, Hemokinesis x1`

原因很直接：

1. 它已经在正式窗口 `50 HP / 8 HP / 0-9-11-13` 下成立
2. 它同时包含两个清楚的机制：
   - `Bash` 的易伤窗口
   - `Hemokinesis` 的自损换爆发
3. 它的强干扰牌质量明显高于第二家族默认备选

对应机器底稿：

- [difficulty1_hemokinesis_bash_trimmed_machine_export.json](C:/Users/Administrator/Documents/New%20project%202/difficulty1_hemokinesis_bash_trimmed_machine_export.json)
- [difficulty1_hemokinesis_bash_trimmed_robustness.json](C:/Users/Administrator/Documents/New%20project%202/difficulty1_hemokinesis_bash_trimmed_robustness.json)

## 2. 为什么不是第二家族先升

第二家族默认备选：

- `Strike x6, Defend x2, Bash x1, Twin Strike x1`

它当然更适合做难度 1 默认备选，这一点已经在主报告里定了。  
但“更适合做难度 1 备选”，不等于“更适合升到难度 2”。

第二家族的问题是：

1. 三解很清楚，但机制交互偏薄
2. 快线存在，但不够强到形成真正有诱惑力的伪最优路线
3. 头部非三解更多是在“成功率略低”层面干扰，而不是在“很像真解、但算完不成立”层面干扰

代表数据：

- `2 回合`：`0 / 28.5714 / 0 / 0 / 71.4286`
- `3 回合`：`0 / 0 / 95.2381 / 0 / 4.7619`
- `4 回合`：`0 / 0 / 0 / 71.4286 / 28.5714`

它非常适合讲“快/中/慢三条线怎么分层”，但不够适合讲“为什么强牌会骗你”。

## 3. `Hemokinesis` 缩尺原型为什么更像难度 2

第六家族稳前沿分支的代表三解是：

1. `2 回合快线`  
   `0 / 85.7143 / 10.4762 / 0 / 3.8095`
2. `3 回合主线`  
   `0 / 0 / 89.5238 / 5.7143 / 4.7619`
3. `4 回合慢线`  
   `0 / 0 / 0 / 71.4286 / 28.5714`

它比第二家族更像难度 2 原型，主要在三点：

1. `Hemokinesis` 本身就是强诱导资源  
   看起来像“显然该拿”，但自损会改变容错边界
2. 它的头部非三解更有伪路线感  
   例如：
   - `Bash, Defend x2, Hemokinesis, Strike x3`：`0 / 85.7143 / 13.8095 / 0 / 0.4762`
   - `Bash, Hemokinesis, Strike x5`：`0 / 85.7143 / 0 / 0 / 14.2857`
3. 它自然形成“爆发更高，但未必更适合当默认解”的比较张力

这正是难度 2 要的体验：

1. 玩家一眼会被强牌吸引
2. 但算完以后要分清：
   - 哪条是快线
   - 哪条是主线
   - 哪条只是伪路线或解释成本过高的路线

## 4. 这份缩尺原型目前已经证明了什么

当前能确定的，不是“难度 2 正式题已经有了”，而是：

1. `Hemokinesis + Bash` 这条家族有明确的难度 2 倾向
2. 它比第二家族更适合做“强干扰题”的种子
3. 它在缩尺版本里已经有：
   - 2 个机制交互
   - 明显敌人窗口
   - 质量足够高的伪路线

也就是说，它已经通过了“要不要继续投算力”的门槛。

## 5. 为什么它现在还不能叫难度 2 正式题稿

按要义当前定义，这份东西还只是缩尺原型，而不是正式题稿。缺的主要不是算法，而是规模：

1. 当前卡池只有 `10`
2. 还远低于难度 2 推荐的 `24-32`
3. 还没有引入更完整的干扰牌层
4. 还没有把药水池、遗物池或更多资源位取舍系统地接进状态模型

所以它现在的身份应明确写成：

**难度 2 缩尺原型种子，而不是难度 2 正式题稿。**

## 6. 当前最合理的后续扩展顺序

如果继续沿这条线往难度 2 推，最合理的顺序不是直接做大池暴力，而是：

1. 先在 `Hemokinesis + Bash` 家族附近加入 1-2 张强干扰牌  
   目标不是抬上界，而是抬“伪路线质量”
2. 再把总池从 `10` 逐步扩到 `12-14`
3. 最后再决定要不要接药水池或遗物池

当前最适合作为下一轮候选的牌型方向是：

1. 继续围绕自损爆发  
2. 或增加一张会诱导玩家高估爆发价值的单卡  
3. 但先不引入全路线通吃的通用外挂资源

## 7. 当前结论

到这一步，难度 2 的第一轮方向已经清楚了：

1. 不从第 23 节主代表直接升
2. 不从第 33 节默认备选直接升
3. 而是从第六家族稳前沿分支出发，做难度 2 缩尺原型

这条结论的意义在于：

1. 难度 1 里“最适合当默认备选”的，不一定就是难度 2 里“最适合扩成强干扰题”的
2. 题库层级和扩题方向可以不同步，必须分别判断

## 8. 第一张强干扰候选牌的验证：`Clash` 在当前原型里不成立

沿 `Hemokinesis + Bash` 方向继续推进时，我先接入了 `Clash`，原因很直接：

1. 它规则简单
2. 它看起来像典型强诱导牌
3. 它天然会制造“是不是该为了爆发全拿攻击牌”的错误直觉

本地资料库里它的描述是：

- `Can only be played if every card in your Hand is an Attack. Deal 14 damage.`

对应首轮缩尺扫描命令是：

```powershell
python .\difficulty1_basic_red_upper_bound.py --totals 10 11 12 --max-specials 4 --max-perfected 0 --max-auxiliary 3 --max-bash 1 --max-uppercut 1 --max-twin-strike 0 --max-pommel-strike 0 --max-bludgeon 0 --max-hemokinesis 1 --max-clash 1 --enemy-hp 50 --player-hp 8 --damage-seq 0,9,11,13 --top 12
```

结果很硬：

1. 总体 `accepted = 24`
2. 但这些接受候选里，**`Clash = 1` 的组数是 `0`**
3. 也就是说，所有还能成立的候选都完全不带 `Clash`

这条结果说明的不是“`Clash` 数值不够强”，而是：

1. 它和当前 `Bash + Hemokinesis + Defend` 的结构方向冲突
2. 难度 2 原型目前需要的是“高质量伪路线”
3. `Clash` 在这个窗口里更像“会把可控结构直接打坏的条件牌”，而不是高质量干扰牌

换句话说：

**`Clash` 没有把当前缩尺原型推进成更好的难度 2 候选，反而证明了这条邻域里不该优先继续投它。**

## 9. 这条结果如何影响下一步

这一步之后，难度 2 的下一张候选牌就不该再选“手牌条件过强、会直接破坏主线骨架”的方向。  
更合理的下一步应转向：

1. 不改变手牌可出性、但会改变未来资源窗口的牌  
   例如 `Headbutt` 这类顶牌/回收型资源位牌
2. 或者不引入复杂状态、但会制造更强伪路线比较的牌  
   例如另一种纯伤害诱导牌

当前更值得继续投入的方向不是“继续试 `Clash` 配比”，而是：

**开下一张更适合做高质量伪路线的干扰牌。**

## 10. 第二张候选牌的验证：`Sword Boomerang` 能活，但会把原型漂移成另一条家族

`Clash` 被排掉以后，我继续试了 `Sword Boomerang`。  
它比 `Clash` 更值得试，因为：

1. 规则简单
2. 不引入新状态
3. 在单敌场景里会和易伤形成比较自然的伤害窗口差

对应首轮扫描命令是：

```powershell
python .\difficulty1_basic_red_upper_bound.py --totals 10 11 12 --max-specials 4 --max-perfected 0 --max-auxiliary 3 --max-bash 1 --max-uppercut 1 --max-twin-strike 0 --max-pommel-strike 0 --max-sword-boomerang 1 --max-bludgeon 0 --max-hemokinesis 1 --max-clash 0 --enemy-hp 50 --player-hp 8 --damage-seq 0,9,11,13 --top 12
```

这次和 `Clash` 不一样：

1. 它不是死牌
2. 接受候选很多
3. 甚至存在 `Sword Boomerang = 1` 且 `Hemokinesis = 1` 的可接受结构

但关键问题在于，它活下来的方式不对。

当前能成立的 `Sword Boomerang + Hemokinesis` 候选是：

1. `Strike x6, Defend x2, Sword Boomerang x1, Hemokinesis x1`
2. 以及更大的若干超集

代表三解是：

1. `2 回合`：`0 / 71.4286 / 22.8571 / 0 / 5.7143`
2. `3 回合`：`0 / 0 / 95.2381 / 0 / 4.7619`
3. `4 回合`：`0 / 0 / 0 / 71.4286 / 28.5714`

但这里最值得注意的不是这组数字，而是：

1. 它已经把 `Bash` 完全挤掉了
2. 也没有保留 `Uppercut`
3. 也就是说，它活下来的方式不是“增强 `Hemokinesis + Bash` 原型”
4. 而是把原型漂移成了一条新的伤害密度家族

换句话说：

**`Sword Boomerang` 不是当前难度 2 原型附近的一张好干扰牌，它更像把你带去另一个家族。**

## 11. 这条结果如何影响下一步

这一步的意义不是“`Sword Boomerang` 不能用”，而是：

1. 它不能作为 `Hemokinesis + Bash` 原型的局部扩展牌
2. 它应该被视为另一条家族的种子，而不是当前原型的干扰补件

所以难度 2 这条线的下一步应改成：

1. 暂时不继续在当前原型附近试纯伤害密度牌
2. 优先寻找不会把 `Bash` 窗口机制直接挤掉的资源位牌或回收牌
3. 如果后续要继续研究 `Sword Boomerang`，应该单独开新家族，而不是继续挂在 `Hemokinesis + Bash` 原型下面

## 12. 第三张候选牌的验证前置：`Headbutt` 要求把跨回合状态再补硬一层

`Headbutt` 和前两张牌不一样。  
它不是单纯加伤害，也不是单纯加条件，而是会直接改写**下一次抽牌的顶部状态**。

所以在做 `Headbutt` 接入测试前，我先把求解器补成了：

1. 打出的牌在本回合内就进入弃牌堆，而不是等到回合末再统一重建
2. 抽牌状态不再只记“无序多重集合”，而是拆成：
   - 抽牌堆顶端的**有序前缀**
   - 其余未定顺序的**无序尾部**
   - 当前弃牌堆
3. `Headbutt` 的“回顶牌”会真实写入有序前缀，而不是只做总概率近似

这一步已经做过导出级验证。  
例如在 `Strike x6, Defend x2, Headbutt x1, Hemokinesis x1` 的代表解里，逐回合报告已经能直接看到：

- `Hemokinesis`
- `Headbutt[top Hemokinesis]`
- `Strike`

也就是说，这里不是只算到了“有 `Headbutt` 这张牌”，而是已经算到了**它回顶了哪张牌，以及这件事如何改变下一回合抽牌窗口**。

## 13. 第三张候选牌的验证：`Headbutt` 能活，但会把原型拆成两支

对应首轮窄扫描命令是：

```powershell
python .\difficulty1_basic_red_upper_bound.py --totals 10 --max-specials 4 --max-perfected 0 --max-auxiliary 3 --max-bash 1 --max-uppercut 1 --max-headbutt 1 --max-twin-strike 0 --max-pommel-strike 0 --max-sword-boomerang 0 --max-bludgeon 0 --max-hemokinesis 1 --max-clash 0 --enemy-hp 50 --player-hp 8 --damage-seq 0,9,11,13 --top 20
```

这轮 `10` 张邻域的统计是：

1. `pool_vectors = 77`
2. `stable_rejected = 17`
3. `missing_gradient = 42`
4. `accepted = 18`

但真正关键的是 `Headbutt` 在接受候选里的分布：

1. `Headbutt = 1` 的接受候选共有 `12` 组
2. 其中 `Headbutt = 1, Bash = 1, Hemokinesis = 1` 的接受候选是 `0` 组
3. `Headbutt = 1, Hemokinesis = 1` 的接受候选只有 `1` 组
4. `Headbutt = 1, Bash = 1` 的接受候选有 `7` 组

我又对 `11` 张邻域补了一轮同口径过滤，结果模式完全一样：

1. `Headbutt = 1` 的接受候选共有 `15` 组
2. `Headbutt = 1, Bash = 1, Hemokinesis = 1` 的接受候选仍然是 `0` 组
3. `Headbutt = 1, Hemokinesis = 1` 的接受候选仍然只有 `1` 组
4. `Headbutt = 1, Bash = 1` 的接受候选增加到 `9` 组

这说明 `Headbutt` 不是结构性坏牌。  
它能产生活候选，而且不少。

但它也不是当前 `Hemokinesis + Bash` 原型要找的那类高质量干扰牌。  
因为它从来没有把这两个核心机制同时保住，反而稳定地把原型拆成了两支：

1. `Bash + Headbutt` 分支
2. `Hemokinesis + Headbutt` 分支

代表性两支如下。

`Bash + Headbutt` 分支代表：

- 卡池：`Strike x6, Defend x2, Bash x1, Headbutt x1`
- `2 回合`：`0 / 23.8095 / 61.9048 / 0 / 14.2857`
- `3 回合`：`0 / 0 / 95.2381 / 0 / 4.7619`
- `4 回合`：`0 / 0 / 0 / 71.4286 / 28.5714`

`Hemokinesis + Headbutt` 分支代表：

- 卡池：`Strike x6, Defend x2, Headbutt x1, Hemokinesis x1`
- `2 回合`：`0 / 76.1905 / 23.3333 / 0 / 0.4762`
- `3 回合`：`0 / 0 / 95.2381 / 0 / 4.7619`
- `4 回合`：`0 / 0 / 0 / 71.4286 / 28.5714`

所以这一步的更准确结论不是“`Headbutt` 不能用”，而是：

**`Headbutt` 不是当前原型的局部扩展成功，而是一张会把原型拆成两支的分叉牌。**

## 14. 这条结果如何影响下一步

这条结果比 `Clash` 和 `Sword Boomerang` 又多了一层信息：

1. `Clash` 是结构性坏牌
2. `Sword Boomerang` 是家族漂移牌
3. `Headbutt` 则更像**原型分叉牌**

也就是说，下一步不该把 `Headbutt` 继续算作 `Hemokinesis + Bash` 原型上的成功扩展，而应该改成：

1. 暂时不把 `Headbutt` 列为当前原型的优先干扰补件
2. 如果要继续研究它，应分别开：
   - `Bash + Headbutt` 分支
   - `Hemokinesis + Headbutt` 分支
3. 当前原型附近，优先级更高的仍然是那种**既能制造资源位取舍、又不会把两个核心机制拆开**的牌

## 15. 第四张候选牌的验证：`Shrug It Off` 是第一张真正保住原型的局部扩展牌

这次我换成了 `Shrug It Off`：

- `1` 费
- `Gain 8 Block. Draw 1 card.`

这张牌的价值不在于它强，而在于它测试的是另一件事：

1. 它会制造“保命 / 过牌 / 爆发”之间的资源位取舍
2. 它不会像 `Headbutt` 那样再引入新的顶牌顺序状态
3. 它也不会像 `Sword Boomerang` 那样天然把题面往纯伤害密度家族漂

对应首轮邻域过滤里，`10` 张窗口的结果是：

1. 总体：`pool_vectors = 77`
2. `stable_rejected = 25`
3. `missing_gradient = 45`
4. `accepted = 7`

关键点不在总体数，而在 `Shrug It Off` 本身：

1. `Shrug It Off = 1` 的接受候选只有 `1` 组
2. 这唯一 `1` 组同时满足：
   - `Bash = 1`
   - `Hemokinesis = 1`
3. 也就是说，它不是坏牌，也不是漂移牌，也不是分叉牌
4. 它是**第一张真正把当前 `Hemokinesis + Bash` 原型保住的局部扩展牌**

这唯一接受候选是：

- 卡池：`Strike x6, Defend x1, Bash x1, Shrug It Off x1, Hemokinesis x1`
- `case_count = 31`
- `stable_count = 0`

代表三解是：

1. `2 回合`：`0 / 85.7143 / 13.8095 / 0 / 0.4762`
2. `3 回合`：`0 / 0 / 89.5238 / 5.7143 / 4.7619`
3. `4 回合`：`0 / 0 / 0 / 92.5926 / 7.4074`

对应机器底稿已经导出：

- [difficulty2_shrug_hemo_bash_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_shrug_hemo_bash_machine_export.json>)

## 16. 这条成功不是无条件成功：`Shrug It Off` 同时压缩了快线并收窄了安全窗

`Shrug It Off` 这次和前面三张牌的区别是：

1. 它确实保住了原型
2. 但它保住原型的方式非常激进

我对这条候选做了参数窗复核，结果是：

- `valid_cases = 24`
- `invalid_cases = 16`

对应窗口明细已经导出：

- [difficulty2_shrug_hemo_bash_robustness.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_shrug_hemo_bash_robustness.json>)

失效模式很集中：

1. `enemy_hp = 49` 时，五条伤害节奏全部直接滑成稳定线
2. `player_hp = 9` 且第 2 回合伤害降到 `8` 的若干窗口，也会滑成稳定线

另外我还补了一个更窄的 `10 / 11` 张家族检查：

1. `10` 张窗口里，唯一成立的就是这条 `Strike x6, Defend x1, Bash x1, Shrug It Off x1, Hemokinesis x1`
2. `11` 张窗口里，最优 `7` 张核心三解完全不变，只是被更大的超集包住

这说明：

1. `Shrug It Off` 不是偶然撞出来的单点
2. 但它也不是“放进去以后题更健康了”
3. 更准确的说法是：

**它是当前难度 2 原型附近第一张“局部扩展成功”的牌，但同时也是一张明显的快线压缩牌 / 窗口收缩牌。**

## 17. 这条结果如何影响下一步

现在四张牌的分型已经变成：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但伴随快线压缩与窗口收缩

所以接下来最合理的判断不是“`Shrug It Off` 就是最终答案”，而是：

1. 它证明了当前原型附近**确实存在**能保住两大核心机制的局部扩展牌
2. 但这条扩展还不够健康，快线过强，参数安全窗也不够宽
3. 下一步应继续找：
   - 同样能保住 `Bash + Hemokinesis`
   - 但不会把 `2 回合` 压到接近稳定
   - 也不会在 `49 HP` 这类邻近血量窗直接滑成稳定线

## 18. 第五张候选牌的验证：`Battle Trance` 不是坏牌，但会直接把原型压穿成稳定线

这次我接的是 `Battle Trance`：

- `0` 费
- `Draw 3 cards.`
- `You cannot draw additional cards this turn.`

它比 `Shrug It Off` 更值得测的一点是：

1. 它不会改写牌堆顺序
2. 它也不会增加额外资源区
3. 但它会显著提高回合内命中率，同时自带“本回合不能再抽”的刹车

也就是说，它很适合用来判断：

**当前原型附近到底缺的是“抽牌密度”本身，还是缺一种更健康的抽牌方式。**

为了接这张牌，我把求解器又补了一层：

1. `midturn_draw_states` 现在支持 `Draw 3`
2. 回合状态里新增了 `can_draw`
3. `Battle Trance` 结算后，本回合后续的 `Pommel Strike` / `Shrug It Off` 这类抽牌会被正确压成 `0`

## 19. `Battle Trance` 的结论：它不是局部扩展成功，而是稳定线生成牌

先看首轮 `10` 张邻域过滤：

1. 总体：`pool_vectors = 77`
2. `stable_rejected = 35`
3. `missing_gradient = 36`
4. `accepted = 6`

但这 `6` 组接受候选里，**带 `Battle Trance` 的是 `0` 组**。

如果只看到这一步，还不能区分它是：

1. 结构性坏牌
2. 还是因为太强而被稳定线筛掉

所以我又补了一轮更窄的定向家族检查，只看：

- `Bash = 1`
- `Hemokinesis = 1`
- `Battle Trance = 1`
- `Uppercut = 0/1`
- `10 / 11` 张总池

这轮结果非常硬：

1. `10` 张窗口下，符合这一定义的候选共有 `7` 组
2. `7 / 7` 全部出现稳定线
3. `11` 张窗口下，符合这一定义的候选共有 `9` 组
4. `9 / 9` 全部出现稳定线

也就是说，`Battle Trance` 不是像 `Clash` 那样“根本活不下来”，而是相反：

**它在当前原型附近太容易活得过头，直接把题压穿成稳定线。**

代表性稳定线样例是：

- 卡池：`Strike x6, Defend x1, Bash x1, Hemokinesis x1, Battle Trance x1`
- 代表解：`Bash, Battle Trance, Defend, Hemokinesis, Strike, Strike, Strike`
- 精确结果：`0 / 100 / 0 / 0 / 0`

对应机器底稿已导出：

- [difficulty2_battle_trance_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_battle_trance_stable_example.json>)

## 20. 这条结果如何影响下一步

现在五张牌的分型已经进一步收敛：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌

这一步的意义在于：

1. 当前原型附近并不缺“单纯更高抽牌密度”
2. 纯抽密度一旦给太足，会直接把 `Bash + Hemokinesis` 组合推成稳定线
3. 所以下一步不该继续优先试 `Battle Trance` 这类高抽密度牌，而应优先寻找：
   - 抽牌更少
   - 或附带更真实代价
   - 或虽然能改善主线，但不会把 `2 回合` 直接压成 `100%` 的资源位牌

## 21. 第六张候选牌的验证：`Pommel Strike` 比 `Battle Trance` 轻，但本质问题没变

接着我测了 `Pommel Strike`：

- `1` 费
- `Deal 9 damage. Draw 1 card.`

这张牌的价值在于，它是比 `Battle Trance` 更轻的一种抽牌密度牌：

1. 抽牌只有 `1`
2. 本身还要占 `1` 点能量
3. 还必须先以攻击牌形态进入路线，而不是纯技能加速

如果这张牌也在当前原型附近失控，那就说明问题不是“抽 3 太夸张”，而是：

**这个窗口本身对“带攻击的抽牌密度”容忍度就很低。**

## 22. `Pommel Strike` 的结论：它不是坏牌，但仍然属于稳定线生成方向

先看首轮 `10` 张邻域过滤：

1. 总体：`pool_vectors = 77`
2. `stable_rejected = 35`
3. `missing_gradient = 36`
4. `accepted = 6`

和 `Battle Trance` 一样，接受候选里：

- `Pommel Strike = 1` 的组数是 `0`

然后我补了更窄的定向家族检查，只看：

- `Bash = 1`
- `Hemokinesis = 1`
- `Pommel Strike = 1`
- `Uppercut = 0/1`
- 总池 `10 / 11`

这轮结果说明它和 `Battle Trance` 有一个差异，也有一个共同点。

差异是：

1. 它不像 `Battle Trance` 那样几乎所有头部线都直接 `100%`
2. 它会出现“非常接近稳定”的快线，例如：
   - `0 / 98.7302 / 1.2698 / 0 / 0`

共同点是更关键的那部分：

1. `10` 张窗口里，这个定向家族的候选全部都会被稳定线筛掉
2. `11` 张窗口里，结果还是一样
3. 也就是说，**它虽然比 `Battle Trance` 轻，但在当前原型附近仍然系统性地把题面往稳定线推**

最强代表性稳定样例是：

- 卡池：`Strike x5, Defend x1, Bash x1, Hemokinesis x1, Pommel Strike x1, Uppercut x1`
- 代表解：`Bash, Defend, Hemokinesis, Pommel Strike, Strike, Strike, Strike, Uppercut`
- 精确结果：`0 / 100 / 0 / 0 / 0`

对应机器底稿已导出：

- [difficulty2_pommel_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_pommel_stable_example.json>)

## 23. 这条结果如何影响下一步

这一步把“抽牌密度牌”的边界钉得更实了：

1. `Battle Trance` 证明了大剂量纯抽牌会直接压穿窗口
2. `Pommel Strike` 证明了**轻量的攻击 + 抽牌**在这个窗口里也不安全

所以当前难度 2 原型附近，不该再把下面这类牌当高优先级方向：

1. 高抽量纯抽牌
2. 轻量攻击 + 抽牌密度牌

这比上一轮的结论更硬，因为现在不是只排掉一张 `Battle Trance`，而是已经开始排掉一个更一般的牌型方向。  
下一步真正更值的是去找：

1. 有真实代价
2. 不主要靠提升抽牌密度
3. 但仍可能改变 `Bash + Hemokinesis` 资源位取舍

的下一类候选牌。

## 24. 第七张候选牌的验证：`Bloodletting` 确实保住了原型，但会把题推成高代价爆发线

这次我换成了 `Bloodletting`：

- `0` 费
- `Lose 3 HP. Gain 2 Energy.`

这张牌比前面的抽牌密度牌更接近我现在真正想测的方向：

1. 它有真实代价
2. 它不提升抽牌密度
3. 它直接改变的是**资源位**，不是命中率

也就是说，它是在测试：

**当前原型附近，能不能用“高代价加速”来保住两大核心机制，而不是用抽牌密度把题压穿。**

## 25. `Bloodletting` 的结论：它不是坏牌，也不是漂移牌，而是高代价加速牌

先看 `10` 张邻域过滤：

1. 总体：`pool_vectors = 77`
2. `stable_rejected = 17`
3. `missing_gradient = 40`
4. `accepted = 20`

这次和前面的几张牌都不一样：

1. `Bloodletting = 1` 的接受候选有 `14` 组
2. 其中 `Bash = 1, Hemokinesis = 1, Bloodletting = 1` 的接受候选有 `1` 组

也就是说，`Bloodletting` 不只是能活，它**确实保住了当前原型**。

这条唯一的原型保留候选是：

- 卡池：`Strike x3, Defend x4, Bash x1, Bloodletting x1, Hemokinesis x1`
- `case_count = 37`
- `stable_count = 0`

代表三解是：

1. `2 回合`：`0 / 96.1905 / 3.3333 / 0 / 0.4762`
2. `3 回合`：`0 / 0 / 68.1429 / 18.2143 / 13.6429`
3. `4 回合`：`0 / 0 / 0 / 7.2286 / 92.7714`

对应机器底稿已导出：

- [difficulty2_bloodletting_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_bloodletting_machine_export.json>)

## 26. 这条成功同样不健康：它不是稳扩展，而是高代价爆发扩展

这条候选的问题不在“能不能成”，而在它成得太偏：

1. `2 回合` 已经接近稳定
2. `3 回合` 主线不够稳
3. `4 回合` 慢线几乎塌掉，只剩 `7.2286%`

这说明 `Bloodletting` 和 `Shrug It Off` 一样，都保住了原型，但保住的方式完全不同：

- `Shrug It Off` 是把慢线和容错往上抬，代价是快线压缩、窗口收缩
- `Bloodletting` 则是把快线几乎顶满，代价是中慢线结构被拉坏

我还给一个更窄的 trimmed 代表做了参数窗复核，结果是：

- `valid_cases = 18`
- `invalid_cases = 22`

对应明细已导出：

- [difficulty2_bloodletting_trimmed_robustness.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_bloodletting_trimmed_robustness.json>)

失效模式也很明确：

1. 一旦窗口稍微软下来，例如 `0/8/10/12`
2. 或敌人血量进一步降低
3. 很容易直接滑成稳定线

所以这一步的更准确结论不是“`Bloodletting` 终于是答案”，而是：

**`Bloodletting` 证明了“高代价加速”这条资源位方向可以保住原型，但目前给出来的是一条明显偏爆发、偏脆的扩展。**

## 27. 这条结果如何影响下一步

现在原型附近的方向已经开始分层了：

1. 抽牌密度方向：
   - `Battle Trance`
   - `Pommel Strike`
   - 结论：整体风险过高，容易直接压穿稳定线阈值
2. 回收/顶牌方向：
   - `Headbutt`
   - 结论：容易把原型拆成分支
3. 纯伤害密度方向：
   - `Sword Boomerang`
   - 结论：容易漂移成新家族
4. 资源位真实代价方向：
   - `Shrug It Off`
   - `Bloodletting`
   - 结论：这是目前最接近“能保住原型”的方向，但一个偏容错，一个偏爆发，都还不够健康

所以下一步最值的不再是继续扫抽牌牌型，而是继续沿“真实代价资源位牌”往下试，目标是找：

1. 能保住 `Bash + Hemokinesis`
2. 不会把 `2 回合` 顶到接近稳定
3. 也不会像 `Bloodletting` 一样把 `4 回合` 慢线彻底打塌

## 28. 第八张候选牌的验证：`Rage` 在当前原型邻域里不成立

这次我接入并验证了 `Rage`：
- `0` 费
- `Whenever you play an Attack this turn, gain 3 Block.`

它比前面的抽牌密度牌更值得试的原因很直接：

1. 它不增加抽牌密度
2. 它不改抽牌堆顺序
3. 它直接改变的是“先下技能、再连打攻击”的价值

也就是说，它测试的是另一条方向：
**能不能在不抬命中率的前提下，用同回合的攻击触发防御，把 `Bash + Hemokinesis` 原型变得更健康。**

先看 `10` 张邻域过滤：

1. `pool_vectors = 77`
2. `stable_rejected = 29`
3. `missing_gradient = 42`
4. `accepted = 6`

关键点不在总体数，而在 `Rage` 本身：

1. `Rage = 1` 的接受候选是 `0` 组
2. `Rage = 1, Bash = 1, Hemokinesis = 1` 的接受候选也是 `0` 组

这说明它不是像 `Shrug It Off` 或 `Bloodletting` 那样，至少还能在当前原型上留下一条可接受前沿。

## 29. 定向家族复核：`Rage` 不是健康扩展，而是“稳定线生成 / 慢线抹除”方向

我又补做了更窄的定向家族检查，只看：

- `Bash = 1`
- `Hemokinesis = 1`
- `Rage = 1`
- `Uppercut = 0/1`
- 总池 `10 / 11`

结果是：

1. `10` 张窗口：`pool_count = 7`
   - `stable_rejected = 6`
   - `missing_gradient = 1`
   - `accepted = 0`
2. `11` 张窗口：`pool_count = 9`
   - `stable_rejected = 8`
   - `missing_gradient = 1`
   - `accepted = 0`

也就是说，`Rage` 在这条原型附近不是“稍微调一调就能成”的牌，而是会稳定落到两种失败之一：

1. 大多数结构直接滑成稳定线
2. 少数边界结构不再稳定，但会把 `4 回合` 慢线直接抹掉

代表性稳定线样例是：

- 卡池：`Strike x3, Defend x3, Bash x1, Uppercut x1, Hemokinesis x1, Rage x1`
- 代表解：`Bash, Defend, Hemokinesis, Rage, Strike, Strike, Uppercut`
- 精确结果：`0 / 98.0952 / 1.9048 / 0 / 0`

对应机器底稿：

- [difficulty2_rage_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_rage_stable_example.json>)

唯一没滑成稳定线的边界例子也不健康：

- 卡池：`Strike x6, Defend x1, Bash x1, Hemokinesis x1, Rage x1`
- 代表解：`Bash, Hemokinesis, Rage, Strike x6`
- 精确结果：`0 / 61.1111 / 19.8413 / 0 / 19.0476`

它的问题不是成功率太低，而是：

1. `2 回合` 已经偏强
2. `3 回合` 仍有一定质量
3. 但 `4 回合` 慢线直接消失

对应机器底稿：

- [difficulty2_rage_missing_gradient_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_rage_missing_gradient_example.json>)

所以这一步的更准确结论不是“`Rage` 不能活”，而是：

**`Rage` 继续证明了“攻击触发防御”这条牌型方向，在当前窗口里会把题面推向过强保命端：要么生成稳定线，要么抹掉慢线。**

这和前面几张牌一起，已经把当前原型附近的失败类型继续补全了：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线

这一步的意义不只是又排掉一张牌，而是把一整个方向也一起排掉了。下一步更值得试的，不该再是会普遍抬高同回合生存窗口的牌，而是仍然改变资源位取舍、但不会系统性放大保命能力的扩展牌。

## 30. 第九张候选牌的验证：`Body Slam` 不是坏牌，而且能保住池级原型

这次我接的是 `Body Slam`：
- `1` 费
- `Deal damage equal to your Block.`

它比前几张牌更值得试的一点在于：

1. 它不增加抽牌密度
2. 它不放大同回合生存能力本身
3. 它改的是“防御资源能不能转成伤害”的资源位取舍

先看 `10` 张邻域过滤：

1. `pool_vectors = 77`
2. `stable_rejected = 10`
3. `missing_gradient = 56`
4. `accepted = 11`

这次和前面几张牌不一样，`Body Slam` 本身是能活下来的：

1. `Body Slam = 1` 的接受候选有 `5` 组
2. 其中 `Body Slam = 1, Bash = 1, Hemokinesis = 1` 的接受候选有 `3` 组

也就是说，它不是结构性坏牌，不是稳定线生成牌，也不是一接进来就把原型整块挤掉。

当前最强的保原型候选是：

- 卡池：`Strike x5, Defend x2, Bash x1, Hemokinesis x1, Body Slam x1`
- `case_count = 36`
- `stable_count = 0`

三条代表线是：

1. `2 回合`：`0 / 85.7143 / 10.4762 / 0 / 3.8095`
2. `3 回合`：`0 / 0 / 89.5238 / 5.7143 / 4.7619`
3. `4 回合`：`0 / 0 / 0 / 71.4286 / 28.5714`

对应机器底稿和鲁棒性明细已经导出：

- [difficulty2_body_slam_hemo_bash_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_body_slam_hemo_bash_machine_export.json>)
- [difficulty2_body_slam_hemo_bash_robustness.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_body_slam_hemo_bash_robustness.json>)

参数窗复核结果也不差：

- `valid_cases = 32`
- `invalid_cases = 8`

单看这些数，它已经比前几张牌更接近“可讨论扩展”。

## 31. 但它仍然不是健康扩展：`Body Slam` 会让中慢线系统性脱核

`Body Slam` 这次真正重要的，不是它能活，而是它怎么活。

我把 `Body Slam + Bash + Hemokinesis` 的 `3` 组接受候选逐个拆开看了，结果非常一致：

1. `2 回合` 最优线都会用到 `Bash + Body Slam + Hemokinesis`
2. `3 回合` 最优线会系统性丢掉 `Body Slam` 和 `Hemokinesis`
3. `4 回合` 最优线会进一步退回到纯 `Defend + Strike`，有时连 `Bash` 也一起丢掉

也就是说，它保住的是：
- **卡池层的原型**

但它没有保住：
- **解法层的原型**

这和前面几张牌都不一样。它既不是：

1. `Sword Boomerang` 那种一眼就漂成新家族
2. `Headbutt` 那种把原型拆成两支
3. `Shrug It Off` 那种三条线都还围着原型打转

它更准确的分类是：

**主线脱核牌**

含义是：
- 新牌和原型能在同一个池里共存
- 最快线会用到它，甚至也会保住原型核心件
- 但中线和慢线的最优解会系统性把新增机制、甚至把原型本身的部分核心件丢掉

这说明 `Body Slam` 不是没有价值，而是：
- 它更像把一条快线拼接到原型上
- 但没有把整个三解结构一起往更健康的方向推

它还有一条更明显的漂移版本：

- 卡池：`Strike x3, Defend x5, Hemokinesis x1, Body Slam x1`
- 鲁棒性：`valid_cases = 18`、`invalid_cases = 22`
- `4 回合` 慢线均值只有 `42.7714%`

对应明细：

- [difficulty2_body_slam_drift_robustness.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_body_slam_drift_robustness.json>)

所以这一步的更准确结论不是“`Body Slam` 可以进当前原型”，而是：

**`Body Slam` 证明了“防御转伤害”这条方向可以保住池级原型，但它会让中慢线脱核，暂时不该算作健康的局部扩展成功。**

到这里，当前原型附近的牌已经开始出现更细的分型：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线
9. `Body Slam`：主线脱核牌

这一步的意义在于：现在不只是知道“哪张牌不行”，而是开始区分“池级保原型”和“解法级保原型”不是一回事。后面再试新牌，不能只看它有没有保住卡池结构，还要看三条代表线是不是还围着同一个核心机制在运转。

## 32. 第十张候选牌的验证：`Thunderclap` 会把原型压成更轻的 Vulnerable 漂移家族

原本这轮我优先想试的是 `Seeing Red`。但当前本地两份 STS2 卡牌资料里都没有对应条目，所以我没有继续拿缺证据的牌硬做，而是切到了本地双源都能核到的 `Thunderclap`：

- `1` 费
- `Deal 4 damage and apply 1 Vulnerable to ALL enemies.`

它值得测的原因很明确：

1. 它不加抽牌密度
2. 它不放大同回合保命
3. 它直接测试“更轻的 Vulnerable 压缩件”会不会把 `Bash + Hemokinesis` 原型挤坏

先看 `10` 张邻域过滤：

1. `pool_vectors = 77`
2. `stable_rejected = 17`
3. `missing_gradient = 47`
4. `accepted = 13`

关键分布是：

1. `Thunderclap = 1` 的接受候选有 `7` 组
2. `Thunderclap = 1, Bash = 1, Hemokinesis = 1` 的接受候选是 `0` 组
3. `Thunderclap = 1, Hemokinesis = 1` 的接受候选也是 `0` 组
4. `Thunderclap = 1, Bash = 1` 的接受候选有 `3` 组

也就是说，它不是坏牌，但它在当前原型附近根本不保 `Hemokinesis`，连保 `Bash + Hemokinesis` 的边缘前沿都没有。

## 33. 定向家族复核：`Thunderclap` 在原型邻域里直接滑成稳定线

我又补做了更窄的定向家族检查，只看：

- `Thunderclap = 1`
- `Hemokinesis = 1`
- `Bash = 0/1`
- `Uppercut = 0/1`
- 总池 `10 / 11`

结果是：

1. `10` 张窗口：`pool_count = 16`
   - `stable_rejected = 13`
   - `missing_gradient = 3`
   - `accepted = 0`
2. `11` 张窗口：`pool_count = 20`
   - `stable_rejected = 17`
   - `missing_gradient = 3`
   - `accepted = 0`

代表性稳定样例是：

- 卡池：`Strike x3, Defend x3, Bash x1, Thunderclap x1, Uppercut x1, Hemokinesis x1`
- 代表解：`Bash, Defend, Hemokinesis, Strike, Strike, Thunderclap, Uppercut`
- 精确结果：`0 / 100 / 0 / 0 / 0`

对应机器底稿：

- [difficulty2_thunderclap_hemo_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_thunderclap_hemo_stable_example.json>)

这说明只要它还挂在 `Hemokinesis` 原型旁边，题面就会被直接压穿。

## 34. 但它自己能活：只是已经漂到了“更轻 Vulnerable 包”家族

`Thunderclap` 真正的问题不是“不能用”，而是它只能靠漂移活。

当前最强漂移候选是：

- 卡池：`Strike x6, Defend x2, Thunderclap x1, Uppercut x1`

代表三解：

1. `2 回合`：`0 / 47.6190 / 35.2381 / 0 / 17.1429`
2. `3 回合`：`0 / 0 / 95.2381 / 0 / 4.7619`
3. `4 回合`：`0 / 0 / 0 / 71.4286 / 28.5714`

对应机器底稿和鲁棒性明细：

- [difficulty2_thunderclap_drift_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_thunderclap_drift_machine_export.json>)
- [difficulty2_thunderclap_drift_robustness.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_thunderclap_drift_robustness.json>)

它的鲁棒性结果是：

- `valid_cases = 24`
- `invalid_cases = 16`

这说明它不是劣质结构，但它已经明显不属于当前 `Bash + Hemokinesis` 原型。

所以这一步的更准确结论不是“`Thunderclap` 是普通漂移牌”，而是：

**`Thunderclap` 是核心机制压缩漂移牌。**

含义是：

1. 它不是换了一个全新 payoff 才活下来
2. 它是用一个更轻、更便宜、功能部分重叠的组件
3. 把原型里的重件核心机制先压冗余，再把整条原型路线挤掉

在这里，被压冗余的是：

- `Bash` 的 Vulnerable 压力位

而被连带挤掉的是：

- `Hemokinesis` 这条原型 payoff 线

所以现在这块原型附近的分型又更细了一层：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线
9. `Body Slam`：主线脱核牌
10. `Thunderclap`：核心机制压缩漂移牌

这一步的价值在于：现在不只是知道“轻量替代件会漂移”，而是开始能指出它到底是怎么漂的。后面再试新牌时，凡是会用更轻的重叠组件把原型核心位直接压冗余的，都不该再被误记成原型上的局部扩展成功。

## 35. 第十一张候选牌的验证：`Anger` 会把原型直接压穿成跨回合增殖稳定线

这次我接的是 `Anger`：
- `0` 费
- `Deal 6 damage. Add a copy of this card into your Discard Pile.`

它和前面几张牌的关键区别在于：

1. 它不加抽牌密度
2. 它不抬同回合保命
3. 但它会在战斗中真实改变弃牌堆规模

也就是说，它测试的是另一条更底层的方向：
**如果新牌会自我增殖，当前原型会不会因为跨回合资源结构被改写而直接失控。**

先看 `10` 张邻域过滤：

1. `pool_vectors = 44`
2. `stable_rejected = 2`
3. `missing_gradient = 30`
4. `accepted = 12`

关键分布比前面更硬：

1. `Anger = 1` 的接受候选有 `12` 组
2. `Anger = 1, Bash = 1, Hemokinesis = 1` 的接受候选是 `0`
3. `Anger = 1, Hemokinesis = 1` 的接受候选也是 `0`
4. `Anger = 1, Bash = 1` 的接受候选有 `5` 组

也就是说，`Anger` 不是坏牌，而且它非常能活；但它完全不保 `Hemokinesis` 原型。

## 36. 定向家族复核：`Anger + Hemokinesis` 在 `10` 张窗口里是 `16 / 16` 全部稳定线淘汰

我补做了最窄的一层定向家族检查，只看：

- `Anger = 1`
- `Hemokinesis = 1`
- `Bash = 0/1`
- `Uppercut = 0/1`
- 总池固定 `10`

结果是：

1. `pool_count = 16`
2. `stable_rejected = 16`
3. `missing_gradient = 0`
4. `accepted = 0`

也就是说，不是“有些结构稳定、有些结构缺梯度”，而是：

**只要 `Anger` 还挂在 `Hemokinesis` 原型旁边，这整块邻域都会被直接压成稳定线。**

代表性稳定样例是：

- 卡池：`Strike x3, Defend x3, Anger x1, Hemokinesis x1, Bash x1, Uppercut x1`
- 代表解：`Anger, Bash, Defend, Hemokinesis, Strike, Strike, Strike, Uppercut`
- 精确结果：`0 / 100 / 0 / 0 / 0`

对应机器底稿：

- [difficulty2_anger_hemo_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_anger_hemo_stable_example.json>)

## 37. 它自己能活，但活法已经是另一条跨回合增殖家族

`Anger` 自己不是不能出接受候选，反而能出很多。只是这些候选已经完全不属于当前原型。

当前最强代表漂移候选是：

- 卡池：`Strike x5, Defend x2, Anger x1, Bash x1, Uppercut x1`

代表三解：

1. `2 回合`：`0 / 96.1905 / 0 / 0 / 3.8095`
2. `3 回合`：`0 / 0 / 94.5238 / 0.7143 / 4.7619`
3. `4 回合`：`0 / 0 / 0 / 71.4286 / 28.5714`

对应机器底稿：

- [difficulty2_anger_drift_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_anger_drift_machine_export.json>)

完整 `40` 点鲁棒性扫描这次过慢，我先做了更相关的缩窗复核：

- `enemy_hp = 49/50/51/52`
- `player_hp = 8`
- `damage_sequence = 0/9/11/13, 0/9/10/12, 0/8/10/12, 0/8/11/13`

结果是：

- `valid_cases = 12`
- `invalid_cases = 4`

对应明细：

- [difficulty2_anger_drift_reduced_robustness.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_anger_drift_reduced_robustness.json>)

## 38. 这次的准确分类：`Anger` 不是普通漂移，而是增殖压穿牌

`Anger` 和 `Thunderclap` 不一样。

- `Thunderclap` 是用更轻的重叠件，把原型里的中间支点机制压冗余
- `Anger` 则不是压缩已有中间件，而是通过**跨回合自我增殖**，把原型原本的牌堆节奏整个改写掉

所以这一步的更准确结论是：

**`Anger` 是增殖压穿牌。**

含义是：

1. 它不是靠保命端放大失控
2. 不是靠抽牌密度压穿
3. 也不是靠更轻的重叠件替换原型支点
4. 它是靠“战斗中真实新增牌拷贝”，把跨回合资源结构整体改写，然后把原型直接压成稳定线或漂向另一条增殖家族

到这里，当前原型附近的分型又更细了一层：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线
9. `Body Slam`：主线脱核牌
10. `Thunderclap`：核心机制压缩漂移牌
11. `Anger`：增殖压穿牌

这一步的价值在于：现在不只是知道“会新增牌拷贝的牌有风险”，而是已经把它和抽牌密度、保命放大、重叠件压缩区分开了。后面只要出现战斗中动态改写总牌量的牌，都要优先按这条口径审它。

## 39. 第十二张候选牌的验证：`Iron Wave` 在当前原型窗口下整体不成立

这次我没有再接新状态系统，而是回到一张本地双源都能核到、而且求解器早就支持的牌：`Iron Wave`

- `1` 费
- `Gain 5 Block. Deal 5 damage.`

它值得补做的原因很直接：

1. 它不加抽牌密度
2. 它不改总牌量
3. 但它把“保命 + 输出”压在同一张牌里，是典型的混合攻防件

这正好能检验：在当前难度 2 原型附近，混合攻防件到底是“可用的小修补”，还是会直接把题压坏。

## 40. 邻域过滤结论：`Iron Wave` 在这块邻域里没有任何接受候选

我做的是最相关的一块窄邻域过滤：

- 固定 `Iron Wave = 1`
- `Strike 3-7`
- `Defend 1-5`
- `Bash = 0/1`
- `Uppercut = 0/1`
- `Hemokinesis = 0/1`
- 总池固定 `10`

结果是：

1. `pool_vectors = 34`
2. `stable_rejected = 29`
3. `missing_gradient = 5`
4. `accepted = 0`

也就是说，在这整块邻域里：

- 它不会给出任何能通过筛选的前沿
- 大多数结构直接滑成稳定线
- 少数边界结构甚至连完整 `2/3/4` 梯度都长不出来

## 41. 原型定向复核：`Iron Wave + Hemokinesis` 不是脆前沿，而是整块邻域直接塌

我又补做了定向家族复核，只看：

- `Iron Wave = 1`
- `Hemokinesis = 1`
- `Bash = 0/1`
- `Uppercut = 0/1`
- 总池固定 `10`

结果是：

1. `pool_count = 16`
2. `stable_rejected = 14`
3. `missing_gradient = 2`
4. `accepted = 0`

而且那 `2` 组不是“差一点能成”，而是连缩窗都过不了。

我又把其中一个最接近边界的缺梯度家族抽出来，做了更相关的缩窗复核：

- `enemy_hp = 49/50/51/52`
- `player_hp = 8`
- `damage_sequence = 0/9/11/13, 0/9/10/12, 0/8/10/12, 0/8/11/13`

结果是：

- `valid_cases = 0`
- `invalid_cases = 16`

所以这条线不是“局部脆”，而是当前正式窗口附近整体不成立。

代表性稳定样例是：

- 卡池：`Strike x3, Defend x3, Iron Wave x1, Hemokinesis x1, Bash x1, Uppercut x1`
- 代表解：`Bash, Defend, Hemokinesis, Iron Wave, Strike, Strike, Uppercut`
- 精确结果：`0 / 98.0952 / 1.9048 / 0 / 0`

对应机器底稿：

- [difficulty2_iron_wave_hemo_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_iron_wave_hemo_stable_example.json>)

## 42. 这次的准确分类：`Iron Wave` 不是普通稳定线牌，而是混合攻防稳定器

`Iron Wave` 和 `Rage`、`Shrug It Off` 不一样。

- `Rage` 是通过同回合攻击触发防御放大保命端
- `Shrug It Off` 是局部扩展成功，但会压快线和收窗口
- `Iron Wave` 则是把“保命 + 输出”预先捆在一张轻件里，结果不是修出更好的三解，而是让大量原型分支在不付出额外结构成本的情况下同时变稳

所以这一步的更准确结论是：

**`Iron Wave` 是混合攻防稳定器。**

含义是：

1. 它不是单纯的保命件
2. 不是单纯的伤害件
3. 它的问题恰恰在于两者一起给了，而且费率又轻
4. 在当前窗口里，这会系统性抬高太多分支的同时可行性，最终把题面压向稳定线或把慢线直接删空

到这里，当前原型附近的分型又补了一层：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线
9. `Body Slam`：主线脱核牌
10. `Thunderclap`：核心机制压缩漂移牌
11. `Anger`：增殖压穿牌
12. `Iron Wave`：混合攻防稳定器

这一步的价值在于：现在连“轻量混合攻防件”也不再只是经验判断，而是已经有了独立分类。后面只要出现类似“轻费同时给伤害和防御”的牌，都应该优先按这条口径审它，而不是先假设它只是温和修补件。

## 43. 第十三张候选牌的验证：`Clothesline` 在当前原型窗口里是整块 `34 / 34` 全稳定

这次我补测的是 `Clothesline`：

- `2` 费
- `Deal 12 damage. Apply 2 Weak.`

它和前面的 `Iron Wave`、`Rage` 又不一样：

1. 它不加抽牌密度
2. 它不直接给格挡
3. 它也不改总牌量
4. 但它会通过 `Weak` 直接改写后续回合的敌人伤害窗口

也就是说，这次测试的是：
**如果新牌主要通过“后续回合减伤”抬高容错，当前原型会不会直接整块失控。**

我做的是最相关的窄邻域过滤：

- 固定 `Clothesline = 1`
- `Strike 3-7`
- `Defend 1-5`
- `Bash = 0/1`
- `Uppercut = 0/1`
- `Hemokinesis = 0/1`
- 总池固定 `10`

结果非常干净：

1. `pool_vectors = 34`
2. `stable_rejected = 34`
3. `missing_gradient = 0`
4. `accepted = 0`

也就是说，这不是“有些结构会稳定、有些结构会缺梯度”，而是：

**在当前正式窗口附近，这整块 `Clothesline` 邻域全部都会被压成稳定线。**

代表性稳定样例是：

- 卡池：`Strike x3, Defend x3, Clothesline x1, Bash x1, Uppercut x1, Hemokinesis x1`
- 代表解：`Bash, Clothesline, Hemokinesis, Strike, Strike, Strike, Uppercut`
- 精确结果：`0 / 98.0952 / 1.9048 / 0 / 0`

对应机器底稿：

- [difficulty2_clothesline_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_clothesline_stable_example.json>)

## 44. 这次的准确分类：`Clothesline` 不是普通 debuff 牌，而是后续减伤稳定器

`Clothesline` 和 `Rage`、`Iron Wave` 的差别在于：

- `Rage` 是同回合攻击触发防御放大
- `Iron Wave` 是轻费混合攻防件
- `Clothesline` 则不直接给玩家格挡，而是把“敌人后续回合伤害下降”变成了几乎全邻域都能吃到的稳定器

所以这一步的更准确结论是：

**`Clothesline` 是后续减伤稳定器。**

含义是：

1. 它不是靠当前回合多挡几点活下来
2. 它是通过削弱敌人后续回合输出，把大量本来会失败的原型分支一起抬进可行区
3. 结果不是多出健康梯度，而是整块家族同时滑向稳定线

到这里，当前原型附近的分型又补了一层：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线
9. `Body Slam`：主线脱核牌
10. `Thunderclap`：核心机制压缩漂移牌
11. `Anger`：增殖压穿牌
12. `Iron Wave`：混合攻防稳定器
13. `Clothesline`：后续减伤稳定器

这一步的价值在于：现在连“不给格挡、只靠 Weak 压后续回合”的牌也有了独立分类。后面只要出现主要靠后续减伤去抬容错的牌，都应该先按这条口径审，而不是先假设它只是温和控制牌。

## 45. 第十四张候选牌的验证：`Burning Pact` 不保 `Bash + Hemokinesis` 原型，只会漂到更薄的 `Hemokinesis` 家族

这次我补测的是 `Burning Pact`：

- `1` 费
- `Exhaust 1 card. Draw 2 cards.`

它和前面的 `Battle Trance`、`Pommel Strike`、`Headbutt` 都不一样：

1. 它确实会提高当回合命中率
2. 但它不是单纯加抽牌密度
3. 它还会主动在战斗中删掉一张当前资源
4. 所以它测试的是另一条更危险的方向：**靠主动变薄去重写后续回合的资源结构**

我先做了最相关的窄邻域过滤：

- 固定 `Burning Pact = 1`
- `Strike 3-7`
- `Defend 1-5`
- `Bash = 0/1`
- `Uppercut = 0/1`
- `Hemokinesis = 0/1`
- 总池固定 `10`

结果是：

1. `pool_vectors = 34`
2. `stable_rejected = 11`
3. `missing_gradient = 21`
4. `accepted = 2`
5. `accepted_with_bash_hemo = 0`

也就是说，`Burning Pact` 不是一接进来就整块失控，但它也**完全不保当前 `Bash + Hemokinesis` 原型**。

我又做了更窄的原型定向复核，只看：

- 固定 `Burning Pact = 1`
- 固定 `Hemokinesis = 1`
- `Bash = 0/1`
- `Uppercut = 0/1`
- 总池 `10 / 11`

结果更清楚：

1. `10` 张窗口：`pool_count = 16`，`stable_rejected = 11`，`missing_gradient = 3`，`accepted = 2`
2. `11` 张窗口：`pool_count = 18`，`stable_rejected = 14`，`missing_gradient = 2`，`accepted = 2`
3. 这 `4` 个接受候选全部都**不带 `Bash`，也不带 `Uppercut`**

也就是说，它活下来的方式不是保住原型，而是把原型里的 Vulnerable 中间支点整体剥掉，只留下更薄的 `Hemokinesis` 线。

代表性稳定样例是：

- 卡池：`Strike x3, Defend x3, Burning Pact x1, Hemokinesis x1, Bash x1, Uppercut x1`
- 代表解：`Bash, Burning Pact, Defend, Hemokinesis, Strike, Strike, Uppercut`
- 精确结果：`0 / 98.4127 / 1.5873 / 0 / 0`

对应机器底稿：

- [difficulty2_burning_pact_hemo_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_burning_pact_hemo_stable_example.json>)

漂移后的代表候选是：

- 卡池：`Strike x5, Defend x4, Burning Pact x1, Hemokinesis x1`
- `2` 回合：`0 / 42.8571 / 51.5873 / 3.6508 / 1.9048`
- `3` 回合：`0 / 0 / 78.7619 / 17.4222 / 3.8159`
- `4` 回合：`0 / 0 / 0 / 71.4286 / 28.5714`

对应机器底稿与鲁棒性：

- [difficulty2_burning_pact_drift_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_burning_pact_drift_machine_export.json>)
- [difficulty2_burning_pact_drift_robustness.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_burning_pact_drift_robustness.json>)

鲁棒性结果是：

- `valid_cases = 12`
- `invalid_cases = 28`

所以它不只是“漂了”，而且漂过去后的家族也很脆。

## 46. 这次的准确分类：`Burning Pact` 不是普通抽牌牌，也不是普通漂移牌，而是主动瘦身漂移牌

`Burning Pact` 和前面的几类不一样：

- `Battle Trance` / `Pommel Strike` 主要是抽牌密度方向
- `Headbutt` 是回顶导致的原型分叉
- `Thunderclap` 是更轻重叠件压缩中间支点
- `Burning Pact` 则是**通过主动 exhaust 掉当前资源，把原型瘦身成另一条更薄的家族**

所以这一步的更准确结论是：

**`Burning Pact` 是主动瘦身漂移牌。**

含义是：

1. 它的问题不只是“多抽了两张”
2. 而是“抽两张”的同时还定向删掉了一张当前资源
3. 这会让原型里原本承担张力的中间支点被直接剥掉
4. 最后活下来的往往不是原型扩展版，而是一条更薄、更偏单 payoff 的新家族

到这里，当前原型附近的分型又补了一层：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线
9. `Body Slam`：主线脱核牌
10. `Thunderclap`：核心机制压缩漂移牌
11. `Anger`：增殖压穿牌
12. `Iron Wave`：混合攻防稳定器
13. `Clothesline`：后续减伤稳定器
14. `Burning Pact`：主动瘦身漂移牌

这一步的价值在于：现在连“通过 exhaust 主动变薄”的牌也不再混在普通抽牌牌或普通漂移牌里。后面只要出现会在战斗中主动删资源、再换取命中率或节奏的牌，都应该优先按这条口径审，而不是先把它当成普通滤牌件。

## 47. 第十五张候选牌的验证：`Armaments` 不是健康扩展，而是把邻近家族推向“缺慢线 / 稳定线”两侧

这次我补测的是 `Armaments`：

- `1` 费
- `Gain 5 Block.`
- `Upgrade a card in your Hand.`

它和前面的牌都不太一样：

1. 它不加抽牌
2. 不改总牌量
3. 也不是简单给一回合爆发
4. 它真正新增的是：**战斗内永久改变单卡质量**

这轮我没有先做整族暴扫，而是先对最像前沿的 `4` 组代表池做了精确对比。原因很简单：升级态一旦接进求解器，状态数会明显放大，先看最相关前沿比盲扫更有效。

我测的 `4` 组代表池是：

1. `Strike x6, Defend x1, Bash x1, Armaments x1, Hemokinesis x1`
2. `Strike x5, Defend x2, Bash x1, Armaments x1, Hemokinesis x1`
3. `Strike x4, Defend x3, Bash x1, Armaments x1, Hemokinesis x1`
4. `Strike x5, Defend x1, Bash x1, Uppercut x1, Armaments x1, Hemokinesis x1`

结果是：

1. `Strike x6, Defend x1, Bash x1, Armaments x1, Hemokinesis x1`
   - `stable_count = 0`
   - `case_count = 31`
   - 但 **没有 `4` 回合线**
   - `2` 回合最优：`0 / 85.7143 / 13.8095 / 0 / 0.4762`
   - `3` 回合最优：`0 / 0 / 92.9524 / 2.2857 / 4.7619`
2. `Strike x5, Defend x2, Bash x1, Armaments x1, Hemokinesis x1`
   - `stable_count = 4`
   - `case_count = 36`
   - 有 `2/3/4` 梯度，但已经出现稳定线
   - `4` 回合最优：`0 / 0 / 0 / 71.4286 / 28.5714`
3. `Strike x4, Defend x3, Bash x1, Armaments x1, Hemokinesis x1`
   - `stable_count = 5`
   - `case_count = 37`
   - 比上一组更容易滑向稳定线
4. `Strike x5, Defend x1, Bash x1, Uppercut x1, Armaments x1, Hemokinesis x1`
   - `stable_count = 2`
   - `case_count = 48`
   - 仍然 **没有 `4` 回合线**

也就是说，`Armaments` 在当前原型附近没有给出像 `Shrug It Off` 那样的健康局部扩展。它的结构形状更像：

- 再薄一点：快线和主线被明显抬高，但慢线直接消失
- 再厚一点：慢线回来，但整池开始冒稳定线

我还补了两份代表性底稿：

边界缺梯度样例：
- [difficulty2_armaments_missing_gradient_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_armaments_missing_gradient_example.json>)
- 代表池：`Strike x6, Defend x1, Bash x1, Armaments x1, Hemokinesis x1`

稳定反例：
- [difficulty2_armaments_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_armaments_stable_example.json>)
- 代表池：`Strike x5, Defend x2, Bash x1, Armaments x1, Hemokinesis x1`
- 代表稳定线：`Armaments, Bash, Defend, Defend, Hemokinesis, Strike, Strike`
- 精确结果：`0 / 85.7143 / 14.0000 / 0.2857 / 0`

## 48. 这次的准确分类：`Armaments` 不是普通保命牌，而是战斗内永久强化阈值牌

`Armaments` 和前面的几类都不一样：

- 它不像 `Shrug It Off` 那样主要通过额外保命抬慢线
- 不像 `Burning Pact` 那样通过主动变薄改写结构
- 也不像 `Headbutt` 那样靠回顶制造分叉

它真正做的事是：

1. 在战斗中把某一张手牌永久升级
2. 这个升级会继续留在后续的 `Hand / Draw / Discard`
3. 因此它不是一回合 buff，而是**永久改写后续回合的牌质分布**

所以这一步的更准确结论是：

**`Armaments` 是战斗内永久强化阈值牌。**

含义是：

1. 它不会稳定地产生健康前沿
2. 它更像把邻近结构往某个阈值上推
3. 阈值左边的结构会变成“快线很强，但没有慢线”
4. 阈值右边的结构会直接滑向稳定线

到这里，当前原型附近的分型又补了一层：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线
9. `Body Slam`：主线脱核牌
10. `Thunderclap`：核心机制压缩漂移牌
11. `Anger`：增殖压穿牌
12. `Iron Wave`：混合攻防稳定器
13. `Clothesline`：后续减伤稳定器
14. `Burning Pact`：主动瘦身漂移牌
15. `Armaments`：战斗内永久强化阈值牌

这一步的价值在于：现在连“战斗内永久升级一张牌”的机制也有了独立分类。后面只要出现会在战斗中持续保留强化结果的牌，都不该先按普通数值补强去看，而要先检查它是不是在把邻近题面往阈值两侧推。

## 49. 第十六张候选牌的验证：`Grapple` 有前沿，但它保住的是快线，不是主线

这次我补测的是 `Grapple`：

- `1` 费
- `Deal 7 damage.`
- `Whenever you gain Block this turn, deal 5 damage to the enemy.`

它和前面的牌也不一样：

1. 它不加抽牌
2. 不改总牌量
3. 也不是永久升级
4. 它新增的是：**回合内延迟触发轴**

也就是打出之后，后续每一次获得格挡，都会再触发一次伤害。

我先做了最相关的窄邻域过滤：

- 固定 `Grapple = 1`
- `Strike 3-7`
- `Defend 1-5`
- `Bash = 0/1`
- `Uppercut = 0/1`
- `Hemokinesis = 0/1`
- 总池固定 `10`

结果是：

1. `pool_vectors = 34`
2. `stable_rejected = 22`
3. `missing_gradient = 10`
4. `accepted = 2`
5. `accepted_with_bash_hemo = 1`

这说明 `Grapple` 不是坏牌，也不是一接进来就整块压穿。它确实留下了一条保原型前沿。

唯一保住 `Bash + Hemokinesis` 的接受池是：

- 卡池：`Strike x5, Defend x2, Grapple x1, Bash x1, Hemokinesis x1`
- `case_count = 36`
- `stable_count = 0`

三条代表线是：

- `2` 回合：`0 / 85.7143 / 10.4762 / 0 / 3.8095`
- `3` 回合：`0 / 0 / 95.2381 / 0 / 4.7619`
- `4` 回合：`0 / 0 / 0 / 71.4286 / 28.5714`

对应机器底稿与鲁棒性：

- [difficulty2_grapple_hemo_bash_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_grapple_hemo_bash_machine_export.json>)
- [difficulty2_grapple_hemo_bash_robustness.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_grapple_hemo_bash_robustness.json>)

鲁棒性结果是：

- `valid_cases = 32`
- `invalid_cases = 8`

也就是说，它在参数窗上的稳定性不差。

但问题不在“成不成立”，而在**三条线是不是还围着同一个原型机制转**。

这组结构里：

1. `2` 回合快线会用 `Bash + Defend + Grapple + Hemokinesis`
2. `3` 回合主线已经变成 `Defend x2 + Grapple + Strike x4`
3. `4` 回合慢线又退回 `Defend x2 + Strike x5`

也就是说，`Grapple` 不是像 `Shrug It Off` 那样把原型整体抬稳，而是：

- 快线还保着原型
- 主线已经切到 `Grapple + Defend` 这个新轴
- 慢线再进一步回退成基础攻防线

## 50. 这次的准确分类：`Grapple` 不是健康扩展，而是中线换轴牌

`Grapple` 和前面的几类区别很清楚：

- 它不像 `Body Slam` 那样整体主线脱核
- 也不像 `Thunderclap` / `Burning Pact` 那样直接漂成另一家族
- 它保住了一条前沿
- 但保住的是快线，不是主线

所以这一步的更准确结论是：

**`Grapple` 是中线换轴牌。**

含义是：

1. 新牌本身不是坏牌
2. 它也不一定让整池失控
3. 但它会把原型的主线解法切换到另一条中间轴
4. 结果就是快线、主线、慢线三层依赖的机制不再一致

到这里，当前原型附近的分型又补了一层：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线
9. `Body Slam`：主线脱核牌
10. `Thunderclap`：核心机制压缩漂移牌
11. `Anger`：增殖压穿牌
12. `Iron Wave`：混合攻防稳定器
13. `Clothesline`：后续减伤稳定器
14. `Burning Pact`：主动瘦身漂移牌
15. `Armaments`：战斗内永久强化阈值牌
16. `Grapple`：中线换轴牌

这一步的价值在于：现在连“回合内延迟触发轴”也不是只看它能不能出一条接受池，而是会继续审它到底保住的是哪一层路线。后面只要出现类似“先下一个触发器，再靠后续资源触发追加伤害”的牌，都应该优先按这条口径审它，而不是只看整体胜率。

## 51. 第十七张候选牌的验证：`Second Wind` 能保快线，但主线和慢线都会把它丢掉

这次我补测的是 `Second Wind`：

- `1` 费
- `Exhaust all non-Attack cards in your Hand.`
- `Gain 5 Block for each card Exhausted.`

它和前面的 `Burning Pact`、`Grapple` 都不一样：

1. 它不是单点 exhaust
2. 不是抽牌换薄
3. 也不是回合内持续触发器
4. 它是**按标签批量清空手牌的一类牌**

也就是说，这次测试的是：
**如果新牌会把当前手里一整类牌批量清掉，并把这个批量清牌直接换成格挡，当前原型会不会出现新的“只保一层路线”的结构。**

我先做了最相关的窄邻域过滤：

- 固定 `Second Wind = 1`
- `Strike 3-7`
- `Defend 1-5`
- `Bash = 0/1`
- `Uppercut = 0/1`
- `Hemokinesis = 0/1`
- 总池固定 `10`

结果是：

1. `pool_vectors = 34`
2. `stable_rejected = 6`
3. `missing_gradient = 24`
4. `accepted = 4`
5. `accepted_with_bash_hemo = 2`

这说明 `Second Wind` 既不是坏牌，也不是一接进来就整块失控。它能留下保原型前沿，而且接受候选数比 `Grapple` 还多。

当前最强的保原型接受池是：

- 卡池：`Strike x5, Defend x2, Second Wind x1, Bash x1, Hemokinesis x1`
- `case_count = 36`
- `stable_count = 0`

三条代表线是：

- `2` 回合：`0 / 85.7143 / 10.4762 / 0 / 3.8095`
- `3` 回合：`0 / 0 / 89.5238 / 5.7143 / 4.7619`
- `4` 回合：`0 / 0 / 0 / 71.4286 / 28.5714`

对应机器底稿与鲁棒性：

- [difficulty2_second_wind_hemo_bash_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_second_wind_hemo_bash_machine_export.json>)
- [difficulty2_second_wind_hemo_bash_robustness.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_second_wind_hemo_bash_robustness.json>)

鲁棒性结果是：

- `valid_cases = 32`
- `invalid_cases = 8`

它的参数窗稳定性也不差。

但拆开三条线看，问题很清楚：

1. `2` 回合快线会用 `Bash + Defend + Hemokinesis + Second Wind`
2. `3` 回合主线已经变成 `Bash + Defend x2 + Strike x4`
3. `4` 回合慢线进一步退回 `Defend x2 + Strike x5`

也就是说，`Second Wind` 确实保住了一条原型快线，但它没有像 `Shrug It Off` 那样把整套原型一起带上来。它更像只在爆发窗口里有用，一到主线和慢线就被整套丢掉。

## 52. 这次的准确分类：`Second Wind` 不是健康扩展，而是快线专属扩展牌

`Second Wind` 和前面的几类要分开看：

- 它不像 `Burning Pact` 那样漂到更薄家族
- 不像 `Grapple` 那样把主线换轴
- 也不像 `Body Slam` 那样三层路线整体脱核

它更准确的形状是：

1. 快线还愿意带它
2. 主线已经把它丢掉
3. 慢线更不会再要它

所以这一步的更准确结论是：

**`Second Wind` 是快线专属扩展牌。**

含义是：

1. 它不是坏牌
2. 也不一定让整池失控
3. 但它只能稳定服务于最快那层路线
4. 对正式题来说，这类牌会让“推荐快解”更像一个单独的小题，而不是和主线、慢线组成统一结构

到这里，当前原型附近的分型又补了一层：

1. `Clash`：结构性坏牌
2. `Sword Boomerang`：家族漂移牌
3. `Headbutt`：原型分叉牌
4. `Shrug It Off`：局部扩展成功牌，但会快线压缩 / 窗口收缩
5. `Battle Trance`：稳定线生成牌
6. `Pommel Strike`：轻量攻击 + 抽牌密度方向风险过高
7. `Bloodletting`：高代价加速牌
8. `Rage`：攻击触发防御方向会生成稳定线或抹掉慢线
9. `Body Slam`：主线脱核牌
10. `Thunderclap`：核心机制压缩漂移牌
11. `Anger`：增殖压穿牌
12. `Iron Wave`：混合攻防稳定器
13. `Clothesline`：后续减伤稳定器
14. `Burning Pact`：主动瘦身漂移牌
15. `Armaments`：战斗内永久强化阈值牌
16. `Grapple`：中线换轴牌
17. `Second Wind`：快线专属扩展牌

这一步的价值在于：现在连“批量清标签牌再按数量换收益”的机制也不只是看整池能不能过筛，而是会继续审它服务的是哪一层路线。后面只要出现类似“按标签批量 exhaust 再换格挡/伤害”的牌，都应该优先按这条口径审，而不是把它和普通 exhaust 件混在一起。

## 53. 精度修正：回合内随机抽牌分支的 `exact_result` 已改成“随机按概率聚合，玩家再继续最优”

这一轮我没有只加新牌，还修了一个更底层的问题：

- 之前求解器在 `exact_multiturn_result` 里，会把“回合内随机抽到什么”这类结果展开成多个后继状态
- 但这些后继状态曾被放进和玩家主动选择同一层的“取最优”里
- 这会把一部分随机结果错误地当成“玩家可选”

现在这一层已经改成：

1. 玩家先选择动作
2. 动作产生的随机结果按概率聚合
3. 玩家在观测到结果后，再从对应新状态继续最优

也就是说，`exact_result` 现在才真正符合“随机结果不是玩家可选分支”的口径。

我做了两组回归抽查：

1. `Burning Pact` 代表导出重新核过，结果与之前记录一致，说明这类“定向 exhaust + 抽牌”结构此前没有明显漂移。
2. `Shrug It Off` 的代表导出已重导出，快线与主线数值都出现了轻微漂移，说明**旧求解器对部分回合内随机抽牌结构确实存在精度风险**，只是影响大小会因牌而异。

所以从这一节开始，规则要再收紧一层：

- 当前求解器导出的 `exact_result` 与全池审计结果，可作为权威值继续使用
- 旧时间点导出的、涉及回合内随机抽牌/随机 exhaust 的机器底稿，原则上都应重导出
- 现在正式导出链路已经切到精确 `turn_rows`；对当前已支持的随机分支牌型，逐回合表与 `exact_result` 已按同一口径对齐，可以一起作为正式题稿的权威底稿

本轮已经先刷新了两份最直接受影响的代表底稿：

- [difficulty2_shrug_hemo_bash_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_shrug_hemo_bash_machine_export.json>)
- [difficulty2_burning_pact_drift_machine_export.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_burning_pact_drift_machine_export.json>)

另外，两份单例稳定样例也已经按同一口径重导出，并补上了精度元数据：

- [difficulty2_battle_trance_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_battle_trance_stable_example.json>)
- [difficulty2_pommel_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_pommel_stable_example.json>)
- [difficulty2_burning_pact_hemo_stable_example.json](</C:/Users/Administrator/Documents/New project 2/difficulty2_burning_pact_hemo_stable_example.json>)

这些文件的 `export_meta.turn_rows_precision` 现在都已标成：
- `authoritative`

为了避免后续再手工漏刷，我还补了一个刷新脚本：

- [refresh_machine_export.py](</C:/Users/Administrator/Documents/New project 2/refresh_machine_export.py>)

它现在已经能同时处理两种旧底稿形态：

1. 标准 `machine_export` 结构
2. 单例 `stable_example` 结构

而且现在已经支持：

- `python .\\refresh_machine_export.py --scan-cwd-random`

也就是直接扫描当前目录下所有带随机分支牌型的机器底稿，并按当前求解器口径原地刷新。

这一步的价值不在于多了一条新分类，而在于：  
**现在难度 2 原型附近，随机分支牌型不再只是“总结果能信”，而是已经具备可直接服务正式成题校对的权威逐回合底稿。**

按“最终能不能正式出题”这条口径回看，当前阻塞点也更清楚了：

1. **阻塞点已经不再是随机分支精度。**
2. 当前难度 2 仍不能定稿，主因是它还停留在缩尺原型：卡池规模、干扰层数量、全池审计范围都还没扩到正式难度 2 口径。
3. 也就是说，后续如果继续校对，应优先盯“是否已经形成正式题稿所需的资源规模和三解结构”，而不是继续怀疑这些随机分支牌型的概率口径本身。
