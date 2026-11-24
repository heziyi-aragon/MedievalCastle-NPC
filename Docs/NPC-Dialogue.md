# 城堡 NPC 对话与决策模块

使用 Unity 2022.3.62f3c1 打开 `Assets/Scenes/Castle_Exterior.unity`。Hierarchy 中的 **Castle NPCs** 组合包含商人、守卫、骑士、对话服务、联调面板和观察位置。三个角色各有独立 GLB、Prefab、碰撞体、待机呼吸和说话动作。可以单独移动或替换任一角色。

![城门前的商人、守卫与骑士](NPC-Cast-Courtyard.png)

### 城门角色

角色图从左到右依次是正面、侧面和一米对话距离的近景。正侧面隐藏周围建筑以便看清轮廓，合照和近景保留城门环境与场景光照。

- **商人**穿蓝金长袍，手指扣住账本。选中时前倾摊掌，报价时抬起账本，收到购买提案时再点头确认。[商人三视图](NPC-Merchant-Views.png)
- **守卫**戴羽冠黑盔，一手扶盾。选中时转头颔首，验通行证时伸出空手，视线向玩家手边移动。[守卫三视图](NPC-Guard-Views.png)
- **骑士**与披甲战马共用一个 Prefab。选中时骑手颔首、战马动耳。发布训练时她望向场中的训练假人，评价进度时朝玩家点头，双手随骑姿握缰。[骑士与战马三视图](NPC-Knight-Views.png)

## 在城门前试一轮

1. 在电脑上点 **Play**。直接点击城门前的角色或右侧角色标签即可看到约一秒的招呼。首次点守卫也会触发。再点预设问句或输入消息。等待回复时角色注视玩家，收到有效台词后才开口。只想看动作可选 **离线演示**。面板支持 English / 中文，**F2** 可收起。头显演示时，先从 Quest 3 进入电脑的 Meta Horizon Link 会话，再在 Unity 点 Play。
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
- `Assets/CastleNPC/Characters/` 保存原创模型和 Prefab，[建模源文件](../Tools/NpcModels)按角色存放。玩法层可调用 `Greet(viewer)` 打招呼、`Listen(viewer)` 等待回复、`Speak(action, viewer, trainingTarget)` 播放决策手势、`EndConversation()` 收回动作。原有 `SetSpeaking(bool)` 可继续控制普通说话动作。
- 三个 GLB 各自内嵌一个 PBR 材质、颜色图和金属度／粗糙度图，守卫另有切线空间法线图。在 Project 里展开 GLB 即可查看子资源。移动或旋转骑士时选择最外层的 **Knight**，马身与骑手会一起移动。重新导入带原始命名节点的模型后，可用 **Tools → Castle NPC → Build Character Prefabs** 更新动画和碰撞体引用。
- `Assets/CastleNPC/Demo/` 保存桌面与头显的演示入口。正式游戏可停用 `NpcDemoPanel`，继续复用同一对象上的 `NpcDialogueService`。
- Unity **Window → General → Test Runner** 可运行 EditMode 和 PlayMode 测试。普通离线测试不请求 API。手动设置 `CASTLE_NPC_LIVE_TESTS=1` 和 `DEEPSEEK_API_KEY` 后，按 `NpcLive` 分类运行三个真实在线用例。API 服务可能产生调用费用。

### 城门角色精修验收 · 2026-11-24

- **外观。** 用 Unity 2022.3.62f3c1 在同一场景、灯光与机位各拍摄十张改前和改后照片。近景相机距脸部取景中心一米。侧面核对了守卫的持盾手、商人的账本与袖口、骑手的握缰姿态及裙甲与马鞍的衔接。[全套原图](NPC-Polish) · [守卫对照](NPC-Polish/Guard-Close-Comparison.png) · [商人对照](NPC-Polish/Merchant-Close-Comparison.png) · [骑士对照](NPC-Polish/Knight-Close-Comparison.png)
- **网格。** 守卫 78,786 个三角形，商人 95,082 个，骑士与坐骑合计 98,276 个。每个 GLB 仍使用一个嵌入式 PBR 材质。源文件可重建三个 GLB，原有模型与 Prefab 的 GUID、角色 ID 和场景站位均保留。
- **Play 模式实录。** 14.58 秒的短片依次展示三人被选中、执行对话手势与回到待机。录制使用真实面板调用和离线决策，逐段检查了颌骨开合与回位。[观看动作短片](NPC-Polish/NPC-Conversation.mp4)
- **桌面实测。** 在 Game 视图直接点选守卫、商人和马身。守卫要求出示通行证（`check_pass`）。商人报出提灯 5 枚铜币（`none`），购买一个面包时提示确认 2 枚铜币（`request_purchase`）。骑士让玩家检查三处训练标记（`offer_training`）。
- **自动回归。** EditMode 通过 64 项，PlayMode 通过全部 6 项。覆盖首次选择默认守卫、快速切换、重复点选、无效或迟到回复、询价和购买手势差异、持盾与握缰稳定，以及骑士发布和评价训练时的朝向。模拟 XR 验证长按不会重复发送、朝空处首次确认不会发问，以及马身和骑手共用选择。
- **验证范围。** 本次使用离线演示验收动作。三项 DeepSeek 在线用例跳过，Quest 3 真机双眼画面与实体手柄尚未实测。
