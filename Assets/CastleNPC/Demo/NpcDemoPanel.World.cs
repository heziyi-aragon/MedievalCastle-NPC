using MedievalCastle.NPC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private const float CardWidth = 520f, CardScale = .00158f, RowHeight = 48f;
        private readonly RectTransform[] rows = new RectTransform[5];
        private readonly Text[] rowLabels = new Text[5];
        private Canvas questions, speech;
        private Text questionTitle, status, controls, replyLabel, pageLabel, placeholder, sendCaption;
        private RectTransform composer;
        private InputField messageField;
        private Camera boardCamera, framedCamera;
        private int placedRole = -1, obstructedRole = -1;
        private Vector3 obstructedAt;
        private bool layoutValid, drawnChinese, drawnXr, drawnPending, drawnFailed;
        private int drawnChoice, drawnPage;
        private string drawnLine;
        private Quaternion priorDesktopRotation;
        private float priorDesktopFov;
        private static readonly Color32 Ink = new Color32(42, 34, 26, 255);
        private static readonly Color32 Parchment = new Color32(247, 232, 197, 255);
        private static readonly Color32 Slate = new Color32(37, 54, 56, 255);
        private static readonly Color32 LightRow = new Color32(246, 222, 171, 255);
        private static readonly Color32 RowInk = new Color32(53, 71, 70, 255);

        private static RectTransform RectPart(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Box(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Image Paint(RectTransform rect, Color color, bool hit = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = hit;
            return image;
        }

        private Text Label(Transform parent, string id, int size, Color color, float x, float y, float width, float h)
        {
            var rect = RectPart(id, parent);
            Box(rect, x, y, width, h);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.color = color;
            return label;
        }

        private Canvas WorldCard(string id, Color color)
        {
            var rect = RectPart(id, transform);
            rect.sizeDelta = new Vector2(CardWidth, 200);
            rect.localScale = Vector3.one * CardScale;
            var canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            Paint(rect, color);
            return canvas;
        }

        private static Button Clickable(RectTransform rect, Image back)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private void BuildWorldCards()
        {
            questions = WorldCard("NPC questions", Slate);
            questions.gameObject.AddComponent<GraphicRaycaster>();
            var root = questions.transform;
            var rule = RectPart("Gold edge", root); Box(rule, 0, 0, 5, 45); Paint(rule, AccentColor);
            questionTitle = Label(root, "Role", 29, Parchment, 18, 1, 480, 42);
            status = Label(root, "Status", 23, Color.white, 18, 44, 484, 40);
            controls = Label(root, "Controls", 18, Parchment, 18, 0, 484, 30);
            for (int i = 0; i < rows.Length; i++)
            {
                var rect = rows[i] = RectPart("Option " + (i + 1), root);
                var back = Paint(rect, RowInk, true);
                rowLabels[i] = Label(rect, "Label", 28, Color.white, 12, 0, CardWidth - 56, RowHeight - 3);
                rowLabels[i].resizeTextForBestFit = true;
                rowLabels[i].resizeTextMinSize = 23;
                rowLabels[i].resizeTextMaxSize = 28;
                int index = i;
                Clickable(rect, back).onClick.AddListener(() =>
                {
                    if (!MenuOpen && InConversation && index < choiceCount)
                    {
                        headsetChoice = index;
                        ActivateChoice(hudChoices[index]);
                        RefreshWorldCards(boardCamera, false);
                    }
                });
            }
            composer = RectPart("Free question", root);
            var field = RectPart("Input", composer);
            Box(field, 0, 0, 377, 44);
            var fieldBack = Paint(field, new Color32(250, 243, 222, 255), true);
            var entered = Label(field, "Text", 24, Ink, 9, 1, 358, 42);
            placeholder = Label(field, "Placeholder", 22, new Color32(94, 90, 74, 255), 9, 1, 358, 42);
            messageField = field.gameObject.AddComponent<InputField>();
            messageField.targetGraphic = fieldBack;
            messageField.textComponent = entered;
            messageField.placeholder = placeholder;
            messageField.characterLimit = 1000;
            messageField.lineType = InputField.LineType.SingleLine;
            messageField.onEndEdit.AddListener(_ =>
            {
                var keys = Keyboard.current;
                if (keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
                    && string.IsNullOrEmpty(Input.compositionString)) SendEnteredQuestion();
            });
            var send = RectPart("Send", composer); Box(send, 384, 0, 100, 44);
            var sendBack = Paint(send, LightRow, true);
            sendCaption = Label(send, "Label", 24, Ink, 0, 0, 100, 44);
            sendCaption.alignment = TextAnchor.MiddleCenter;
            Clickable(send, sendBack).onClick.AddListener(SendEnteredQuestion);

            speech = WorldCard("NPC speech", Parchment);
            ((RectTransform)speech.transform).pivot = new Vector2(.5f, 0);
            replyLabel = Label(speech.transform, "Reply", 29, Ink, 18, 6, CardWidth - 36, 60);
            pageLabel = Label(speech.transform, "Page", 18, Ink, 18, 0, CardWidth - 36, 24);
            pageLabel.alignment = TextAnchor.MiddleRight;
            var tail = RectPart("Speech tail", speech.transform);
            tail.anchorMin = tail.anchorMax = new Vector2(.5f, 0);
            tail.sizeDelta = Vector2.one * 12;
            tail.localRotation = Quaternion.Euler(0, 0, 45);
            Paint(tail, Parchment);
        }

        private void SendEnteredQuestion()
        {
            if (MenuOpen || !InConversation || Pending || Failed || string.IsNullOrWhiteSpace(messageField.text)) return;
            string question = messageField.text;
            messageField.SetTextWithoutNotify("");
            Submit(question);
        }

        private void EnsureDesktopEvents()
        {
            if (EventSystem.current) return;
            var events = new GameObject("Castle dialogue input");
            events.transform.SetParent(transform, false);
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }

        private void RefreshWorldCards(Camera eye, bool xr)
        {
            if (!InConversation || MenuOpen || !eye)
            {
                if (questions) questions.gameObject.SetActive(false);
                if (farewellActive && !MenuOpen && eye) ShowFarewellCaption(eye);
                else if (speech) speech.gameObject.SetActive(false);
                return;
            }
            if (!questions) BuildWorldCards();
            bool switchedDisplay = layoutValid && drawnXr != xr;
            bool place = placedRole != role || boardCamera != eye;
            if (switchedDisplay && xr && framedCamera)
            {
                // Link now owns the camera pose. Only release the desktop lens.
                framedCamera.fieldOfView = priorDesktopFov;
                framedCamera = null;
            }
            boardCamera = eye;
            questions.worldCamera = speech.worldCamera = eye;
            questions.gameObject.SetActive(true);
            if (!place && layoutValid && drawnChinese == chinese && drawnXr == xr &&
                drawnPending == Pending && drawnFailed == Failed && drawnChoice == headsetChoice &&
                drawnPage == replyPage && drawnLine == SpokenLine)
            {
                speech.gameObject.SetActive(!string.IsNullOrEmpty(SpokenLine));
                FacePlayer(eye);
                return;
            }
            SetHudChoices();
            headsetChoice = Mathf.Clamp(headsetChoice, 0, choiceCount - 1);
            questionTitle.text = RoleName(role);
            status.text = Pending ? T("Waiting for a reply…", "正在等待回复…") :
                (Failed ? T("Unable to get a reply.", "暂时无法取得回复。") : "");
            bool hasStatus = status.text.Length != 0;
            status.gameObject.SetActive(hasStatus);
            float y = hasStatus ? 87 : 47;
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].gameObject.SetActive(i < choiceCount);
                if (i >= choiceCount) continue;
                Box(rows[i], 14, y, CardWidth - 28, RowHeight - 3);
                bool selected = xr && headsetChoice == i;
                rows[i].GetComponent<Image>().color = selected ? LightRow : RowInk;
                rowLabels[i].color = selected ? Ink : Color.white;
                rowLabels[i].text = ChoiceLabel(hudChoices[i]);
                y += RowHeight;
            }
            bool compose = !xr && !Pending && !Failed;
            composer.gameObject.SetActive(compose);
            if (compose)
            {
                Box(composer, 18, y + 5, CardWidth - 36, 44);
                placeholder.text = T("Your question…", "输入你的问题…");
                sendCaption.text = T("Send", "发送");
                y += 54;
            }
            controls.gameObject.SetActive(xr);
            if (xr)
            {
                controls.text = T("↑↓ Choose    ·    A Confirm", "↑↓ 选择    ·    A 确认");
                Box(controls.rectTransform, 18, y, CardWidth - 36, 29);
                y += 30;
            }
            ((RectTransform)questions.transform).sizeDelta = new Vector2(CardWidth, y + 9);
            DrawSpeech(SpokenLine, HasPages);

            layoutValid = true;
            drawnChinese = chinese;
            drawnXr = xr;
            drawnPending = Pending;
            drawnFailed = Failed;
            drawnChoice = headsetChoice;
            drawnPage = replyPage;
            drawnLine = SpokenLine;
            if (place || switchedDisplay)
            {
                placedRole = role;

                if (!xr)
                {
                    EnsureDesktopEvents();
                    FrameDesktop(eye);
                }
            }
            FacePlayer(eye);
        }

        private void ShowFarewellCaption(Camera eye)
        {
            if (!speech || !eye) return;
            speech.worldCamera = eye;
            if (!speech.gameObject.activeSelf || replyLabel.text != SpokenLine) DrawSpeech(SpokenLine, false);
            speech.transform.rotation = Facing(speech.transform.position, eye);
        }

        private void DrawSpeech(string spoken, bool pages)
        {
            if (pages)
            {
                int pageCount = (spoken.Length + PageLength - 1) / PageLength;
                replyPage = Mathf.Clamp(replyPage, 0, pageCount - 1);
                int start = replyPage * PageLength;
                spoken = spoken.Substring(start, Mathf.Min(PageLength, spoken.Length - start));
                pageLabel.text = (replyPage + 1) + " / " + pageCount;
            }
            pageLabel.gameObject.SetActive(pages);
            speech.gameObject.SetActive(!string.IsNullOrEmpty(spoken));
            if (!speech.gameObject.activeSelf) return;
            replyLabel.text = spoken;
            float captionWidth = Mathf.Clamp(Mathf.Ceil(replyLabel.preferredWidth) + 40f, 144f, CardWidth);
            Box(replyLabel.rectTransform, 18, 6, captionWidth - 36, 60);
            float bodyHeight = Mathf.Max(43, Mathf.Ceil(replyLabel.preferredHeight) + 5);
            Box(replyLabel.rectTransform, 18, 6, captionWidth - 36, bodyHeight);
            if (pages) Box(pageLabel.rectTransform, 18, bodyHeight + 6, captionWidth - 36, 24);
            ((RectTransform)speech.transform).sizeDelta = new Vector2(captionWidth, bodyHeight + 14 + (pages ? 26 : 0));
        }

        private void FacePlayer(Camera eye)
        {
            var candidate = Facing(questions.transform.position, eye);
            // With physical sidesteps the board may turn towards a nearby wall. Keep the last
            // clear orientation until the whole card can turn without entering the wall.
            if (Quaternion.Angle(questions.transform.rotation, candidate) > .3f)
            {
                var half = ChoiceExtents(questions.transform.lossyScale.x);
                if (!Physics.CheckBox(questions.transform.position, half, candidate, ~0, QueryTriggerInteraction.Ignore))
                    questions.transform.rotation = candidate;
            }
            speech.transform.rotation = Facing(speech.transform.position, eye);
        }

        private static Quaternion Facing(Vector3 position, Camera eye)
        {
            var direction = position - eye.transform.position;
            return direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction, Vector3.up) : eye.transform.rotation;
        }

        // Includes the maximum pending/ready, desktop/XR and five-row pagination layouts.
        private static Vector3 ChoiceExtents(float scale) =>
            new Vector3((CardWidth / 2 + 8) * scale, 182 * scale, .035f);

        private bool PlaceWorldCards(CastleNpcCharacter actor, Camera eye)
        {
            if (!questions) BuildWorldCards();
            var head = System.Array.Find(actor.GetComponentsInChildren<Transform>(), bone => bone.name == "Head");
            var bounds = new Bounds(head.position, Vector3.zero);
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);
            var crown = head.position;
            crown.y = bounds.max.y + .085f;

            var face = Vector3.ProjectOnPlane(eye.transform.position - head.position, Vector3.up).normalized;
            if (face.sqrMagnitude < .01f) face = actor.transform.forward;
            var center = bounds.center;
            center.y = Mathf.Clamp(eye.transform.position.y - .18f, actor.transform.position.y + .90f,
                actor.transform.position.y + 1.66f);
            // Keep letters comfortably legible when a player reaches the horse long before
            // reaching the rider. Close human conversations retain a compact physical panel.
            float captionScale = Mathf.Clamp(Vector3.Distance(crown, eye.transform.position) * .00105f, CardScale, .0032f);
            var speechHalf = new Vector3((CardWidth * .5f + 4) * captionScale, 106 * captionScale, .022f);
            bool captionFits = false;
            Vector3 captionAnchor = crown;
            foreach (float forward in new[] { 0f, .13f, .25f })
            {
                var anchor = crown + face * forward;
                var rotation = Facing(anchor, eye);
                if (!ClearCard(eye.transform.position, anchor + rotation * Vector3.up * speechHalf.y, speechHalf, rotation)) continue;
                captionAnchor = anchor;
                captionFits = true;
                break;
            }
            if (!captionFits) return CannotPlace(eye);
            var scales = new[] { 1f, .82f };
            var angles = new[] { -62f, 62f, -82f, 82f, -45f, 45f, -100f, 100f, -120f, 120f };
            foreach (float multiplier in scales)
            foreach (float height in new[] { 0f, -.28f, .24f })
            foreach (float extra in new[] { 0f, .24f })
            foreach (float angle in angles)
            {
                var outward = Quaternion.AngleAxis(angle, Vector3.up) * face;
                float radius = Mathf.Abs(outward.x) * bounds.extents.x + Mathf.Abs(outward.z) * bounds.extents.z;
                if (actor.RoleId == "guard")
                {
                    // Shield, cloak and greeting hand rotate about the planted feet. Reserve their
                    // swept radius so later turns cannot enter an otherwise clear panel.
                    radius = Mathf.Max(radius, Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z),
                        new Vector2(actor.transform.position.x, actor.transform.position.z)) +
                        new Vector2(bounds.extents.x, bounds.extents.z).magnitude);
                }
                var edge = center + outward * (radius + extra + .08f) + Vector3.up * height;
                float distance = Vector3.Distance(edge + outward * .5f, eye.transform.position);
                float scale = multiplier * Mathf.Clamp(distance * .00122f, CardScale, .0035f);
                var half = ChoiceExtents(scale);
                var point = edge + outward * half.x;
                var facing = Facing(point, eye);
                if (Vector3.Distance(point, eye.transform.position) < .56f ||
                    !ClearCard(eye.transform.position, point, half, facing)) continue;
                speech.transform.localScale = Vector3.one * captionScale;
                speech.transform.SetPositionAndRotation(captionAnchor, Facing(captionAnchor, eye));
                questions.transform.localScale = Vector3.one * scale;
                questions.transform.SetPositionAndRotation(point, facing);
                obstructedRole = -1;
                return true;
            }
            return CannotPlace(eye);
        }

        private bool CannotPlace(Camera eye)
        {
            // Leave the player free to step out of a recess that cannot fit a legible card.
            obstructedRole = nearRole;
            obstructedAt = eye.transform.position;
            questions.gameObject.SetActive(false);
            speech.gameObject.SetActive(false);
            return false;
        }

        private static bool ClearCard(Vector3 eye, Vector3 point, Vector3 half, Quaternion rotation)
        {
            if (Physics.CheckBox(point, half, rotation, ~0, QueryTriggerInteraction.Ignore)) return false;
            for (int corner = -1; corner <= 3; corner++)
            {
                var offset = corner < 0 ? Vector3.zero : new Vector3(
                    (corner % 2 == 0 ? -1 : 1) * half.x, (corner < 2 ? -1 : 1) * half.y, 0);
                if (Physics.Linecast(eye, point + rotation * offset, ~0, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }

        private void FrameDesktop(Camera eye)
        {
            // E works beside or behind an NPC, even when looking away. Compose the two fixed
            // boards for mouse use without moving the feet. Restore the exploration view on exit.
            framedCamera = eye;
            priorDesktopRotation = eye.transform.localRotation;
            priorDesktopFov = eye.fieldOfView;
            // Expand in each actual billboard plane. Side panels can be strongly oblique to
            // the camera, so a screen-aligned approximation can lose their closest corner.
            var corners = new Vector3[8];
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (CardWidth / 2f + 4f);
                corners[i] = questions.transform.TransformPoint(new Vector3(x, i < 2 ? -184f : 184f, 0));
                corners[4 + i] = speech.transform.TransformPoint(new Vector3(x, i < 2 ? -9f : 213f, 0));
            }
            Vector3 center = Vector3.zero;
            foreach (var corner in corners) center += (corner - eye.transform.position).normalized;
            if (center.sqrMagnitude < .001f) return;
            eye.transform.rotation = Quaternion.LookRotation(center, Vector3.up);
            // Center the angular envelope, not the midpoint of boards at different distances.
            for (int pass = 0; pass < 3; pass++)
            {
                var low = new Vector2(180, 180);
                var high = -low;
                foreach (var corner in corners)
                {
                    var p = eye.transform.InverseTransformPoint(corner);
                    var angles = new Vector2(Mathf.Atan2(p.x, p.z), Mathf.Atan2(p.y, p.z)) * Mathf.Rad2Deg;
                    low = Vector2.Min(low, angles);
                    high = Vector2.Max(high, angles);
                }
                var middle = (low + high) * (.5f * Mathf.Deg2Rad);
                var aim = eye.transform.TransformDirection(new Vector3(Mathf.Tan(middle.x), Mathf.Tan(middle.y), 1f));
                eye.transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
            }
            float tangent = 0;
            foreach (var corner in corners)
            {
                var p = eye.transform.InverseTransformPoint(corner);
                tangent = Mathf.Max(tangent, Mathf.Max(Mathf.Abs(p.y), Mathf.Abs(p.x) / eye.aspect) / Mathf.Max(.035f, p.z));
            }
            // 5% margin on each side for the largest reply/page and option layout.
            eye.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(tangent / .90f) * Mathf.Rad2Deg, 30f, 155f);
        }
        private void ReleaseWorldCards()
        {
            placedRole = -1;
            layoutValid = false;
            if (framedCamera)
            {
                framedCamera.transform.localRotation = priorDesktopRotation;
                framedCamera.fieldOfView = priorDesktopFov;
                framedCamera = null;
            }
            if (questions) questions.gameObject.SetActive(false);
            if (speech) speech.gameObject.SetActive(false);
            if (messageField) messageField.SetTextWithoutNotify("");
        }
    }
}
