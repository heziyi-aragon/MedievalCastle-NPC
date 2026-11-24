using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private GUISkin desktopSkin;
        private GUIStyle speechStyle;

        private bool clearTextFocus;
        private CastleLanguageMenu languageSheet;
        private bool seenMenuPointer;
        private Vector2 lastMenuPointer;
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
            RefreshWorldCards(camera, false);
            if (MenuOpen)
            {
                ShowLanguageSheet(camera, false);
                var mouse = Mouse.current;
                if (mouse != null)
                    SampleMenuPointer(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame,
                        mouse.leftButton.wasReleasedThisFrame);
            }
        }

        private void ShowLanguageSheet(Camera camera, bool headset)
        {
            if (languageSheet == null || !languageSheet.IsAlive)
            {
                languageSheet?.Destroy();
                languageSheet = new CastleLanguageMenu(transform, font);
            }
            languageSheet.Show(camera, headset, chinese, menuChoice);
        }

        private void ResetDialoguePresentation()
        {
            ReleaseWorldCards();
            clearTextFocus = true;
            headsetChoice = 0;
            replyPage = 0;
            nextHudDraw = 0;
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
            speechStyle = new GUIStyle(desktopSkin.label) { fontSize = 16, wordWrap = true };
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
            if (MenuOpen)
            {
                HandleMenuPointer(Event.current);
                return;
            }
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
                if (!InConversation) DrawExplorationHint(width, height);

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

        private void HandleMenuPointer(Event ev)
        {
            bool down = ev.type == EventType.MouseDown && ev.button == 0;
            bool up = ev.type == EventType.MouseUp && ev.button == 0;
            if (!down && !up && ev.type != EventType.MouseMove && ev.type != EventType.MouseDrag) return;
            var point = new Vector2(ev.mousePosition.x, Screen.height - ev.mousePosition.y);
            if (SampleMenuPointer(point, down, up) && (down || up)) ev.Use();
        }

        internal bool SampleMenuPointer(Vector2 point, bool pressed, bool released)
        {
            if (!MenuOpen || languageSheet == null || !languageSheet.IsAlive) return false;
            int hover = languageSheet.PointerRow(point);
            bool moved = seenMenuPointer && (point - lastMenuPointer).sqrMagnitude >= .25f;
            seenMenuPointer = true;
            lastMenuPointer = point;
            if (hover >= 0 && (moved || pressed || released)) menuChoice = hover;
            int clicked = languageSheet.ClickRow(pressed, released, hover);
            if (clicked >= 0) SetLanguage(clicked == 1);
            languageSheet.Show(null, false, chinese, menuChoice);
            return hover >= 0;
        }

    }
}
