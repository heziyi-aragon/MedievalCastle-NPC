using System.Linq;
using MedievalCastle.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private const float HudRefreshInterval = .1f;
        private Canvas desktopGameplayHud;
        private Text desktopWallet, desktopBag, desktopObjective, desktopDirection;
        private float nextDesktopHudDraw;

        private (string wallet, string bag, string objective, string direction) GetGameplayHudText(Camera eye)
        {
            var state = gameplay.ReadPlayer();
            string wallet = T($"{state.gold} coins  ·  {state.reputation} rep", $"{state.gold} 金币  ·  {state.reputation} 声望");
            string bag = state.inventory.Length == 0 ? T("Backpack empty", "背包为空") :
                string.Join("  ·  ", state.inventory.Select(item =>
                    $"{T(CastleGame.ItemName(item.itemId), NpcGameplaySession.ChineseItemName(item.itemId))} ×{item.quantity}"));

            Transform NpcTarget(int index) => actors[index] && actors[index].isActiveAndEnabled
                ? actors[index].transform : null;

            string progress = $"{state.trainingProgress}/{state.trainingTotal}";
            int marker = state.trainingProgress + 1;
            string objective;
            Transform target;
            switch (gameplay.Objective)
            {
                case CastleObjective.GetPass:
                    objective = T("Get a pass from the merchant", "向商人购买通行证");
                    target = NpcTarget(1);
                    break;
                case CastleObjective.VerifyPass:
                    objective = T("Show your pass to the guard", "向守卫出示通行证");
                    target = NpcTarget(0);
                    break;
                case CastleObjective.StartTraining:
                    objective = T("Start training with the knight", "找骑士开始操练");
                    target = NpcTarget(2);
                    break;
                case CastleObjective.VisitMarker:
                    objective = T($"Training {progress}  ·  Reach marker {marker}", $"操练 {progress}  ·  前往 {marker} 号标记");
                    target = state.trainingProgress < trainingPoints.Length ? trainingPoints[state.trainingProgress] : null;
                    break;
                case CastleObjective.ClaimReward:
                    objective = T($"Training {progress}  ·  Return to the knight", $"操练 {progress}  ·  回骑士处领奖");
                    target = NpcTarget(2);
                    break;
                default:
                    objective = T($"Training {progress}  ·  Reward collected", $"操练 {progress}  ·  奖励已领取");
                    target = null;
                    break;
            }

            return (wallet, bag, objective, target ? DirectionTo(eye.transform, target.position) : "");
        }

        private string DirectionTo(Transform eye, Vector3 target)
        {
            Vector3 travel = Vector3.ProjectOnPlane(target - eye.position, Vector3.up);
            if (travel.sqrMagnitude <= 4f) return T("Nearby", "就在附近");
            int meters = Mathf.Max(1, Mathf.RoundToInt(travel.magnitude));
            string distance = T($"{meters} m", $"{meters} 米");
            Vector3 forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) return distance;
            float angle = Vector3.SignedAngle(forward, travel, Vector3.up);
            string bearing = Mathf.Abs(angle) > 145f ? T("behind", "后方") :
                angle < -25f ? T("left", "左侧") : angle > 25f ? T("right", "右侧") : T("ahead", "前方");
            return $"{distance}  ·  {bearing}";
        }

        private void RefreshDesktopGameplayHud(Camera eye)
        {
            if (!eye || MenuOpen || InConversation)
            {
                if (desktopGameplayHud) desktopGameplayHud.gameObject.SetActive(false);
                return;
            }
            if (!desktopGameplayHud) BuildDesktopGameplayHud();
            if (desktopGameplayHud.gameObject.activeSelf && Time.unscaledTime < nextDesktopHudDraw) return;

            nextDesktopHudDraw = Time.unscaledTime + HudRefreshInterval;
            var text = GetGameplayHudText(eye);
            desktopWallet.text = text.wallet;
            desktopBag.text = text.bag;
            desktopObjective.text = text.objective;
            desktopDirection.text = text.direction;
            desktopGameplayHud.gameObject.SetActive(true);
        }

        private void BuildDesktopGameplayHud()
        {
            var root = RectPart("Castle gameplay HUD", transform);
            desktopGameplayHud = root.gameObject.AddComponent<Canvas>();
            desktopGameplayHud.renderMode = RenderMode.ScreenSpaceOverlay;
            desktopGameplayHud.sortingOrder = 4;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1120, 680);

            RectTransform Card(string name, bool right, float width)
            {
                var rect = RectPart(name, root);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1 : 0, 1);
                rect.anchoredPosition = new Vector2(right ? -18 : 18, -18);
                rect.sizeDelta = new Vector2(width, 80);
                Paint(rect, PanelColor);
                var edge = RectPart("Gold edge", rect);
                Box(edge, 0, 0, 3, 80);
                Paint(edge, AccentColor);
                return rect;
            }

            var inventory = Card("Inventory", false, 298);
            desktopWallet = Label(inventory, "Wallet", 22, Parchment, 14, 7, 270, 28);
            desktopBag = Label(inventory, "Backpack", 16, MutedColor, 14, 36, 270, 36);
            var objective = Card("Objective", true, 326);
            desktopObjective = Label(objective, "Current objective", 20, Parchment, 14, 7, 298, 44);
            desktopDirection = Label(objective, "Direction", 17, AccentColor, 14, 51, 298, 22);
        }
    }
}
