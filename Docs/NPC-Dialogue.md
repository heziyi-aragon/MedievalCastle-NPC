# 城堡 NPC 对话与决策模块

使用 Unity 2022.3.62f3c1 打开 `Assets/Scenes/Castle_Exterior.unity`。Hierarchy 中的 **Castle NPCs** 组合包含商人、守卫、骑士、对话服务、联调面板和观察位置。三个角色各有独立 GLB、Prefab、碰撞体、待机呼吸和说话动作。可以单独移动或替换任一角色。

![城门前的商人、守卫与骑士](NPC-Cast-Courtyard.png)

### 城门角色

角色图从左到右依次是正面、侧面和约一米距离的脸部近景。城门合照使用场景里的灯光。单人图保留同一组灯光，隐藏周围建筑以便看清轮廓。

- **商人**身穿象牙白内袍和蓝金刺绣外衣。讲话时抬起摊开的手，另一只手托住账册。[商人三视图](NPC-Merchant-Views.png)
- **守卫**站在闸门前，盾牌、盔冠和两枚披风扣从正面可见。他在对话中轻轻点头。[守卫三视图](NPC-Guard-Views.png)
- **骑士**与披甲战马共用一个 Prefab。骑手转头回应玩家，两手握缰。马身呼吸和左右耳的小动作在待机时也会播放。[骑士与战马三视图](NPC-Knight-Views.png)

## 在城门前试一轮

1. 在电脑上点 **Play**。选择一个角色，点预设问句，或在输入框里自由输入。面板支持 English / 中文，**F2** 可收起。头显演示时，先从 Quest 3 进入电脑的 Meta Horizon Link 会话，再在 Unity 点 Play。
2. 在头显中注视一个角色，按右手 **扳机或 A** 选中。瞄准骑手、马身、马颈或马头都会选中同一个骑士。松开扳机，上下拨右摇杆选择问句，再按扳机或 A 发送。**B** 收起或展开面板。角色位于城门外的同一片空地，站在出生点即可与三人交谈。
3. 选择 **Merchant**，先询问面包价格，再说 `I would like to buy two loaves of bread.`。展开桌面调试区，对照输入时的状态、原始 JSON 和解析结果。默认面包每个 2 金币，提灯每个 5 金币。一次明确的购买请求会给玩法层一个购买提议。
4. 在 **Edit demo player state** 把金币改成 3，重复购买两份面包。在面板回传 **Rejected**，或由正式交易系统回传具体失败原因。守卫可用通行证开关检验；骑士可通过切换训练状态检验。面板上的 Accepted / Rejected 只模拟玩法层回执。

## 在线与离线

Windows 用户环境变量 `DEEPSEEK_API_KEY` 保存这台演示电脑自己的密钥。在 **系统属性 → 环境变量 → 用户变量** 中添加后重新进入 Play。可用 `DEEPSEEK_MODEL` 覆盖默认模型 `deepseek-flash`。Unity 在主线程异步请求 DeepSeek 的 HTTPS Chat Completions 接口，使用 JSON 输出，单轮超时 30 秒。密钥不需要填入场景、Prefab 或交接文件。

