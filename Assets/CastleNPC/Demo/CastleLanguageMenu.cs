using UnityEngine;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo
{
    // The desktop overlay and the world-space sheet use the same artwork, geometry and text.
    internal sealed class CastleLanguageMenu
    {
        private const float Width = 576f, Height = 392f;
        private static readonly Color Ink = new Color32(56, 42, 32, 255);
        private static readonly Color Bronze = new Color32(112, 82, 51, 255);
        private static readonly Color Gold = new Color32(179, 141, 77, 255);
        private static readonly Color Focus = new Color32(47, 62, 60, 255);
        private static readonly Color LitPaper = new Color32(249, 237, 208, 255);
        private static readonly Color RowPaper = new Color32(229, 211, 173, 255);

        private readonly Canvas canvas;
        private readonly RectTransform sheet;
        private readonly Font bodyFont, displayFont;
        private readonly Text heading, language, selectionHint, closeHint;
        private readonly Image[] rowEdges = new Image[2], rowFills = new Image[2];
        private readonly Text[] selected = new Text[2];
        private readonly Image[] seals = new Image[2];
        private bool inWorld, initialized;
        private int appliedFocus = -1;
        private bool appliedLanguage, appliedWorld;
        private int pressedRow = -1;
        internal bool IsAlive => canvas;

        internal CastleLanguageMenu(Transform owner, Font font)
        {
            bodyFont = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 46);
            var root = new GameObject("Castle language settings", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(owner, false);
            root.SetActive(false);
            canvas = root.GetComponent<Canvas>();
            sheet = new GameObject("Parchment", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
            sheet.SetParent(root.transform, false);
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f);
            sheet.anchoredPosition = Vector2.zero;
            sheet.sizeDelta = new Vector2(Width, Height);
            var parchment = sheet.GetComponent<RawImage>();
            parchment.texture = Resources.Load<Texture2D>("CastleLanguageParchment");
            parchment.raycastTarget = false;

            heading = Label("Title", "", 44, 19, 365, 68, 46, Ink);
            language = Label("Language", "", 45, 109, 425, 34, 21, Bronze);
            for (int i = 0; i < 2; i++)
            {
                float y = 152 + i * 73;
                rowEdges[i] = Block("Language row " + i, 44, y, 488, 61, Bronze);
                rowFills[i] = Block("Row surface", 46, y + 2, 484, 57, RowPaper);
                Label(i == 0 ? "English" : "Chinese", i == 0 ? "English" : "中文", 65, y + 1, 241, 59, 29, Ink);
                selected[i] = Label("Selected language", "", 304, y + 2, 162, 57, 18, Ink, TextAnchor.MiddleRight);
                seals[i] = Block("Selected lozenge", 484, y + 19, 23, 23, Gold);
                seals[i].rectTransform.Rotate(0, 0, 45);
                var inset = SealPart(seals[i].rectTransform, "Ink inlay", Vector2.zero, new Vector2(20, 20), 0, Focus);
                // Counter the lozenge's tilt. The check is drawn with strokes to work with every font.
                SealPart(inset, "Check short stroke", new Vector2(-5, 1), new Vector2(9, 3), -90, LitPaper);
                SealPart(inset, "Check long stroke", new Vector2(2, -2), new Vector2(15, 3), 0, LitPaper);
            }
            selectionHint = Label("Language controls", "", 45, 331, 227, 39, 18, Bronze);
            closeHint = Label("Close menu hint", "", 273, 331, 258, 39, 18, Ink, TextAnchor.MiddleRight);
        }

        private static RectTransform SealPart(RectTransform parent, string name, Vector2 point, Vector2 size, float angle, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = img.rectTransform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = point;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0, 0, angle);
            img.color = color;
            img.raycastTarget = false;
            return rect;
        }

        private RectTransform Add(Graphic graphic, float x, float y, float w, float h)
        {
            var rect = graphic.rectTransform;
            rect.SetParent(sheet, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x + w / 2f, -y - h / 2f);
            rect.sizeDelta = new Vector2(w, h);
            graphic.raycastTarget = false;
            return rect;
        }

        private Image Block(string name, float x, float y, float w, float h, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Add(img, x, y, w, h);
            img.color = color;
            return img;
        }

        private Text Label(string name, string value, float x, float y, float w, float h,
            int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            Add(text, x, y, w, h);
            text.font = bodyFont;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        internal void Show(Camera viewer, bool world, bool chinese, int focus)
        {
            if (!initialized || inWorld != world || (world && canvas.worldCamera != viewer))
            {
                initialized = true;
                inWorld = world;
                canvas.renderMode = world ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = world ? viewer : null;
                canvas.sortingOrder = 10;
                if (world)
                {
                    var rect = (RectTransform)canvas.transform;
                    rect.sizeDelta = new Vector2(Width, Height);
                    rect.SetParent(viewer.transform, false);
                    rect.localScale = Vector3.one * .00140f;
                    rect.localRotation = Quaternion.identity;
                    rect.localPosition = new Vector3(0f, -.04f, 1.12f);
                }
                else canvas.transform.SetParent(null, false);
            }
            if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
            float fit = Mathf.Min((Screen.width - 24f) / Width, (Screen.height - 24f) / Height);
            float scale = world ? 1f : Mathf.Min(Mathf.Clamp(Screen.width / 1060f, .82f, 1.75f), fit);
            sheet.localScale = Vector3.one * Mathf.Max(.1f, scale);
            if (appliedFocus == focus && appliedLanguage == chinese && appliedWorld == world) return;
            appliedFocus = focus;
            appliedLanguage = chinese;
            appliedWorld = world;
            heading.text = chinese ? "设置" : "Settings";
            heading.font = !chinese && displayFont ? displayFont : bodyFont;
            language.text = chinese ? "语言" : "Language";
            selectionHint.text = world ? (chinese ? "↑↓ 选择   ·   A 确认" : "↑↓ Choose   ·   A Select") : "";
            closeHint.text = world
                ? (chinese ? "再按 B 关闭" : "Press B again to close")
                : (chinese ? "再按 Esc 关闭" : "Press Esc again to close");
            for (int i = 0; i < 2; i++)
            {
                bool active = i == (chinese ? 1 : 0);
                rowEdges[i].color = i == focus ? Focus : Bronze;
                rowFills[i].color = i == focus ? LitPaper : RowPaper;
                selected[i].text = active ? (chinese ? "已选" : "Selected") : "";
                seals[i].gameObject.SetActive(active);
            }
        }

        // Game View also delivers mouse events through IMGUI. Reuse the displayed row rectangles
        // for hit testing so this works without a second EventSystem or XR ray interactor.
        internal int PointerRow(Vector2 pixel)
        {
            for (int i = 0; i < rowEdges.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(rowEdges[i].rectTransform, pixel, null)) return i;
            return -1;
        }

        internal int ClickRow(bool pressed, bool released, int hover)
        {
            if (pressed) pressedRow = hover;
            if (released)
            {
                int clicked = pressedRow == hover ? hover : -1;
                pressedRow = -1;
                return clicked;
            }
            return -1;
        }

        internal void Hide()
        {
            pressedRow = -1;
            if (canvas && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
        }

        internal void Destroy()
        {
            if (displayFont) Object.Destroy(displayFont);
            if (canvas) Object.Destroy(canvas.gameObject);
        }
    }
}
