# 城堡 NPC 对话与决策模块

使用 Unity 2022.3.62f3c1 打开 `Assets/Scenes/Castle_Exterior.unity`。Hierarchy 中的 **Castle NPCs** 组合包含商人、守卫、骑士、对话服务、玩家对话界面和出生位置。三个角色各有独立 GLB、Prefab、碰撞体、待机呼吸和说话动作。可以单独移动或替换任一角色。

![从出生点看向城门，左边市集有商人，门口有守卫，骑士在右边城墙与训练场围栏之间](Gate-Dialogue/NPC-Location-Map.png)

### 城门角色

角色图从左到右依次是正面、侧面和一米对话距离的近景。正侧面隐藏周围建筑以便看清轮廓，合照和近景保留城门环境与场景光照。[早期角色合照](NPC-Cast-Courtyard.png)

- **商人**穿蓝金开襟长袍，腰包垂在身侧。他会托起账册低头查阅，有人搭话时放低账册、前倾摊掌，报价时再抬本。[商人三视图](NPC-Merchant-Views.png)
- **守卫**披黑甲和重披风。他左右巡视城门，肩膀稍晚于头部转动，偶尔收盾。有人搭话时从当前姿势转向玩家，验通行证时伸出空手。[守卫三视图](NPC-Guard-Views.png)
- **骑士**骑在披甲战马上，裙甲顺着马鞍两侧展开。她会环顾训练区，坐骑缓缓呼吸，分别转动双耳、偶尔甩尾。发布训练时她望向训练假人，评价时回望玩家。[骑士与战马三视图](NPC-Knight-Views.png)

## 从城门开始

玩家出生在城门外，画面显示 **Talk to the guard／跟守卫交流**。向前走，守卫站在闸门外侧的门柱旁。面向城门向左走，商人在市集柜台靠路的一端。沿城墙向右走，骑士和战马驻在墙与训练场围栏之间，走到马身旁即可搭话。玩家从角色身旁的面板选问句，在角色头顶读台词。招呼期间即可选问句。与守卫成功交谈后，探索提示会指明两人的方向。

- **Quest Link** 使用左摇杆行走，速度随推杆幅度变化，最大 1.6 米/秒。右摇杆左右连续转向，轻推可微调，满推约 0.12 秒达到 50°/秒，松手后约 0.08 秒停稳。走到角色或马身约 1.6 米以内，右手按 **A** 打招呼，松开后用右摇杆上下选择问句，再按 **A** 发送。选 **End conversation** 后让右摇杆回中，即可继续转向。
- **电脑** 点击 Game 视图，用 **WASD** 行走、鼠标环顾。走近后按 **E**，可点预设问句或输入自己的问题。打开对话时鼠标会释放，输入文字不会移动角色。
- **眼高** 电脑视角高 1.75 米。Quest Link 建立连接后，请用平常游玩的姿势看向前方片刻。系统以首次稳定的坐姿或站姿校准到 1.75 米，随后俯身和站起会自然改变视线高度。
- **设置** 用右手 **B** 或电脑 **Esc** 打开羊皮纸手札。墨色边框指示当前焦点，勾记标出已用语言。用摇杆选择并按 **A** 确认，电脑可直接点选。再按 **B／Esc** 返回原来的谈话或探索。首次启动使用英语，之后会记住选定的语言。

## DeepSeek 与开发测试

