using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MedievalCastle.Mechanics
{
    internal sealed class CastleHud
    {
        private static readonly Color Ink = new Color32(24, 29, 28, 248);
        private static readonly Color Rim = new Color32(86, 84, 66, 255);
        private static readonly Color Gold = new Color32(241, 201, 115, 255);
        private static readonly Color Paper = new Color32(243, 237, 218, 255);
        private static readonly Color Muted = new Color32(178, 184, 170, 255);

        private readonly Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private readonly Text wallet;
        private readonly Text bag;
        private readonly Text objective;
        private readonly Text direction;
        private readonly Text prompt;
        private readonly Text title;
        private readonly Button firstSuggestion;
        private readonly Button secondSuggestion;
        private readonly Text transcript;
        private readonly GameObject dialogue;
        private readonly InputField input;
        private readonly Button sendButton;
        private readonly ScrollRect scroll;
        private readonly List<string> messages = new List<string>();

        public CastleHud(Transform root, Action<string> send, Action close)
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("Castle Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(root, false);
            }

            var canvasObject = new GameObject("Castle UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1;
            Transform layer = canvas.transform;

            Transform status = Panel(layer, "Inventory", new Vector2(0, 1), new Vector2(145, -65), new Vector2(258, 98), Ink);
            Label(status, "INVENTORY", 13, Gold, new Vector2(0, 1), new Vector2(125, -20), new Vector2(224, 21));
            wallet = Label(status, "", 20, Paper, new Vector2(0, 1), new Vector2(125, -47), new Vector2(224, 27));
            bag = Label(status, "", 14, Muted, new Vector2(0, 1), new Vector2(125, -75), new Vector2(224, 28));

            Transform quest = Panel(layer, "Objective", new Vector2(1, 1), new Vector2(-169, -78), new Vector2(306, 124), Ink);
            Label(quest, "OBJECTIVE", 13, Gold, new Vector2(0, 1), new Vector2(150, -20), new Vector2(266, 21));
            objective = Label(quest, "", 17, Paper, new Vector2(0, 1), new Vector2(150, -58), new Vector2(266, 53));
            direction = Label(quest, "", 14, Gold, new Vector2(0, 1), new Vector2(150, -99), new Vector2(266, 25));

            Transform footer = Panel(layer, "Controls", new Vector2(.5f, 0), new Vector2(0, 30), new Vector2(425, 28), Ink);
            Label(footer, "WASD Move   ·   Click to look   ·   E Talk   ·   Esc Release", 14, Paper,
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(405, 27), TextAnchor.MiddleCenter);
            prompt = Label(layer, "", 19, Gold, new Vector2(.5f, 0), new Vector2(0, 78), new Vector2(370, 34),
                TextAnchor.MiddleCenter);
            var promptShadow = prompt.gameObject.AddComponent<Shadow>();
            promptShadow.effectDistance = new Vector2(2, -2);

            Transform modal = Panel(layer, "Dialogue", new Vector2(.5f, .5f), new Vector2(0, -36), new Vector2(630, 383), Ink);
            dialogue = modal.gameObject;
            title = Label(modal, "", 30, Gold, new Vector2(0, 1), new Vector2(255, -37), new Vector2(454, 45));
            title.fontStyle = FontStyle.Bold;
            MakeButton(modal, "Close", new Vector2(1, 1), new Vector2(-54, -36), new Vector2(80, 35), () => close());
            Panel(modal, "Divider", new Vector2(.5f, 1), new Vector2(0, -66), new Vector2(576, 1), Rim);

            Transform view = Panel(modal, "History", new Vector2(.5f, 1), new Vector2(0, -164), new Vector2(576, 180), Ink);
            view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            transcript = Label(view, "", 18, Paper, new Vector2(0, 1), new Vector2(282, -6), new Vector2(564, 180), TextAnchor.UpperLeft);
            transcript.rectTransform.pivot = new Vector2(.5f, 1);
            transcript.verticalOverflow = VerticalWrapMode.Overflow;
            scroll.content = transcript.rectTransform;

            Transform entry = Panel(modal, "Message", new Vector2(0, 0), new Vector2(248, 49), new Vector2(441, 45), new Color32(46, 51, 45, 255));
            input = entry.gameObject.AddComponent<InputField>();
            input.targetGraphic = entry.GetComponent<Image>();
            input.characterLimit = 250;
            input.lineType = InputField.LineType.SingleLine;
            input.textComponent = Label(entry, "", 18, Paper, new Vector2(.5f, .5f), Vector2.zero, new Vector2(413, 36));
            input.placeholder = Label(entry, "Type a message…", 17, Muted, new Vector2(.5f, .5f), Vector2.zero, new Vector2(413, 36));

            firstSuggestion = MakeButton(modal, "", new Vector2(.5f, 0), new Vector2(-145, 97), new Vector2(276, 32), null);
            secondSuggestion = MakeButton(modal, "", new Vector2(.5f, 0), new Vector2(145, 97), new Vector2(276, 32), null);
            firstSuggestion.gameObject.name = "First dialogue suggestion";
            secondSuggestion.gameObject.name = "Second dialogue suggestion";
            sendButton = MakeButton(modal, "Send", new Vector2(1, 0), new Vector2(-83, 49), new Vector2(111, 45), () => send(input.text), true);
            input.onEndEdit.AddListener(message =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) send(message);
            });
            Close();
        }

        public void SetInteraction(string value) => prompt.text = value;

        public void SetDirection(string value)
        {
            if (direction.text != value) direction.text = value;
        }

        public void Refresh(PlayerSnapshot state)
        {
            wallet.text = $"{state.gold} gold   ·   {state.reputation} rep";
            bag.text = state.inventory.Length == 0 ? "Backpack empty" :
                string.Join("   ·   ", state.inventory.Select(item => $"{CastleGame.ItemName(item.itemId)} ×{item.quantity}"));

            if (state.questStage == nameof(QuestStage.Rewarded))
                objective.text = "Training complete. Reward collected.";
            else if (state.questStage == nameof(QuestStage.Complete))
                objective.text = "Return to the knight for your reward.";
            else if (state.questStage == nameof(QuestStage.InProgress))
                objective.text = $"Training stations   {state.trainingProgress}/{state.trainingTotal}\nVisit station {state.trainingProgress + 1}.";
            else if (state.gateOpen)
                objective.text = "Find the knight at the training yard.";
            else if (state.inventory.Any(item => item.itemId == CastleGame.PassId))
                objective.text = "Show your Castle Pass to the guard.";
            else
                objective.text = "Ask the merchant for a Castle Pass.";
        }

        public void Open(NpcActor actor, PlayerSnapshot state, IReadOnlyList<NpcTurn> history)
        {
            messages.Clear();
            transcript.text = "";
            title.text = actor.displayName;
            input.text = "";
            prompt.text = "";
            dialogue.SetActive(true);
            foreach (NpcTurn turn in history)
            {
                Append("You", turn.message);
                ShowAnswer(actor.displayName, turn);
            }
            SetSuggestions(actor.role, state);
            SetWaiting(false);
        }

        public void SetSuggestions(NpcRole role, PlayerSnapshot state)
        {
            switch (role)
            {
                case NpcRole.Guard:
                    SetSuggestion(firstSuggestion, "Check my pass");
                    SetSuggestion(secondSuggestion, "Where is the merchant?");
                    break;
                case NpcRole.Merchant:
                    SetSuggestion(firstSuggestion, "What do you sell?");
                    SetSuggestion(secondSuggestion, state.inventory.Any(item => item.itemId == CastleGame.PassId)
                        ? "Buy a potion" : "Buy a pass");
                    break;
                default:
                    string action = state.questStage == nameof(QuestStage.Available) ? "Accept training" :
                        state.questStage == nameof(QuestStage.Complete) ? "Claim my reward" : "What should I do next?";
                    SetSuggestion(firstSuggestion, action);
                    SetSuggestion(secondSuggestion, "How does training work?");
                    break;
            }
        }

        private void SetSuggestion(Button button, string message)
        {
            button.GetComponentInChildren<Text>().text = message;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                input.text = message;
                input.ActivateInputField();
                input.MoveTextEnd(false);
            });
        }

        public void ShowAnswer(string actor, NpcTurn turn)
        {
            if (!string.IsNullOrWhiteSpace(turn.reply)) Append(actor, turn.reply);
            if (!string.IsNullOrWhiteSpace(turn.result)) Append("Game", turn.result);
        }

        public void Close() => dialogue.SetActive(false);

        public void OnSent(string message)
        {
            input.text = "";
            Append("You", message);
            SetWaiting(true);
        }

        public void SetWaiting(bool waiting)
        {
            input.interactable = !waiting;
            sendButton.interactable = !waiting;
            firstSuggestion.interactable = !waiting;
            secondSuggestion.interactable = !waiting;
            sendButton.GetComponentInChildren<Text>().text = waiting ? "Waiting…" : "Send";
            if (!waiting) input.ActivateInputField();
        }

        public void Append(string speaker, string message)
        {
            messages.Add(speaker + "\n" + message);
            if (messages.Count > 24) messages.RemoveAt(0);
            transcript.text = string.Join("\n\n", messages);
            transcript.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(180, transcript.preferredHeight + 12));
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0;
        }

        private Text Label(Transform parent, string value, int size, Color color, Vector2 anchor, Vector2 position,
            Vector2 bounds, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect(parent, "Label", anchor, position, bounds);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.text = value;
            return label;
        }

        private Button MakeButton(Transform parent, string value, Vector2 anchor, Vector2 position, Vector2 bounds,
            UnityAction click, bool primary = false)
        {
            Transform panel = Panel(parent, value, anchor, position, bounds, primary ? Gold : Rim);
            Button button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            if (click != null) button.onClick.AddListener(click);
            Label(panel, value, 16, primary ? Ink : Paper, new Vector2(.5f, .5f), Vector2.zero, bounds, TextAnchor.MiddleCenter);
            return button;
        }

        private static Transform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 bounds, Color color)
        {
            RectTransform rect = Rect(parent, name, anchor, position, bounds);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 bounds)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = bounds;
            return rect;
        }
    }
}
