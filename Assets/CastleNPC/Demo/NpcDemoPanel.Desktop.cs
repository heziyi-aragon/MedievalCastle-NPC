using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private GUISkin desktopSkin;
        private GUIStyle headingStyle, captionStyle, speechStyle, promptStyle;
        private Vector2 desktopScroll;
        private bool clearTextFocus;
        private readonly DesktopKey talkKey = new DesktopKey(), menuKey = new DesktopKey();

        // Game View can deliver IMGUI key events without updating Keyboard.current. Queue them
        // for the same Update path as hardware input and discard its echo on either side of a frame.
        private sealed class DesktopKey
        {
            private bool guiHeld, guiQueued;
            private int hardwareFrame = -10, guiOnlyFrame = -10;

            public void FromGui(bool down, int frame)
            {
                if (down && !guiHeld && frame - hardwareFrame > 1) guiQueued = true;
                guiHeld = down;
            }

            public bool Sample(bool hardwareDown, int frame)
            {
                bool queued = guiQueued;
                guiQueued = false;
                if (hardwareDown)
                {
                    hardwareFrame = frame;
                    bool echo = frame - guiOnlyFrame <= 1;
                    guiOnlyFrame = -10;
                    return !echo || queued;
                }
                if (queued) guiOnlyFrame = frame;
                return queued;
            }

            public void Reset()
            {
                guiHeld = guiQueued = false;
                hardwareFrame = guiOnlyFrame = -10;
            }
        }

        private static readonly Color PanelColor = new Color(.065f, .083f, .085f, .96f);
        private static readonly Color AccentColor = new Color(.94f, .78f, .49f);
        private static readonly Color MutedColor = new Color(.75f, .80f, .79f);

        private void UpdateDesktop()
        {
            if (XRSettings.isDeviceActive) return;
            var keys = Keyboard.current;
            int frame = Time.frameCount;
            bool e = keys != null && keys.eKey.wasPressedThisFrame;
            bool esc = keys != null && keys.escapeKey.wasPressedThisFrame;
            HandleDesktopFrame(Camera.main, talkKey.Sample(e, frame), menuKey.Sample(esc, frame));
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            talkKey.Reset();
            menuKey.Reset();
        }

        private void HandleGuiKeys(Event current)
        {
            bool down = current.type == EventType.KeyDown;
            if (!down && current.type != EventType.KeyUp) return;
            if (current.keyCode == KeyCode.Escape)
            {
                menuKey.FromGui(down, Time.frameCount);
                current.Use();
            }
            else if (current.keyCode == KeyCode.E)
            {
                // Do not intercept text input or IME composition while a conversation is open.
                if (!down || (!MenuOpen && !InConversation))
                    talkKey.FromGui(down, Time.frameCount);
            }
        }

        internal void HandleDesktopFrame(Camera camera, bool interactPressed, bool menuPressed)
        {
            if (!camera) return;
            UpdateNearby(camera.transform);
            if (menuPressed)
            {
                ToggleMenu();
                clearTextFocus = true;
            }
            else if (!MenuOpen && !InConversation && interactPressed)
                BeginNearby(camera.transform);
        }

        private void ResetDialoguePresentation()
        {
            desktopScroll = Vector2.zero;
            clearTextFocus = true;
            headsetChoice = 0;
            replyPage = 0;
            nextHudDraw = 0;
        }

        private string ConversationBody()
        {
            if (Pending) return T("Waiting for a reply…", "正在等待回复…");
            if (Failed) return T("Unable to get a reply right now.", "暂时无法取得回复。");
            return result?.Reply ?? "";
        }

        private void BuildDesktopSkin(GUISkin original)
        {
            desktopSkin = Instantiate(original);
            if (font != null) desktopSkin.font = font;
            foreach (var style in new[] { desktopSkin.label, desktopSkin.button, desktopSkin.textField })
            {
                style.font = desktopSkin.font;
                style.fontSize = 15;
                style.richText = false;
                style.fixedHeight = 0;
            }
            desktopSkin.button.wordWrap = true;
            desktopSkin.button.padding = new RectOffset(10, 10, 4, 4);
            desktopSkin.textField.padding = new RectOffset(8, 8, 5, 4);
            headingStyle = new GUIStyle(desktopSkin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            headingStyle.normal.textColor = AccentColor;
            captionStyle = new GUIStyle(desktopSkin.label) { fontSize = 13, wordWrap = true };
            captionStyle.normal.textColor = MutedColor;
            speechStyle = new GUIStyle(desktopSkin.label) { fontSize = 16, wordWrap = true };
            promptStyle = new GUIStyle(desktopSkin.button) { alignment = TextAnchor.MiddleLeft };
        }

        private static void Fill(Rect bounds, Color color)
        {
            var saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = saved;
        }

        private void DrawBackdrop(Rect bounds)
        {
            Fill(bounds, PanelColor);
            Fill(new Rect(bounds.x, bounds.y, 3, bounds.height), AccentColor);
        }

        private void OnGUI()
        {
            if (XRSettings.isDeviceActive) return;
            HandleGuiKeys(Event.current);
            var original = GUI.skin;
            if (!desktopSkin) BuildDesktopSkin(original);
            var matrix = GUI.matrix;
            GUI.skin = desktopSkin;
            try
            {
                float scale = Mathf.Clamp(Screen.width / 1100f, .85f, 1.55f);
                GUI.matrix = matrix * Matrix4x4.Scale(new Vector3(scale, scale, 1));
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                if (clearTextFocus)
                {
                    GUI.FocusControl(null);
                    clearTextFocus = false;
                }
                if (MenuOpen) DrawMenu(width, height);
                else if (InConversation) DrawTalk(width, height);
                else DrawExplorationHint(width, height);
            }
            finally { GUI.skin = original; GUI.matrix = matrix; }
        }

        private void DrawExplorationHint(float width, float height)
        {
            var line = ExploreText(false);
            if (string.IsNullOrEmpty(line)) return;
            float w = Mathf.Min(width - 24, speechStyle.CalcSize(new GUIContent(line)).x + 42);
            float h = speechStyle.CalcHeight(new GUIContent(line), w - 34) + 18;
            var bounds = new Rect((width - w) * .5f, height - h - 19, w, h);
            DrawBackdrop(bounds);
            GUI.Label(new Rect(bounds.x + 17, bounds.y + 8, w - 34, h - 12), line, speechStyle);
        }

        private void DrawMenu(float width, float height)
        {
            float w = Mathf.Min(340, width - 24);
            const float h = 174;
            var bounds = new Rect((width - w) * .5f, Mathf.Max(4, (height - h) * .5f), w, h);
            DrawBackdrop(bounds);
            GUILayout.BeginArea(new Rect(bounds.x + 18, bounds.y + 10, w - 36, h - 20));
            GUILayout.Label(T("Settings", "设置"), headingStyle);
            GUILayout.Space(4);
            GUILayout.Label(T("Language", "语言"), captionStyle);
            int selected = GUILayout.Toolbar(chinese ? 1 : 0, new[] { "English", "中文" }, GUILayout.Height(31));
            if (selected != (chinese ? 1 : 0)) SetLanguage(selected == 1);
            GUILayout.Space(10);
            if (GUILayout.Button(T("Resume", "继续游戏"), GUILayout.Height(32))) ToggleMenu();
            GUILayout.EndArea();
        }

        private void DrawTalk(float width, float height)
        {
            float w = Mathf.Min(width - 24, Mathf.Clamp(width * .40f, 310, 430));
            float h = Mathf.Min(height - 24, 325);
            var bounds = new Rect(width - w - 12, height - h - 12, w, h);
            DrawBackdrop(bounds);
            GUILayout.BeginArea(new Rect(bounds.x + 14, bounds.y + 9, w - 28, h - 18));
            GUILayout.BeginHorizontal();
            GUILayout.Label(RoleName(role), headingStyle);
            bool finish = GUILayout.Button(T("End conversation", "结束交谈"), GUILayout.Width(chinese ? 89 : 146), GUILayout.Height(28));
            GUILayout.EndHorizontal();
            desktopScroll = GUILayout.BeginScrollView(desktopScroll);
            if (!string.IsNullOrWhiteSpace(sentText))
                GUILayout.Label(T("You · ", "你 · ") + sentText, captionStyle);
            var line = ConversationBody();
            if (line.Length > 0) GUILayout.Label(line, speechStyle);
            if (Failed)
            {
                if (GUILayout.Button(T("Retry", "重试"), GUILayout.MinHeight(30))) Retry();
            }
            else if (!Pending)
            {
                for (int i = 0; i < 3; i++)
                    if (GUILayout.Button(Prompt(i), promptStyle, GUILayout.MinHeight(29))) Submit(Prompt(i));
                DrawMessageInput();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (finish) EndTalk();
        }

        private void DrawMessageInput()
        {
            var ev = Event.current;
            bool enter = ev.type == EventType.KeyDown &&
                (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "NpcMessage" && string.IsNullOrEmpty(Input.compositionString);
            if (enter) ev.Use();
            GUILayout.Space(4);
            GUILayout.Label(T("Your question", "你的问题"), captionStyle);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("NpcMessage");
            typed = GUILayout.TextField(typed, 1000, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true), GUILayout.MinHeight(29));
            bool canSend = !string.IsNullOrWhiteSpace(typed);
            var wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && canSend;
            bool send = GUILayout.Button(T("Send", "发送"), GUILayout.Width(62), GUILayout.MinHeight(29));
            GUI.enabled = wasEnabled;
            GUILayout.EndHorizontal();
            if (canSend && (send || enter))
            {
                Submit(typed);
                typed = "";
            }
        }
    }
}
