# 城堡 NPC 对话与决策模块

使用 Unity 2022.3.62f3c1 打开 `Assets/Scenes/Castle_Exterior.unity`。Hierarchy 中的 **Castle NPCs** 组合包含商人、守卫、骑士、对话服务、玩家对话界面和出生位置。三个角色各有独立 GLB、Prefab、碰撞体、待机呼吸和说话动作。可以单独移动或替换任一角色。

![城门前的商人、守卫与骑士](NPC-Cast-Courtyard.png)

### 城门角色

角色图从左到右依次是正面、侧面和一米对话距离的近景。正侧面隐藏周围建筑以便看清轮廓，合照和近景保留城门环境与场景光照。

- **商人**穿蓝金长袍，手指扣住账本。选中时前倾摊掌，报价时抬起账本，收到购买提案时再点头确认。[商人三视图](NPC-Merchant-Views.png)
- **守卫**戴羽冠黑盔，一手扶盾。选中时转头颔首，验通行证时伸出空手，视线向玩家手边移动。[守卫三视图](NPC-Guard-Views.png)
- **骑士**与披甲战马共用一个 Prefab。选中时骑手颔首、战马动耳。发布训练时她望向场中的训练假人，评价进度时朝玩家点头，双手随骑姿握缰。[骑士与战马三视图](NPC-Knight-Views.png)

## 从城门开始

玩家出生在城门外，首先看到 **Walk up to the guard**。走近黑甲守卫，看到角色名与交谈键时按下即可打招呼。收到守卫第一条有效回复后，结束谈话会看到拜访商人和训练区骑士的提示。三个角色都可以自由拜访。

- **Quest Link** 使用左摇杆行走，速度随推杆幅度变化，最大 1.6 米/秒。向右或向左拨右摇杆转向 30°。走到角色或马身约 1.6 米以内，右手按 **A** 打招呼，松开后可用右摇杆上下选择问句、按 **A** 发送。选择 **End conversation** 恢复行走。
- **电脑** 点击 Game 视图，用 **WASD** 行走、鼠标环顾。走近后按 **E**，可点预设问句或输入自己的问题。打开对话时鼠标会释放，输入文字不会移动角色。
- **设置** 用右手 **B** 或电脑 **Esc** 打开。首次启动使用英语，选择中文或 English 后会记住偏好。在交谈中打开设置，选 **Resume** 或再次按菜单键可以返回谈话。

## DeepSeek 与开发测试

玩家对话使用这台电脑的个人 DeepSeek。设置 Windows 用户环境变量 `DEEPSEEK_API_KEY` 后重新进入 Play。可用 `DEEPSEEK_MODEL` 覆盖默认的 `deepseek-flash`。每次发送会结合角色、玩家状态与历史请求一条回复，超时为 30 秒。缺少密钥、余额不足及网络失败时，玩家可在对话中选择 **Retry** 或 **End conversation**。API 密钥只保存在本机。[DeepSeek API 文档](https://api-docs.deepseek.com/api/create-chat-completion/)

编辑器自动测试可为 `NpcDialogueService` 指定离线提供方或注入受控响应，复现交易、训练、失败与迟到回包。正式游戏由玩法层执行并回报有效提议，NPC 面板只负责对话和手势。

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

### 步行与近距离交谈验收 · 2026-11-24

- 桌面 Game 视图实测显示英语出生引导，Esc 打开独立语言菜单。PlayMode 从实际出生点使用玩家控制器走到三个角色身边并返回，验证守卫查证、商人询价与下单、骑士训练等代表性回复和手势。键盘输入回归覆盖 E 打招呼、Esc 打开设置和返回原谈话。
- 模拟 Quest 输入覆盖侧身靠近、马身交谈、长按或重连不重复发送、B 菜单切换语言。实体碰撞验证了墙体隔断、门塔阻挡、抬起的吊闸下方通行以及碰撞体随闸移动。
- Unity 2022.3.62f3c1 的 74 项 EditMode 和 12 项 PlayMode 测试通过。回复、重试和晚到的响应通过离线及受控提供方验证。三项真实 API 测试跳过。OpenXR 报告未检测到可用头显，Quest Link 的双眼画面与实体手柄尚待真机确认。

### 历史记录 · 城门角色精修验收 · 2026-11-24

- **外观。** 用 Unity 2022.3.62f3c1 在同一场景、灯光与机位各拍摄十张改前和改后照片。近景相机距脸部取景中心一米。侧面核对了守卫的持盾手、商人的账本与袖口、骑手的握缰姿态及裙甲与马鞍的衔接。[全套原图](NPC-Polish) · [守卫对照](NPC-Polish/Guard-Close-Comparison.png) · [商人对照](NPC-Polish/Merchant-Close-Comparison.png) · [骑士对照](NPC-Polish/Knight-Close-Comparison.png)
- **网格。** 守卫 78,786 个三角形，商人 95,082 个，骑士与坐骑合计 98,276 个。每个 GLB 仍使用一个嵌入式 PBR 材质。源文件可重建三个 GLB，原有模型与 Prefab 的 GUID、角色 ID 和场景站位均保留。
- **Play 模式实录。** 14.58 秒的短片依次展示三人被选中、执行对话手势与回到待机。录制使用真实面板调用和离线决策，逐段检查了颌骨开合与回位。[观看动作短片](NPC-Polish/NPC-Conversation.mp4)
- **桌面实测。** 在 Game 视图直接点选守卫、商人和马身。守卫要求出示通行证（`check_pass`）。商人报出提灯 5 枚铜币（`none`），购买一个面包时提示确认 2 枚铜币（`request_purchase`）。骑士让玩家检查三处训练标记（`offer_training`）。
- **自动回归。** EditMode 通过 64 项，PlayMode 通过全部 6 项。覆盖首次选择默认守卫、快速切换、重复点选、无效或迟到回复、询价和购买手势差异、持盾与握缰稳定，以及骑士发布和评价训练时的朝向。模拟 XR 验证长按不会重复发送、朝空处首次确认不会发问，以及马身和骑手共用选择。
- **验证范围。** 本次使用离线演示验收动作。三项 DeepSeek 在线用例跳过，Quest 3 真机双眼画面与实体手柄尚未实测。