玩家对话使用这台电脑的个人 DeepSeek。设置 Windows 用户环境变量 `DEEPSEEK_API_KEY` 后重新进入 Play。可用 `DEEPSEEK_MODEL` 覆盖默认的 `deepseek-flash`。每次发送会结合角色、玩家状态与历史请求一条回复，超时为 30 秒。缺少密钥、余额不足及网络失败时，玩家可在对话中选择 **Retry** 或 **End conversation**。API 密钥只保存在本机。[DeepSeek API 文档](https://api-docs.deepseek.com/api/create-chat-completion/)

编辑器自动测试可为 `NpcDialogueService` 指定离线提供方或注入受控响应，复现交易、训练、失败与迟到回包。正式游戏由玩法层执行并回报有效提议，NPC 面板只负责对话和手势。

### 角色语音

角色使用同一个 `DEEPSEEK_API_KEY` 将有效回复中的 `reply` 送到 DeepSeek 语音合成接口。守卫和商人使用 `tide`，女骑士使用 `stella`。头顶先显示文字，音频就绪后由角色嘴边的 3D 声源播放，口型随实际声音变化。语音服务按字符独立计费。[DeepSeek 语音合成文档](https://api-docs.deepseek.com/zh-cn/guides/tts/)

中文守卫、英文商人和英文骑士的招呼已预置 DeepSeek 合成录音。其余角色与语言组合首次使用时合成，随后在当前场景复用。商人的语速较慢、音调较低，高频稍柔和。女骑士的语速略快，以清晰的短句发出训练指令。问候、回答和告别都使用各自的角色调音。语音请求失败时玩家仍可阅读文字、继续提问，玩法层仍能取得本轮有效决策。

主动选择「结束交谈」后即可走动。角色在原地说完告别，头顶字幕和口型随录音结束。六句中英文告别随项目提供，在断网时也可立即播放。若告别途中开始另一段谈话，上一位角色会停下。直接走出交谈距离时，角色会收回动作。[六句 DeepSeek 告别原音](../Assets/CastleNPC/Demo/Resources/CastleNpcFarewells)

声线对比采用同一句英文问候，每段先播放旧声，停顿 0.85 秒再播放新声。两段按听感响度对齐，适合用耳机比较语速和音色。文件采用游戏内相同的音调及滤波参数单独渲染。[商人试听](Gate-Dialogue/Voice-Comparisons/merchant-before-after.wav) · [女骑士试听](Gate-Dialogue/Voice-Comparisons/knight-before-after.wav)

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

### 训练场外围栏与右摇杆转向验收 · 2026-11-25

骑士位于 `(X=-11.8, Z=25.6)`，从城门沿右侧城墙走到马身旁即可交谈。整匹马距墙侧那排围栏至少 1.97 米。用 Unity 地形碰撞体核对了四蹄，接地误差均小于 1 毫米。从这条来路交谈时，骑士面向玩家的水平偏差约 6°，发布训练时面向假人 B 的偏差约 3°。[Play 模式位置图](Gate-Dialogue/NPC-Location-Map.png)

30、72、90、120 FPS 模拟测试均验证了右摇杆轻推、满推、反向和松手。满推达到稳定转速后，每秒转过 50°；此时松手，视角再转约 2° 即停。桌面 E 和模拟手柄 A 均已验证场外问答与告别。76 项常驻 EditMode、31 项常驻 PlayMode 及 3 项取景和接地检查通过，3 项可选在线聊天测试跳过。实际佩戴 Quest 的舒适度仍待真机体验确认。

### 历史记录 · 城门、市集和训练场入口站位验收 · 2026-11-25

实际站位的世界坐标 `(X, Z)` 为守卫 `(1.30, 24.85)`、商人 `(12.30, 33.50)`、骑士 `(-14.85, 34.10)`。骑士朝向 60°，玩家从马侧搭话时她能转脸看人，发布训练时转向入口旁的假人 B。Unity 依据鞋底和马蹄网格向地形碰撞体测量，八个接地点的高度差均在 1 毫米以内。

桌面和模拟 Quest 均从出生点走到三个站位、与角色交谈并告别。玩家控制器可以沿门洞中线经过守卫、穿过升起的闸门。已核对实际玩家视角下的问句面板和字幕。常驻回归通过 76 项 EditMode 和 31 项 PlayMode 测试，另通过 3 项场景取景与接地检查。3 项可选在线对话测试跳过。Quest 的 A 键使用模拟输入验证。[城门位置图](Gate-Dialogue/NPC-Location-Map.png)

### 商人、骑士声线与告别验收 · 2026-11-25

六句 DeepSeek 告别录音均为 24 kHz 单声道，原音长 2.20–3.20 秒。Unity 在实际播放时应用各角色的调音，口型读同一段录音。两组试听的前后响度差均小于 0.04 LUFS。

在工程副本的 `Castle_Exterior` 中，桌面按钮和模拟 Quest 右手 A 都验证了三人各两种语言的告别。菜单往返后仍可确认，连续按键只播放一次。玩家点结束后能边听边走；发起新谈话会打断旧声，迟到的回复也不会盖过告别。Unity 2022.3.62f3c1 通过 76 项 EditMode 和全部 30 项 PlayMode 回归。常规回归跳过 3 项可选在线聊天测试。OpenXR 未检测到实体头显，耳机中的空间声仅在编辑器中验证了声源位置与设置。

另外通过游戏的 DeepSeek 对话接口实测了两种角色。中文商人报出一盏提灯五枚铜币，动作是 `none`。英文骑士回应训练邀请时介绍了三个训练标记，并给出 `offer_training` 和任务 ID `courtyard_drill`。首次骑士回答还提到了金币和通行证，收紧训练提示词后已重新在线验证。

### 城门交谈视听验收 · 2026-11-25

从玩家实际视角录制的 15 秒短片依次展示三名角色的招呼与口型，并展示守卫查验通行证的动作。中文守卫和两句英文招呼均为本次真实 DeepSeek 合成，查证片段使用离线回复字幕。音轨来自 Unity 的立体声混音。[带声音的交谈实录](Gate-Dialogue/Castle-Conversation.mp4)

三名角色的问句板和头顶台词都在原始玩家机位内完整入镜。验证了靠近马身开始交谈、问句板一侧靠墙时重新选边、用鼠标点选和摇杆确认；设置中切换语言会保留当前台词页。[守卫招呼](Gate-Dialogue/guard-game.png) · [守卫查证](Gate-Dialogue/guard-pass.png) · [商人](Gate-Dialogue/merchant-game.png) · [骑士](Gate-Dialogue/knight-game.png)

Unity 2022.3.62f3c1 回归通过 76 项 EditMode 和 27 项 PlayMode 测试。模拟 Quest 从守卫正面、两侧、背面靠近，等到显示守卫名字再按 A，并检查守卫是否看向玩家。另测坐姿与站姿的初始眼高、俯身与重连、招呼缓存、打断和迟到音频。三项在线聊天用例跳过。三句语音合成已实测，守卫首个请求未收到响应，重试后成功，客户端无法确定首次请求的计费状态。本机 OpenXR 报告未检测到可用头显，Quest 真机眼高、双眼可读性及耳机播放仍待确认。

### 历史记录 · 城门轮廓、待机与转向

- 同机位、同光照的城门合照与三个全身侧面可逐项比较。守卫胸甲从约 47 厘米加宽到 51 厘米，商人腰线收紧、袍摆外展，骑士的分片裙甲顺着马侧垂下，后腿落点区分承重与休息姿态。[城门合照对比](Courtyard-Polish/Courtyard-Comparison.jpg) · [守卫侧面](Courtyard-Polish/Guard-Side.jpg) · [商人侧面](Courtyard-Polish/Merchant-Side.jpg) · [骑士侧面](Courtyard-Polish/Knight-Side.jpg)
- 三套 GLB 分别有 85,606、96,906 和 99,968 个三角形，每套使用一个材质。守卫的柄与指、商人的账册与指跟随同一只手臂。骑士与战马的握缰接点、裙甲与鞍座已做网格检查，马尾由独立的 `HorseTail` 节点驱动。
- 在 Unity Play 模式从同一出生点走向三人，在待机动作中途打招呼，分别触发验通行证、购买确认与发布训练，随后退出并恢复待机。短片使用编辑器离线回复。[31 秒城门交谈实录](Courtyard-Polish/Conversation.mp4)
- 在 30、72、90、120 FPS 的模拟输入下检查满推、轻推、反向、回中和菜单退出。满推以当前眼位为轴，在 0.12 秒内达到 65°/秒，回中后约 0.08 秒停转。
- 桌面点击与模拟手柄均验证了菜单的开关、语言勾记和焦点。切换语言后返回原谈话会保留已显示的回复页，等待时打开设置也不会重复发送请求。Unity 截图分别展示桌面中文和模拟头显英文菜单。[桌面设置](Courtyard-Polish/Menu-Desktop.png) · [模拟 XR 设置](Courtyard-Polish/Menu-XR.png)
- Unity 2022.3.62f3c1 最终回归通过 74 项 EditMode 和 19 项 PlayMode 测试。三项 DeepSeek 在线用例跳过。录制时 OpenXR 返回 `XR_ERROR_FORM_FACTOR_UNAVAILABLE`，实体 Quest 3 的转向手感和双眼文字可读性仍待真机确认。

### 历史记录 · 步行与近距离交谈验收 · 2026-11-24

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
