# COMP5140 游戏机制与交互界面

在 Unity 2022.3.62f3c1 打开 `Assets/GameMechanics/Demo/MechanicsDemo.unity`，进入 Play。用 WASD 移动，点击游戏画面后用鼠标转向。靠近 NPC 按 E，输入消息，点击 Send 或按回车。用 Esc 退出对话或释放鼠标。

| NPC | 演示输入 | 游戏反馈 |
|---|---|---|
| Gate Guard | `Open the gate` | 没有通行证则拒绝。持证后升起城门 |
| Merchant | `What do you sell?`、`Buy a pass`、`Buy a potion` | 通行证 10 金币，药水 8 金币。成交后同步扣款和更新背包 |
| Training Knight | `Accept training`、`Claim my reward` | 依次走过 1、2、3 号训练点，再回来领取 20 金币和 5 声望 |

初始状态为 30 金币。先向守卫申请通行并观察拒绝结果，再向商人买票，回到守卫处开门。完成骑士任务后的余额是 40 金币。退出 Play 或重启试玩可重置状态。

试玩使用 `LocalNpcDecisions`。这个本地模拟器支持表内的英文指令，也识别“开门”“买通行证”“买药水”“接受训练”“领取奖励”等短句。它用于离线演示流程。生产接入由下面的 provider 接口提供。

## 对接场景和 LLM

在正式场景中添加一个 `CastleInteraction` 组件，指定玩家 `CastlePlayer`（和玩家摄像机）、城门 `GateView`、带稳定 ID 和角色的三个 `NpcActor`、按顺序排列的训练点 Transform，以及一个 `NpcDecisionProvider`。给城门配置 Collider、`localTravel` 和速度。将 NPC 和训练点的 Transform 放在玩家可接近的地面高度。启动时会创建 HUD 和对话窗口，并在场景缺少 EventSystem 时创建旧输入系统的 EventSystem。

实现 `NpcDecisionProvider.DecideAsync(NpcRequest request, CancellationToken cancellationToken)`。请求携带 `requestId`、`npcId`、`role`、`message`、`player`（金币、背包、声望、城门、任务状态和训练进度）及上一条动作结果 `previousResult`。回复可用 `NpcDecisionProvider.DecodeJson` 解析，例如：

```json
{"reply":"I have a pass for you.","action":{"type":"BUY_ITEM","itemId":"castle_pass","quantity":1}}
```

动作值为 `NONE`、`OPEN_GATE`、`BUY_ITEM`、`ACCEPT_QUEST` 或 `CLAIM_REWARD`。`NONE` 必须提供正文。购买动作必须提供 `itemId` 和正整数 `quantity`。NPC 角色、价格、任务进度和奖励由 Unity 校验。游戏显示 Unity 返回的执行结果。请把模型回复写成角色对白，避免在验证前宣称交易或奖励已经完成。异步请求请遵守取消信号；关窗或超时后，延迟的回复会被丢弃。

正式城堡场景原有的 glTFast 本地包路径和 Git LFS 资产由场景负责人提供。机制试玩场景使用 Unity 基础模型，可单独验证。
