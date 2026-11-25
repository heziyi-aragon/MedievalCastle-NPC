using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private enum HudChoice { Prompt1, Prompt2, Prompt3, NextPage, Retry, ConfirmPurchase, CancelPurchase, ClaimTrainingReward, End }
        private readonly HudChoice[] hudChoices = new HudChoice[5];

        private int choiceCount, headsetChoice, menuChoice, replyPage;
        private float nextHudDraw;
        private bool primaryHeld, backHeld, stickHeld, handConnected, replyIsChinese;
        private Camera headsetCamera;
        private Canvas headsetCanvas;
        private RectTransform hudRect;
        private Text hudObjective, hudDirection, hudWallet, hudBag, hudBody;

        private void UpdateHeadset()
        {
            if (!XRSettings.isDeviceActive)
            {
                if (headsetCanvas && headsetCanvas.gameObject.activeSelf) headsetCanvas.gameObject.SetActive(false);
                handConnected = false;
                return;
            }
            var camera = Camera.main;
            if (!camera) return;
            var hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            hand.TryGetFeatureValue(CommonUsages.primaryButton, out bool a);
            hand.TryGetFeatureValue(CommonUsages.secondaryButton, out bool b);
            hand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
            HandleHeadsetFrame(camera, hand.isValid, a, b, stick.y);
        }

        internal void HandleHeadsetFrame(Camera camera, bool connected, bool a, bool b, float stickY)
        {
            if (!camera) return;
            if (!headsetCanvas || headsetCamera != camera)
            {
                if (headsetCanvas) Destroy(headsetCanvas.gameObject);
                headsetCamera = camera;
                BuildHud(camera);
            }

            var previousNearby = Nearby;
            bool wasTalking = InConversation;
            UpdateNearby(camera.transform);
            // On reconnect the user may already be holding A. Require a fresh press after release.
            bool confirm = connected && handConnected && a && !primaryHeld;
            bool menu = connected && handConnected && b && !backHeld;
            bool navigate = connected && handConnected && !stickHeld && Mathf.Abs(stickY) > .65f;
            handConnected = connected;
            primaryHeld = a;
            backHeld = b;
            if (Mathf.Abs(stickY) < .30f || !connected) stickHeld = false;
            else if (Mathf.Abs(stickY) > .65f) stickHeld = true;

            if (menu)
            {
                ToggleMenu();
            }
            else if (MenuOpen)
            {
                if (navigate) menuChoice = 1 - menuChoice;
                if (confirm) SetLanguage(menuChoice == 1);
            }
            else if (!InConversation)
            {
                if (confirm && !wasTalking) BeginNearby(camera.transform);
            }
            else
            {
                SetHudChoices();
                int index = Mathf.Clamp(headsetChoice, 0, choiceCount - 1);
                if (navigate)
                    index = (index + (stickY < 0 ? 1 : -1) + choiceCount) % choiceCount;
                headsetChoice = index;
                if (confirm) ActivateChoice(hudChoices[index]);
            }

            if (menu || confirm || navigate || previousNearby != Nearby || wasTalking != InConversation || Time.unscaledTime >= nextHudDraw)
            {
                nextHudDraw = Time.unscaledTime + HudRefreshInterval;
                PaintHud();
            }
            else if (InConversation && !MenuOpen && questions) FacePlayer(camera);
        }

        private int PageLength => replyIsChinese ? 48 : 112;
        private bool HasPages => SpokenLine.Length > PageLength && result != null && result.Success;
        private bool ShowTrainingRewardChoice => role == 2 && gameplay.CanClaimTrainingReward;

        private void SetHudChoices()
        {
            choiceCount = 0;
            if (Failed) hudChoices[choiceCount++] = HudChoice.Retry;
            else if (gameplay.PendingPurchase != null)
            {
                hudChoices[choiceCount++] = HudChoice.ConfirmPurchase;
                hudChoices[choiceCount++] = HudChoice.CancelPurchase;
                if (HasPages) hudChoices[choiceCount++] = HudChoice.NextPage;
            }
            else if (!Pending)
            {
                if (HasPages) hudChoices[choiceCount++] = HudChoice.NextPage;
                hudChoices[choiceCount++] = HudChoice.Prompt1;
                hudChoices[choiceCount++] = HudChoice.Prompt2;
                hudChoices[choiceCount++] = ShowTrainingRewardChoice ? HudChoice.ClaimTrainingReward : HudChoice.Prompt3;
            }
            hudChoices[choiceCount++] = HudChoice.End;
        }

        private string ChoiceLabel(HudChoice choice)
        {
            switch (choice)
            {
                case HudChoice.Prompt1: case HudChoice.Prompt2: case HudChoice.Prompt3: return Prompt((int)choice);
                case HudChoice.NextPage: return T("Next page", "翻页");
                case HudChoice.Retry: return T("Retry", "重试");
                case HudChoice.ConfirmPurchase: return T("Confirm purchase", "确认购买");
                case HudChoice.CancelPurchase: return T("Cancel purchase", "取消购买");
                case HudChoice.ClaimTrainingReward: return T("Confirm reward", "确认领奖");
                default: return T("End conversation", "结束交谈");
            }
        }

        private void ActivateChoice(HudChoice choice)
        {
            switch (choice)
            {
                case HudChoice.Prompt1:
                    Submit(Prompt(0), actors[role].RoleId == "knight" ? NpcAction.OfferTraining : NpcAction.None);
                    break;
                case HudChoice.Prompt2: case HudChoice.Prompt3: Submit(Prompt((int)choice)); break;
                case HudChoice.NextPage:
                    if (HasPages) replyPage = (replyPage + 1) % ((SpokenLine.Length + PageLength - 1) / PageLength);
                    break;
                case HudChoice.Retry: Retry(); break;
                case HudChoice.ConfirmPurchase: case HudChoice.CancelPurchase:
                    gameOutcome = gameplay.FinishPurchase(service, choice == HudChoice.ConfirmPurchase, chinese);
                    break;
                case HudChoice.ClaimTrainingReward:
                    if (role != 2 || Pending) break;
                    gameOutcome = gameplay.ClaimTrainingReward(chinese);
                    StopVoice();
                    result = null;
                    break;
                case HudChoice.End: EndTalkWithFarewell(); break;
            }
        }

        private void PaintHud()
        {
            RefreshWorldCards(headsetCamera, true);
            if (MenuOpen || InConversation)
            {
                headsetCanvas.gameObject.SetActive(false);
                if (MenuOpen) ShowLanguageSheet(headsetCamera, true);
                return;
            }

            headsetCanvas.gameObject.SetActive(true);
            var text = GetGameplayHudText(headsetCamera);
            float height = SetHudLine(hudObjective, 10, text.objective, 60);
            height = SetHudLine(hudDirection, height, text.direction, 34);
            height = SetHudLine(hudWallet, height + 4, text.wallet, 35);
            height = SetHudLine(hudBag, height, text.bag, 60);
            height = SetHudLine(hudBody, height + 4, ExploreText(true), 60);
            hudRect.sizeDelta = new Vector2(680, height + 8);
        }
        private static float SetHudLine(Text text, float top, string value, float maxHeight)
        {
            if (text.text != value) text.text = value;
            bool visible = !string.IsNullOrEmpty(value);
            if (text.gameObject.activeSelf != visible) text.gameObject.SetActive(visible);
            if (!visible) return top;
            var rect = text.rectTransform;
            float height = Mathf.Min(maxHeight, Mathf.Ceil(text.preferredHeight) + 4);
            rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(-40, height);
            return top + height + 2;
        }

        private void BuildHud(Camera camera)
        {
            var panel = new GameObject("NPC exploration hint", typeof(RectTransform), typeof(Canvas), typeof(Image));
            hudRect = (RectTransform)panel.transform;
            hudRect.SetParent(camera.transform, false);
            hudRect.pivot = new Vector2(.5f, 1f);
            hudRect.localPosition = new Vector3(0, -.20f, 1.10f);
            hudRect.localScale = Vector3.one * .00113f;
            hudRect.sizeDelta = new Vector2(680, 180);
            var background = panel.GetComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;
            headsetCanvas = panel.GetComponent<Canvas>();
            headsetCanvas.renderMode = RenderMode.WorldSpace;
            headsetCanvas.worldCamera = camera;
            hudObjective = AddHudLine("Objective", 26, Parchment);
            hudDirection = AddHudLine("Direction", 21, AccentColor);
            hudWallet = AddHudLine("Wallet", 23, Parchment);
            hudBag = AddHudLine("Backpack", 21, MutedColor);
            hudBody = AddHudLine("Hint", 25, Color.white);
            nextHudDraw = 0;
        }

        private Text AddHudLine(string name, int size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(hudRect, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(-40, 60);
            var text = obj.GetComponent<Text>();
            text.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.alignment = TextAnchor.UpperLeft;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 19;
            text.resizeTextMaxSize = size;
            return text;
        }
    }
}