首次启动会显示当前提供方。有密钥时选用 **Live · DeepSeek**，缺少密钥时选用 **Offline demo**。可以手动切换。离线预设覆盖双语演示路径，自由上下文生成由在线模型提供。在线请求失败时，界面显示失败和重试入口。供多人共享服务端密钥的正式发行版应由服务端持有共享密钥。 [DeepSeek API 文档](https://api-docs.deepseek.com/api/create-chat-completion/)

## 成员 3 如何接入

代码使用程序集 `CastleNPC.Dialogue`、命名空间 `MedievalCastle.Npc`。玩法层提供最新的玩家状态，并在 Unity 主线程调用场景上的 `NpcDialogueService`。示例展示“送出对话 → 获得提议 → 回报执行结果”。

```csharp
var turn = await dialogue.SendTurnAsync("merchant", playerText, snapshot, "en", cancellationToken);
if (!turn.Success)
{
    ShowRetry(turn.Error);
    return;
}

ShowSubtitle(turn.Reply);
if (turn.Decision.Action == NpcAction.RequestPurchase)
{
    // 这是玩法组提供的接口。它按 turnId 去重，并使用实时余额和商品表完成交易。
    string outcome = shop.TryPurchaseOnce(turn.TurnId, snapshot.playerId,
        turn.Decision.itemId, turn.Decision.quantity);
    dialogue.ReportDecisionResult(turn.TurnId, outcome);
}
```

- **输入。** 角色 ID 为 `guard`、`merchant`、`knight`；语言为 `en` 或 `zh`。每个玩家使用稳定的 `playerId`。状态包括 `hasPass`、`coins`、`inventory`（商品 ID 和数量）、`catalog`（商品 ID 和单价）、`questId`、`trainingStatus`、`trainingProgress`、`trainingTarget`。首次求助、状态变化和切换 NPC 都可以沿用同一服务实例。
- **输出。** 成功结果包含 `TurnId`、`Reply`、`Decision`、`RawJson`、`IsOffline`。协议固定为 `reply`、`action`、`itemId`、`quantity`、`questId` 五个字段。无关 ID 填空字符串，无关数量填 0。玩法代码用枚举 `Decision.Action` 分发。 [六个可复制的 JSON 示例](NPC-Decision-Examples.json)
- **守卫。** 问路和问规则返回 `None`；请求入城提出 `CheckPass`。玩法层读取实际通行证与城门权限。
- **商人。** 询价和浏览商品返回 `None`；明确购买提出 `RequestPurchase`。商品 ID 必须位于当前目录，数量必须是正整数。玩法层以当前价格、数量和余额验证并执行交易。
- **骑士。** 固定任务 ID 为 `courtyard_drill`，演示目标是检查三处操练标记。`available` 可提出 `OfferTraining`，`active` 和 `ready` 可提出 `ReviewTraining`，`rewarded` 只对话。玩法层维护标记进度并决定奖励。
- **回执与记忆。** 按玩家和角色分别保留最近六个有效对话回合。回执只归属于对应的 `turnId`，首次回执进入后续上下文。聊天记录在本次服务实例内有效。`Cancel()` 使进行中的结果失效，`ResetSession()` 同时清除记忆。玩家退出或换档时由调用方选择重置。
- **重试与并发。** 同一玩家和角色的一轮进行中时重复提交会收到失败结果。空回复、错误 JSON、角色越权、取消和过期状态均返回无可执行决策的结果。若玩法层整体替换了状态对象，应取消旧请求或按自己的状态版本丢弃旧回合，并在执行时再次检查实时状态。

## 维护与验证

- `Assets/CastleNPC/Dialogue/Resources/CastleNPC/` 存放公共输出约定及三个角色的独立 Prompt。Prompt 接收游戏提供的状态快照、该角色的对话历史、执行回执及本轮玩家输入。
- `Assets/CastleNPC/Characters/` 保存原创模型和 Prefab。预制体的稳定角色 ID 供射线检测使用；角色组件 `CastleNpcCharacter.SetSpeaking(bool)` 控制动作。运行时不依赖外部建模软件。
- 三个 GLB 各自内嵌一个 PBR 材质、颜色图和金属度／粗糙度图，守卫另有切线空间法线图。在 Project 里展开 GLB 即可查看子资源。移动或旋转骑士时选择最外层的 **Knight**，马身与骑手会一起移动。重新导入带原始命名节点的模型后，可用 **Tools → Castle NPC → Build Character Prefabs** 更新动画和碰撞体引用。
- `Assets/CastleNPC/Demo/` 保存桌面与头显的演示入口。正式游戏可停用 `NpcDemoPanel`，继续复用同一对象上的 `NpcDialogueService`。
- Unity **Window → General → Test Runner** 可运行 EditMode 和 PlayMode 测试。普通离线测试不请求 API。手动设置 `CASTLE_NPC_LIVE_TESTS=1` 和 `DEEPSEEK_API_KEY` 后，按 `NpcLive` 分类运行三个真实在线用例。API 服务可能产生调用费用。

### 本机验收记录 · 2026-11-24

- Unity 2022.3.62f3c1 的 EditMode 测试通过 59 项，其中 53 项覆盖 NPC 模块，6 项为已有地形回归。验收包含 GLB 导入后的 UV、颜色图与表面贴图、可用着色器、Prefab 引用，以及原有对话、记忆与决策解析。
- PlayMode 测试通过 2 项。在 `Castle_Exterior` 中，桌面预设依次触发守卫的 `check_pass`、商人的 `request_purchase`、骑士的 `offer_training`。测试逐人采样开口和回到待机的位置，也检查三人完整轮廓位于面板旁的视口内，以及马匹的呼吸、动耳动作。
- 模拟头显在场景出生点 1.65 米眼高分别命中骑手和马身，两者解析到同一个 `knight`。按住选人扳机不会误发问句，松开后发送中文训练进度问题，持续按住仍只产生一个 `turnId`，回复为 0/3。
- Unity 在同一城门场景输出合照和九个单人机位。场景日光方向、环境光和城门补光已经写入场景，游戏中也可见这些变化。近景检查包含商人的袍领与灰须、守卫胸甲高光与头盔接缝、骑士双手与缰绳，以及侧面裙甲与鞍具的衔接。
- 模型按刚性动作节点合并子网格。守卫为 78,468 个三角形、6 个图元，商人为 87,348 个三角形、6 个图元，骑手与坐骑合计 79,356 个三角形、16 个图元。三人各用一个材质。这里统计的是模型数据，实际每帧绘制次数与帧率取决于双眼渲染、阴影及场景设置。
- 重开项目后已在桌面 Game 视图手动点选三个角色并发送中文预设。守卫要求出示通行证，商人报一份面包 2 枚铜币，骑士提出检查庭院的三处操练标记。面板分别显示 `check_pass`、`request_purchase`、`offer_training` 和玩法回执按钮。
- 三个 DeepSeek 真实在线用例已就绪。本次机器缺少密钥，三个用例均跳过。验收时 Quest Link 会话未连接，新 NPC 面板的双眼观感、物理手柄输入和头显内字号留待实机确认。
