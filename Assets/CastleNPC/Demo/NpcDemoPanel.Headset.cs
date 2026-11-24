using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private enum HudChoice { Prompt1, Prompt2, Prompt3, NextPage, Retry, End, Resume, English, Chinese }
        private readonly HudChoice[] hudChoices = new HudChoice[5];
        private readonly StringBuilder optionText = new StringBuilder(192);
        private int choiceCount, headsetChoice, menuChoice, replyPage;
        private float nextHudDraw;
        private bool primaryHeld, backHeld, stickHeld, handConnected;
        private Camera headsetCamera;
        private Canvas headsetCanvas;
        private RectTransform hudRect;
        private Text hudTitle, hudBody, hudOptions, hudHint;

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
                menuChoice = 0;
            }
            else if (!MenuOpen && !InConversation)
            {
                if (confirm && !wasTalking) BeginNearby(camera.transform);
            }
            else
            {
                SetHudChoices();
                int index = MenuOpen ? menuChoice : headsetChoice;
                index = Mathf.Clamp(index, 0, choiceCount - 1);
                if (navigate)
                    index = (index + (stickY < 0 ? 1 : -1) + choiceCount) % choiceCount;
                if (MenuOpen) menuChoice = index;
                else headsetChoice = index;
                if (confirm) ActivateChoice(hudChoices[index]);
            }

            if (menu || confirm || navigate || previousNearby != Nearby || wasTalking != InConversation || Time.unscaledTime >= nextHudDraw)
            {
                nextHudDraw = Time.unscaledTime + .1f;
                PaintHud();
            }
        }

        private int PageLength => chinese ? 48 : 112;
        private bool HasPages => result != null && result.Success && result.Reply.Length > PageLength;

        private void SetHudChoices()
        {
            choiceCount = 0;
            if (MenuOpen)
            {
                hudChoices[choiceCount++] = HudChoice.Resume;
                hudChoices[choiceCount++] = HudChoice.English;
                hudChoices[choiceCount++] = HudChoice.Chinese;
                return;
            }
            if (Failed) hudChoices[choiceCount++] = HudChoice.Retry;
            else if (!Pending)
            {
                if (HasPages) hudChoices[choiceCount++] = HudChoice.NextPage;
                hudChoices[choiceCount++] = HudChoice.Prompt1;
                hudChoices[choiceCount++] = HudChoice.Prompt2;
                hudChoices[choiceCount++] = HudChoice.Prompt3;
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
                case HudChoice.Resume: return T("Resume", "继续游戏");
                case HudChoice.English: return chinese ? "English" : "English  •";
                case HudChoice.Chinese: return chinese ? "中文  •" : "中文";
                default: return T("End conversation", "结束交谈");
            }
        }

        private void ActivateChoice(HudChoice choice)
        {
            switch (choice)
            {
                case HudChoice.Prompt1: case HudChoice.Prompt2: case HudChoice.Prompt3: Submit(Prompt((int)choice)); break;
                case HudChoice.NextPage: replyPage = (replyPage + 1) % ((result.Reply.Length + PageLength - 1) / PageLength); break;
                case HudChoice.Retry: Retry(); break;
                case HudChoice.End: EndTalk(); break;
                case HudChoice.Resume: ToggleMenu(); break;
                case HudChoice.English: SetLanguage(false); break;
                case HudChoice.Chinese: SetLanguage(true); break;
            }
        }

        private void PaintHud()
        {
            bool exploring = !MenuOpen && !InConversation;
            string line = exploring ? ExploreText(true) : MenuOpen ? "" : ConversationBody();
            bool visible = !exploring || !string.IsNullOrEmpty(line);
            if (headsetCanvas.gameObject.activeSelf != visible) headsetCanvas.gameObject.SetActive(visible);
            if (!visible) return;
            float top = 10f;
            top = SetHudLine(hudTitle, top, MenuOpen ? T("Settings", "设置") : exploring ? "" : RoleName(role), 38);
            if (!MenuOpen && InConversation && HasPages)
            {
                int pages = (result.Reply.Length + PageLength - 1) / PageLength;
                replyPage = Mathf.Clamp(replyPage, 0, pages - 1);
                int start = replyPage * PageLength;
                line = "(" + (replyPage + 1) + "/" + pages + ") " +
                    result.Reply.Substring(start, Mathf.Min(PageLength, result.Reply.Length - start));
            }
            top = SetHudLine(hudBody, top, line, exploring ? 84 : 96);
            string options = "";
            if (!exploring)
            {
                SetHudChoices();
                int index = Mathf.Clamp(MenuOpen ? menuChoice : headsetChoice, 0, choiceCount - 1);
                optionText.Clear();
                for (int i = 0; i < choiceCount; i++)
                {
                    if (i > 0) optionText.Append('\n');
                    optionText.Append(i == index ? "›   " : "     ").Append(ChoiceLabel(hudChoices[i]));
                }
                options = optionText.ToString();
            }
            top = SetHudLine(hudOptions, top, options, choiceCount * 36f);
            string hint = exploring ? "" : T("Right stick ↑↓   ·   A  Confirm     B  Menu", "右摇杆 ↑↓ 选择     A 确认     B 菜单");
            top = SetHudLine(hudHint, top, hint, 30);
            hudRect.sizeDelta = new Vector2(740, top + 10);
        }

        private static float SetHudLine(Text text, float top, string value, float maxHeight)
        {
            text.text = value;
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
            var panel = new GameObject("NPC headset dialogue", typeof(RectTransform), typeof(Canvas), typeof(Image));
            hudRect = (RectTransform)panel.transform;
            hudRect.SetParent(camera.transform, false);
            // Keep the upper edge below eye level as a long reply expands the panel.
            hudRect.pivot = new Vector2(.5f, 1f);
            hudRect.localPosition = new Vector3(0, -.12f, 1.10f);
            hudRect.localScale = Vector3.one * .00122f;
            hudRect.sizeDelta = new Vector2(740, 104);
            var background = panel.GetComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;
            headsetCanvas = panel.GetComponent<Canvas>();
            headsetCanvas.renderMode = RenderMode.WorldSpace;
            headsetCanvas.worldCamera = camera;
            hudTitle = AddHudLine("Role", 28, AccentColor);
            hudBody = AddHudLine("Reply", 25, Color.white);
            hudOptions = AddHudLine("Selection", 24, Color.white);
            hudHint = AddHudLine("Controls", 21, MutedColor);
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
