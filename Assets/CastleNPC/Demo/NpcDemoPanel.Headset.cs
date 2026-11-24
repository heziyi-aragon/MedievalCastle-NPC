using System;
using System.Collections.Generic;
using MedievalCastle.NPC;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private enum Choice { Prompt1, Prompt2, Prompt3, NextPage, Retry, Cancel, Accept, Reject, Pass, Coins, Training, Language, Mode, Role }
        private readonly List<Choice> choices = new List<Choice>(16);
        private Canvas headsetCanvas;
        private Text title, message, choiceText, hint;
        private Camera headsetCamera;
        private bool primaryHeld, backHeld, stickHeld, handConnected, headsetOpen = true;
        private int headsetChoice, replyPage;
        private int lastTarget = -2;
        private float nextDraw;

        private void UpdateHeadset()
        {
            if (!XRSettings.isDeviceActive)
            {
                if (headsetCanvas != null && headsetCanvas.gameObject.activeSelf) headsetCanvas.gameObject.SetActive(false);
                handConnected = false;
                return;
            }
            if (headsetCamera == null) headsetCamera = Camera.main;
            if (headsetCamera == null) return;
            var hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            hand.TryGetFeatureValue(CommonUsages.primaryButton, out bool a);
            hand.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger);
            hand.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue);
            hand.TryGetFeatureValue(CommonUsages.secondaryButton, out bool b);
            hand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
            HandleHeadsetFrame(headsetCamera, hand.isValid, a || trigger, b, triggerValue, stick.y);
        }

        internal void HandleHeadsetFrame(Camera camera, bool connected, bool activateButton, bool backButton, float triggerValue, float stickY)
        {
            headsetCamera = camera;
            if (headsetCanvas == null) BuildHeadsetPanel();
            // A digital trigger and its analog value describe the same physical press.
            bool select = activateButton || triggerValue > (primaryHeld ? .35f : .7f);
            bool pressed = connected && handConnected && select && !primaryHeld;
            bool toggle = connected && handConnected && backButton && !backHeld;
            handConnected = connected;
            primaryHeld = select;
            if (toggle)
            {
                headsetOpen = !headsetOpen;
                if (!headsetOpen && !Pending) EndSelection();
                pressed = false;
            }
            backHeld = backButton;
            var head = headsetCamera.transform;
            CastleNpcCharacter actor = null;
            if (Physics.Raycast(head.position, head.forward, out var hit, 18f, ~0, QueryTriggerInteraction.Collide))
                actor = hit.collider.GetComponentInParent<CastleNpcCharacter>();
            int target = actor == null ? -1 : Array.IndexOf(NpcDemoScenario.Roles, actor.RoleId);
            bool aimingAtOther = target >= 0 && target != SelectedRole;
            bool picked = pressed && !Pending && target >= 0 && (aimingAtOther || !headsetOpen);
            if (picked)
            {
                SelectRole(target, head);
                headsetOpen = true;
                aimingAtOther = false;
                pressed = false;
            }
            if (headsetCanvas.gameObject.activeSelf != headsetOpen) headsetCanvas.gameObject.SetActive(headsetOpen);
            if (!headsetOpen) return;

            MakeChoices();
            headsetChoice = Mathf.Clamp(headsetChoice, 0, choices.Count - 1);
            bool navigate = Mathf.Abs(stickY) > .65f && !stickHeld;
            if (navigate)
            {
                headsetChoice = (headsetChoice + (stickY > 0 ? -1 : 1) + choices.Count) % choices.Count;
                stickHeld = true;
            }
            if (Mathf.Abs(stickY) < .3f) stickHeld = false;
            var choice = choices[headsetChoice];
            if (pressed && roleSelected && !(Pending && aimingAtOther)) Activate(choice);
            if (Time.unscaledTime < nextDraw && target == lastTarget && !pressed && !navigate && !toggle && !picked) return;
            lastTarget = target;
            nextDraw = Time.unscaledTime + .08f;
            title.text = RoleName(role) + "   /   " + ModeName;
            if (!roleSelected && target < 0)
            {
                choiceText.text = T("Select a character", "请选择角色");
                hint.text = T("Look at a character · Trigger / A to select", "注视角色 · 扳机 / A 选择角色");
            }
            else if (aimingAtOther && !Pending)
            {
                choiceText.text = T("Talk to ", "对话 · ") + RoleName(target);
                hint.text = T("Look at a character · Trigger / A to select", "注视角色 · 扳机 / A 选择角色");
            }
            else
            {
                choiceText.text = "› " + ChoiceLabel(choice);
                hint.text = T("Stick ↑↓ choose    Trigger / A confirm    B hide", "摇杆 ↑↓ 切换    扳机 / A 确认    B 收起");
            }
            message.text = ResponsePage();
        }

        private void MakeChoices()
        {
            choices.Clear();
            if (Pending) { choices.Add(Choice.Cancel); return; }
            choices.Add(Choice.Mode);
            choices.Add(Choice.Prompt1);
            choices.Add(Choice.Prompt2);
            choices.Add(Choice.Prompt3);
            if (result != null && result.Reply.Length > PageLength) choices.Add(Choice.NextPage);
            if (Failed) choices.Add(Choice.Retry);
            if (CanReport) { choices.Add(Choice.Accept); choices.Add(Choice.Reject); }
            choices.Add(Choice.Pass);
            choices.Add(Choice.Coins);
            choices.Add(Choice.Training);
            choices.Add(Choice.Language);
            choices.Add(Choice.Role);
        }

        private int PageLength => chinese ? 55 : 132;
        private string ResponsePage()
        {
            if (Pending) return T("Waiting for ", "正在等待") + RoleName(sentRole) + "…";
            if (Failed) return error;
            if (result == null) return T("Look at a character and press Trigger / A to talk.", "注视角色后按扳机或 A 键交谈。");
            int pageLength = PageLength;
            int pages = Mathf.Max(1, (result.Reply.Length + pageLength - 1) / pageLength);
            replyPage = Mathf.Clamp(replyPage, 0, pages - 1);
            int start = replyPage * pageLength;
            var bodyText = result.Reply.Substring(start, Mathf.Min(pageLength, result.Reply.Length - start));
            var provider = result.IsOffline ? T("Offline", "离线") : T("Live", "在线");
            var prefix = RoleName(sentRole) + " · " + provider + (pages > 1 ? $" {replyPage+1}/{pages}" : "") + " · ";
            return prefix + bodyText + (outcome.Length == 0 ? "" : "  [" + outcome + "]");
        }

        private string ChoiceLabel(Choice choice)
        {
            switch (choice)
            {
                case Choice.Prompt1: case Choice.Prompt2: case Choice.Prompt3: return Prompt((int)choice);
                case Choice.NextPage: return T("Read next page", "阅读下一页");
                case Choice.Retry: return T("Retry last request", "重试上次请求");
                case Choice.Cancel: return T("Cancel request", "取消请求");
                case Choice.Accept: return T("Report accepted · ", "回传已通过 · ") + result?.Decision?.action;
                case Choice.Reject: return T("Report rejected · ", "回传已拒绝 · ") + result?.Decision?.action;
                case Choice.Pass: return T("Toggle pass · ", "切换通行证 · ") + (state.hasPass ? T("Yes", "有") : T("No", "无"));
                case Choice.Coins: return T("Cycle coins · ", "切换金币 · ") + state.coins;
                case Choice.Training: return T("Cycle training · ", "切换训练状态 · ") + state.trainingStatus;
                case Choice.Language: return chinese ? "Switch to English" : "切换为中文";
                case Choice.Mode: return service.Mode == NpcProviderMode.DeepSeek
                    ? T("Switch to offline demo", "切换到离线演示") : T("Switch to live DeepSeek", "切换到 DeepSeek 在线模式");
                default: return T("Next character", "下一个角色");
            }
        }

        private void Activate(Choice choice)
        {
            switch (choice)
            {
                case Choice.Prompt1: case Choice.Prompt2: case Choice.Prompt3: Submit(Prompt((int)choice)); break;
                case Choice.NextPage: replyPage = (replyPage+1) % Mathf.Max(1, (result.Reply.Length + PageLength - 1) / PageLength); break;
                case Choice.Retry: SendSaved(); break;
                case Choice.Cancel: cancellation?.Cancel(); break;
                case Choice.Accept: Report(true); break;
                case Choice.Reject: Report(false); break;
                case Choice.Pass: state.hasPass = !state.hasPass; draftState = ""; break;
                case Choice.Coins: state.coins = state.coins == 0 ? 2 : state.coins == 2 ? 10 : 0; draftState = ""; break;
                case Choice.Training: SetStatus((Array.IndexOf(NpcDemoScenario.Statuses, state.trainingStatus)+1) % NpcDemoScenario.Statuses.Length); break;
                case Choice.Language: chinese = !chinese; break;
                case Choice.Mode: ToggleMode(); break;
                case Choice.Role: SelectRole((role+1) % NpcDemoScenario.Roles.Length, headsetCamera.transform); break;
            }
        }

        private void BuildHeadsetPanel()
        {
            headsetChoice = !hasApiKey && service.Mode == NpcProviderMode.DeepSeek ? 0 : 1;
            var panel = new GameObject("NPC headset dialogue", typeof(RectTransform), typeof(Canvas), typeof(Image));
            panel.transform.SetParent(headsetCamera.transform, false);
            panel.transform.localPosition = new Vector3(0, -.48f, 1.45f);
            panel.transform.localScale = Vector3.one * .00155f;
            var rect = (RectTransform)panel.transform;
            rect.sizeDelta = new Vector2(760, 248);
            panel.GetComponent<Image>().color = new Color(.035f, .055f, .075f, .95f);
            headsetCanvas = panel.GetComponent<Canvas>();
            headsetCanvas.renderMode = RenderMode.WorldSpace;
            headsetCanvas.worldCamera = headsetCamera;
            title = AddText(rect, "Role and mode", 10, 40, 27, new Color(.97f, .84f, .54f));
            message = AddText(rect, "Reply", 51, 84, 24, Color.white);
            choiceText = AddText(rect, "Selection", 140, 56, 25, new Color(.69f, .92f, 1f));
            hint = AddText(rect, "Controls", 203, 36, 21, new Color(.84f, .89f, .92f));
        }

        private Text AddText(RectTransform parent, string label, float top, float height, int size, Color color)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(-40, height);
            rect.anchoredPosition = new Vector2(0, -top);
            var text = obj.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            return text;
        }
    }
}
