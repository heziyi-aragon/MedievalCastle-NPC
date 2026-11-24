using System;
using UnityEngine;
using UnityEngine.XR;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private Vector2 desktopScroll;
        private bool desktopOpen = true, debugOpen, stateOpen;
        private string draftState = "", draftError = "";
        private GUIStyle heading, caption, body, tabs, panelStyle;
        private GUISkin desktopSkin;
        private Camera desktopCamera;
        private Rect originalRect;
        private float originalFov;
        private int layoutWidth, layoutHeight;
        private bool layoutOpen;

        private static float PanelWidth(float guiWidth)
        {
            return Mathf.Min(Mathf.Clamp(guiWidth * .40f, 295f, 390f), guiWidth - 16f);
        }

        private void LateUpdate()
        {
            if (XRSettings.isDeviceActive) { RestoreDesktopCamera(); return; }
            if (!desktopCamera)
            {
                desktopCamera = Camera.main;
                if (!desktopCamera) return;
                originalRect = desktopCamera.rect;
                originalFov = desktopCamera.fieldOfView;
            }
            if (layoutWidth == Screen.width && layoutHeight == Screen.height && layoutOpen == desktopOpen) return;
            layoutWidth = Screen.width;
            layoutHeight = Screen.height;
            layoutOpen = desktopOpen;
            float scale = Mathf.Clamp(layoutWidth / 970f, .8f, 1f);
            float reserved = desktopOpen ? (PanelWidth(layoutWidth / scale) + 16f) * scale : 0f;
            float sceneWidth = Mathf.Max(layoutWidth * .2f, layoutWidth - reserved);
            desktopCamera.rect = new Rect(0, 0, sceneWidth / layoutWidth, 1);

            // Fit the cast inside the part of the Game view left for the castle.
            float aspect = sceneWidth / Mathf.Max(1, layoutHeight);
            desktopCamera.aspect = aspect;
            float halfAngle = 0f;
            foreach (var actor in characters.Values)
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
            {
                Bounds b = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3((corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y, (corner & 4) == 0 ? b.min.z : b.max.z);
                    point = desktopCamera.transform.InverseTransformPoint(point);
                    if (point.z > desktopCamera.nearClipPlane)
                        halfAngle = Mathf.Max(halfAngle, Mathf.Max(Mathf.Abs(point.x) / aspect, Mathf.Abs(point.y)) / point.z);
                }
            }
            desktopCamera.fieldOfView = Mathf.Max(originalFov, 2f * Mathf.Atan(halfAngle * 1.10f) * Mathf.Rad2Deg);
        }

        private void RestoreDesktopCamera()
        {
            if (!desktopCamera) return;
            desktopCamera.rect = originalRect;
            desktopCamera.ResetAspect();
            desktopCamera.fieldOfView = originalFov;
            desktopCamera = null;
            layoutWidth = layoutHeight = 0;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F2)
            {
                desktopOpen = !desktopOpen;
                Event.current.Use();
            }
            var previous = GUI.skin;
            var previousMatrix = GUI.matrix;
            if (desktopSkin == null)
            {
                desktopSkin = Instantiate(previous);
                if (font != null) desktopSkin.font = font;
                foreach (var style in new[] { desktopSkin.button, desktopSkin.label, desktopSkin.toggle,
                    desktopSkin.textField, desktopSkin.textArea, desktopSkin.box })
                {
                    style.font = desktopSkin.font;
                    style.fontSize = 14;
                    style.fixedHeight = 0;
                    style.richText = false;
                }
                desktopSkin.button.wordWrap = true;
                desktopSkin.button.padding = new RectOffset(7, 7, 4, 4);
                desktopSkin.button.margin = new RectOffset(2, 2, 2, 2);
                desktopSkin.textField.wordWrap = false;
                desktopSkin.textField.padding = new RectOffset(7, 7, 4, 4);
                desktopSkin.textArea.wordWrap = true;
                desktopSkin.textArea.padding = new RectOffset(7, 7, 4, 4);
                desktopSkin.label.wordWrap = true;
                tabs = new GUIStyle(desktopSkin.button) { name = "CastleNpcTab", padding = new RectOffset(3, 3, 3, 3) };
                heading = new GUIStyle(desktopSkin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                caption = new GUIStyle(desktopSkin.label) { fontSize = 13 };
                body = new GUIStyle(desktopSkin.label) { fontSize = 15 };
                panelStyle = new GUIStyle { padding = new RectOffset(9, 9, 7, 7) };
            }
            GUI.skin = desktopSkin;
            try
            {
                float scale = Mathf.Clamp(Screen.width / 970f, .8f, 1f);
                GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1));
                float fullWidth = Screen.width / scale;
                float fullHeight = Screen.height / scale;
                if (!desktopOpen)
                {
                    if (GUI.Button(new Rect(fullWidth - 127, 12, 115, 30), T("Talk · F2", "NPC 对话 · F2"))) desktopOpen = true;
                    return;
                }
                float width = PanelWidth(fullWidth);
                var panelRect = new Rect(fullWidth - width - 8, 8, width, fullHeight - 16);
                var color = GUI.color;
                GUI.color = new Color(.055f, .073f, .09f, .96f);
                GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
                GUI.color = color;
                GUILayout.BeginArea(panelRect, panelStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label(T("Castle dialogue", "城堡对话"), heading, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                if (GUILayout.Button(chinese ? "English" : "中文", GUILayout.Width(67), GUILayout.MinHeight(28))) chinese = !chinese;
                if (GUILayout.Button(T("Hide", "收起"), GUILayout.Width(54), GUILayout.MinHeight(28))) desktopOpen = false;
                GUILayout.EndHorizontal();
                desktopScroll = GUILayout.BeginScrollView(new Vector2(0, desktopScroll.y), false, true,
                    GUIStyle.none, desktopSkin.verticalScrollbar);
                float scrollbar = Mathf.Max(16, desktopSkin.verticalScrollbar.fixedWidth) + desktopSkin.verticalScrollbar.margin.left;
                GUILayout.BeginVertical(GUILayout.Width(width - panelStyle.padding.horizontal - scrollbar - 6));
                DrawConversation();
                DrawInspector();
                GUILayout.EndVertical();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally { GUI.skin = previous; GUI.matrix = previousMatrix; }
        }

        private void DrawConversation()
        {
            GUI.enabled = !Pending;
            int next = GUILayout.Toolbar(role, new[] { RoleName(0), RoleName(1), RoleName(2) }, tabs, GUILayout.MinHeight(28));
            SelectRole(next);
            int mode = service.Mode == NpcProviderMode.Offline ? 1 : 0;
            int nextMode = GUILayout.Toolbar(mode, new[] { T("Live · DeepSeek", "在线 · DeepSeek"), T("Offline demo", "离线演示") }, tabs, GUILayout.MinHeight(28));
            if (nextMode != mode) ToggleMode();
            if (service.Mode == NpcProviderMode.DeepSeek && !hasApiKey)
                GUILayout.Label(T("Live requires DEEPSEEK_API_KEY.", "在线模式需要设置 DEEPSEEK_API_KEY。"), caption);
            GUILayout.Space(5);
            for (int i = 0; i < 3; i++)
                if (GUILayout.Button(Prompt(i), GUILayout.MinHeight(25))) Submit(Prompt(i));
            GUILayout.Label(T("Your message", "输入你的问题"), caption);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("NpcMessage");
            typed = GUILayout.TextField(typed, 1000, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true), GUILayout.MinHeight(30));
            var ev = Event.current;
            bool submitKey = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == "NpcMessage" && string.IsNullOrEmpty(Input.compositionString);
            GUI.enabled = !Pending && !string.IsNullOrWhiteSpace(typed);
            if ((GUILayout.Button(T("Send", "发送"), GUILayout.Width(64), GUILayout.MinHeight(30)) || submitKey) && GUI.enabled)
            {
                Submit(typed);
                if (submitKey) ev.Use();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (sentText.Length == 0) return;
            GUILayout.Space(5);
            GUILayout.Label(RoleName(sentRole) + " · " + sentText, caption);
            if (Pending)
            {
                GUILayout.Label(T("Waiting for a reply…", "正在等待回复…"), body);
                if (GUILayout.Button(T("Cancel request", "取消请求"))) cancellation?.Cancel();
                return;
            }
            if (Failed)
            {
                GUILayout.Label(error, body);
                if (GUILayout.Button(T("Retry last request", "重试上次请求"))) SendSaved();
                return;
            }
            if (result == null) return;
            GUILayout.Label(result.Reply, body);
            GUILayout.Label((result.IsOffline ? T("Offline", "离线") : T("Live", "在线")) + " · " + result.Decision.action, caption);
            if (CanReport)
            {
                GUILayout.Label(T("Report a simulated game outcome", "回传模拟游戏结果"), caption);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(T("Accepted", "已通过"))) Report(true);
                if (GUILayout.Button(T("Rejected", "已拒绝"))) Report(false);
                GUILayout.EndHorizontal();
            }
            if (outcome.Length > 0) GUILayout.Label(T("Reported · ", "已回传 · ") + outcome, caption);
        }

        private void DrawInspector()
        {
            GUILayout.Space(7);
            stateOpen = GUILayout.Toggle(stateOpen, T("Edit demo player state", "编辑演示玩家状态"), GUI.skin.button);
            if (stateOpen)
            {
                GUI.enabled = !Pending;
                bool pass = GUILayout.Toggle(state.hasPass, T("Carrying a pass", "持有通行证"));
                if (pass != state.hasPass) { state.hasPass = pass; draftState = ""; }
                GUILayout.BeginHorizontal();
                GUILayout.Label(T("Coins", "金币") + "  " + state.coins);
                if (GUILayout.Button("−1", GUILayout.Width(45))) { state.coins = Mathf.Max(0, state.coins - 1); draftState = ""; }
                if (GUILayout.Button("+1", GUILayout.Width(45))) { if (state.coins < int.MaxValue) state.coins++; draftState = ""; }
                GUILayout.EndHorizontal();
                int idx = Array.IndexOf(NpcDemoScenario.Statuses, state.trainingStatus);
                var changed = GUILayout.SelectionGrid(idx, chinese ? new[] { "待接", "进行中", "可验收", "已领奖" } : NpcDemoScenario.Statuses, 2, tabs, GUILayout.MinHeight(52));
                if (changed >= 0 && changed != idx) SetStatus(changed);
                GUILayout.Label(T("Full input snapshot (JSON)", "完整输入状态（JSON）"), caption);
                if (draftState.Length == 0) draftState = JsonUtility.ToJson(state, true);
                draftState = GUILayout.TextArea(draftState, 12000, GUILayout.MinHeight(55));
                if (GUILayout.Button(T("Apply snapshot", "应用状态"))) ApplySnapshot();
                if (draftError.Length > 0) GUILayout.Label(draftError, caption);
                GUI.enabled = true;
            }
            debugOpen = GUILayout.Toggle(debugOpen, T("Inspect last turn", "查看上次请求"), GUI.skin.button);
            if (debugOpen)
            {
                GUILayout.Label(T("Input state", "输入状态"), caption);
                GUILayout.TextArea(sentState.Length == 0 ? JsonUtility.ToJson(state) : sentState);
                if (result != null)
                {
                    GUILayout.Label(T("Raw provider output", "模型原始输出"), caption);
                    GUILayout.TextArea(string.IsNullOrEmpty(result.RawJson) ? "—" : result.RawJson);
                    if (result.Decision != null)
                    {
                        GUILayout.Label(T("Parsed decision", "解析后的决策"), caption);
                        GUILayout.TextArea(JsonUtility.ToJson(result.Decision, true));
                    }
                }
            }
        }

        private void ApplySnapshot()
        {
            try
            {
                var candidate = JsonUtility.FromJson<NpcPlayerState>(draftState);
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.playerId) || candidate.coins < 0 ||
                    candidate.catalog == null || candidate.inventory == null || candidate.trainingTarget < 1 ||
                    candidate.trainingProgress < 0 || candidate.trainingProgress > candidate.trainingTarget ||
                    Array.IndexOf(NpcDemoScenario.Statuses, candidate.trainingStatus) < 0) throw new ArgumentException();
                state = candidate;
                draftError = "";
            }
            catch (ArgumentException)
            {
                draftError = T("Check the JSON, player ID, inventory, catalog and training values.",
                    "请检查 JSON、玩家 ID、背包、商品目录及训练状态。");
            }
        }
    }
}
