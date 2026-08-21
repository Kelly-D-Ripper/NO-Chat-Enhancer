using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NuclearOption.Chat;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearOptionChatEnhancer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kellydripper.nuclearoption.chatenhancer";
        public const string PluginName = "NuclearOptionChatEnhancer";
        public const string PluginVersion = "1.1.5";
        private const int VanillaChatLimit = 128;

        private static readonly FieldInfo MessageTextField = AccessTools.Field(typeof(MessageUI), "messageText");
        private static readonly FieldInfo KillFeedTextField = AccessTools.Field(typeof(MessageUI), "killFeedText");
        private static readonly FieldInfo ChatField = AccessTools.Field(typeof(MessageUI), "chat");
        private static readonly FieldInfo ChatInputField = AccessTools.Field(typeof(ChatBox), "input");
        private static readonly FieldInfo ChatSendButtonField = AccessTools.Field(typeof(ChatBox), "sendButton");
        private static readonly FieldInfo ChatAlliesToggleField = AccessTools.Field(typeof(ChatBox), "alliesOnlyToggle");

        internal static Plugin Instance;
        internal static ConfigEntry<bool> EnableHistory;
        internal static ConfigEntry<int> HistoryEntries;
        internal static ConfigEntry<int> HistoryWidth;
        internal static ConfigEntry<int> HistoryHeight;
        internal static ConfigEntry<int> HistoryFontSize;
        internal static ConfigEntry<bool> ClearHistoryOnSceneChange;
        internal static ConfigEntry<int> CompactMaximumLines;
        internal static ConfigEntry<float> CompactVisibleSeconds;
        internal static ConfigEntry<float> CompactFadeSeconds;
        internal static ConfigEntry<bool> EnableLongPlayerMessages;
        internal static ConfigEntry<int> MaximumMessageLength;
        internal static ConfigEntry<bool> ExpandStatsBridgeMessages;
        internal static ConfigEntry<bool> ExpandKillFeedAnnouncements;

        private readonly List<HistoryEntry> _history = new List<HistoryEntry>();
        private Harmony _harmony;
        private TextMeshProUGUI _messageText;
        private TextMeshProUGUI _killFeedText;
        private ChatBox _chatBox;
        private TMP_InputField _chatInput;
        private bool _chatWasOpen;
        private bool _messageTextSuppressed;
        private bool _messageTextWasEnabled;
        private bool _killFeedTextSuppressed;
        private bool _killFeedTextWasEnabled;
        private Vector2 _scrollPosition;
        private float _lastMaximumScroll;
        private bool _scrollToBottom = true;
        private GUIStyle _headerStyle;
        private GUIStyle _messageStyle;
        private int _styleFontSize;

        private sealed class HistoryEntry
        {
            internal readonly string Text;
            internal readonly float AddedAt;

            internal HistoryEntry(string text, float addedAt)
            {
                Text = text;
                AddedAt = addedAt;
            }
        }

        internal static int EffectiveMessageLimit
        {
            get
            {
                if (MaximumMessageLength == null) return 1024;
                return Mathf.Clamp(MaximumMessageLength.Value, VanillaChatLimit, 4096);
            }
        }

        private void Awake()
        {
            Instance = this;
            BindConfiguration();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            TryPatchStatsBridge();
            TryPatchSarcasticKillFeed();

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. History: " + EnableHistory.Value
                + ", player message limit: " + (EnableLongPlayerMessages.Value ? EffectiveMessageLimit.ToString() : VanillaChatLimit.ToString()) + ".");
        }

        private void BindConfiguration()
        {
            EnableHistory = Config.Bind("History", "Enabled", true,
                "Keep a scrollable message history and show it while the in-game chat box is open.");
            HistoryEntries = Config.Bind("History", "MaximumEntries", 500,
                "Maximum number of game/chat messages retained locally (50-2000).");
            HistoryWidth = Config.Bind("History", "WindowWidth", 760,
                "Minimum history window width at 1080p (400-1600).");
            HistoryHeight = Config.Bind("History", "WindowHeight", 420,
                "History window height at 1080p (200-900).");
            HistoryFontSize = Config.Bind("History", "FontSize", 16,
                "History text size at 1080p (11-28).");
            ClearHistoryOnSceneChange = Config.Bind("History", "ClearOnSceneChange", true,
                "Clear retained messages when a new gameplay UI/mission is loaded.");

            CompactMaximumLines = Config.Bind("CompactFeed", "MaximumLines", 6,
                "Maximum number of wrapped lines shown while chat is closed (1-12).");
            CompactVisibleSeconds = Config.Bind("CompactFeed", "VisibleSeconds", 6f,
                "How long a new message remains fully visible before fading (1-30 seconds).");
            CompactFadeSeconds = Config.Bind("CompactFeed", "FadeSeconds", 1f,
                "How long a message takes to fade after its fully-visible period (0.1-5 seconds).");

            EnableLongPlayerMessages = Config.Bind("LongMessages", "EnablePlayerMessages", true,
                "Raise the 128-character player chat limit. The same setting and limit must be installed on the server and every client that sends or receives long player chat.");
            MaximumMessageLength = Config.Bind("LongMessages", "MaximumLength", 1024,
                "Maximum player chat and expanded stats-response length (128-4096).");
            ExpandStatsBridgeMessages = Config.Bind("LongMessages", "ExpandStatsBridgeResponses", true,
                "When Kelly's RIPPER Control Bridge is installed on the server, stop splitting its responses every 128 characters.");
            ExpandKillFeedAnnouncements = Config.Bind("LongMessages", "ExpandKillFeedAnnouncements", true,
                "When Kelly's KIA is installed on the server, stop shortening its kill announcements to 128 characters.");
        }

        private void OnDestroy()
        {
            SuppressOriginalText(false);
            if (_harmony != null) _harmony.UnpatchSelf();
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        internal void Attach(MessageUI messageUi)
        {
            SuppressOriginalText(false);
            _messageText = MessageTextField == null ? null : MessageTextField.GetValue(messageUi) as TextMeshProUGUI;
            _killFeedText = KillFeedTextField == null ? null : KillFeedTextField.GetValue(messageUi) as TextMeshProUGUI;
            _chatBox = ChatField == null ? null : ChatField.GetValue(messageUi) as ChatBox;
            _chatInput = GetChatInput(_chatBox);
            ApplyInputLimit(_chatBox);

            if (ClearHistoryOnSceneChange.Value)
            {
                _history.Clear();
                _scrollPosition = Vector2.zero;
                _lastMaximumScroll = 0f;
                _scrollToBottom = true;
            }
        }

        internal void Attach(ChatBox chatBox)
        {
            _chatBox = chatBox;
            _chatInput = GetChatInput(chatBox);
            ApplyInputLimit(chatBox);
        }

        private static TMP_InputField GetChatInput(ChatBox chatBox)
        {
            return chatBox == null || ChatInputField == null ? null : ChatInputField.GetValue(chatBox) as TMP_InputField;
        }

        private static void ApplyInputLimit(ChatBox chatBox)
        {
            TMP_InputField input = GetChatInput(chatBox);
            if (input != null) input.characterLimit = EnableLongPlayerMessages.Value ? EffectiveMessageLimit : VanillaChatLimit;
        }

        internal void AddHistory(string message)
        {
            if (!EnableHistory.Value || string.IsNullOrEmpty(message)) return;

            bool wasAtBottom = _lastMaximumScroll <= 1f || _scrollPosition.y >= _lastMaximumScroll - 20f;
            float addedAt = Time.unscaledTime;
            string[] lines = message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string line in lines) _history.Add(new HistoryEntry(line, addedAt));

            int maximum = Mathf.Clamp(HistoryEntries.Value, 50, 2000);
            int excess = _history.Count - maximum;
            if (excess > 0) _history.RemoveRange(0, excess);
            if (wasAtBottom) _scrollToBottom = true;
        }

        private void Update()
        {
            bool chatOpen = IsChatOpen();
            if (chatOpen && !_chatWasOpen) _scrollToBottom = true;

            if (chatOpen && EnableHistory.Value)
            {
                if (Input.GetKeyDown(KeyCode.PageUp)) _scrollPosition.y = Mathf.Max(0f, _scrollPosition.y - CurrentHistoryHeight() * 0.8f);
                if (Input.GetKeyDown(KeyCode.PageDown)) _scrollPosition.y = Mathf.Min(_lastMaximumScroll, _scrollPosition.y + CurrentHistoryHeight() * 0.8f);
                if (Input.GetKeyDown(KeyCode.Home)) _scrollPosition.y = 0f;
                if (Input.GetKeyDown(KeyCode.End)) _scrollToBottom = true;
            }

            SuppressOriginalText(EnableHistory.Value);
            _chatWasOpen = chatOpen;
        }

        private bool IsChatOpen()
        {
            return _chatBox != null && _chatBox.gameObject != null && _chatBox.gameObject.activeInHierarchy;
        }

        private void SuppressOriginalText(bool suppress)
        {
            if (_messageText == null)
            {
                _messageTextSuppressed = false;
            }
            else if (suppress && !_messageTextSuppressed)
            {
                _messageTextWasEnabled = _messageText.enabled;
                _messageText.enabled = false;
                _messageTextSuppressed = true;
            }
            else if (suppress)
            {
                _messageText.enabled = false;
            }
            else if (!suppress && _messageTextSuppressed)
            {
                _messageText.enabled = _messageTextWasEnabled;
                _messageTextSuppressed = false;
            }

            if (_killFeedText == null)
            {
                _killFeedTextSuppressed = false;
            }
            else if (suppress && !_killFeedTextSuppressed)
            {
                _killFeedTextWasEnabled = _killFeedText.enabled;
                _killFeedText.enabled = false;
                _killFeedTextSuppressed = true;
            }
            else if (suppress)
            {
                _killFeedText.enabled = false;
            }
            else if (_killFeedTextSuppressed)
            {
                _killFeedText.enabled = _killFeedTextWasEnabled;
                _killFeedTextSuppressed = false;
            }
        }

        internal void AfterMessageUiLateUpdate()
        {
            bool replaceFeed = EnableHistory != null && EnableHistory.Value;
            SuppressOriginalText(replaceFeed);
        }

        private float CurrentScale()
        {
            return Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.75f);
        }

        private float CurrentHistoryHeight()
        {
            return Mathf.Clamp(HistoryHeight.Value, 200, 900) * CurrentScale();
        }

        private void OnGUI()
        {
            if (!EnableHistory.Value) return;

            float scale = CurrentScale();
            EnsureStyles(scale);
            if (IsChatOpen())
                DrawHistoryPanel(scale);
            else
                DrawCompactFeed(scale);
        }

        private void DrawHistoryPanel(float scale)
        {
            Rect panel = GetPanelRect(scale);
            float headerHeight = 30f * scale;
            Rect header = new Rect(panel.x + 10f * scale, panel.y + 3f * scale, panel.width - 20f * scale, headerHeight);
            Rect viewport = new Rect(panel.x + 7f * scale, panel.y + headerHeight, panel.width - 14f * scale, panel.height - headerHeight - 7f * scale);

            Color oldColor = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(header, "MESSAGE HISTORY  (mouse wheel / PgUp / PgDn / Home / End)", _headerStyle);

            float contentWidth = Mathf.Max(100f, viewport.width - 24f * scale);
            float spacing = 4f * scale;
            float[] heights = new float[_history.Count];
            float contentHeight = 2f * scale;
            for (int i = 0; i < _history.Count; i++)
            {
                heights[i] = Mathf.Max(_messageStyle.lineHeight, _messageStyle.CalcHeight(new GUIContent(_history[i].Text), contentWidth));
                contentHeight += heights[i] + spacing;
            }
            contentHeight = Mathf.Max(contentHeight, viewport.height - 2f);
            _lastMaximumScroll = Mathf.Max(0f, contentHeight - viewport.height);
            if (_scrollToBottom)
            {
                _scrollPosition.y = _lastMaximumScroll;
                _scrollToBottom = false;
            }

            Rect content = new Rect(0f, 0f, contentWidth, contentHeight);
            _scrollPosition = GUI.BeginScrollView(viewport, _scrollPosition, content, false, true);
            float y = 1f * scale;
            for (int i = 0; i < _history.Count; i++)
            {
                GUI.Label(new Rect(2f * scale, y, contentWidth - 4f * scale, heights[i]), _history[i].Text, _messageStyle);
                y += heights[i] + spacing;
            }
            GUI.EndScrollView();
        }

        private void DrawCompactFeed(float scale)
        {
            if (_history.Count == 0) return;

            float visibleSeconds = Mathf.Clamp(CompactVisibleSeconds.Value, 1f, 30f);
            float fadeSeconds = Mathf.Clamp(CompactFadeSeconds.Value, 0.1f, 5f);
            float now = Time.unscaledTime;
            int newestActive = _history.Count - 1;
            while (newestActive >= 0 && now - _history[newestActive].AddedAt > visibleSeconds + fadeSeconds) newestActive--;
            if (newestActive < 0) return;

            Rect controlsRect;
            bool haveControls = TryGetChatControlsRect(scale, out controlsRect);
            float x = haveControls ? controlsRect.xMin : 10f * scale;
            float edgeMargin = haveControls ? 0f : 10f * scale;
            float width = ResolveWindowWidth(HistoryWidth.Value, scale,
                haveControls ? controlsRect.width : 0f, Screen.width, edgeMargin);
            x = Mathf.Clamp(x, edgeMargin, Screen.width - width - edgeMargin);
            float textWidth = width - 16f * scale;
            float lineHeight = Mathf.Max(_styleFontSize + 2f,
                _messageStyle.CalcHeight(new GUIContent("Ag"), textWidth));
            int maximumLines = Mathf.Clamp(CompactMaximumLines.Value, 1, 12);
            float spacing = 2f * scale;
            List<int> selected = new List<int>();
            List<float> heights = new List<float>();
            float usedHeight = 0f;
            int usedLines = 0;

            for (int i = newestActive; i >= 0; i--)
            {
                HistoryEntry entry = _history[i];
                if (now - entry.AddedAt > visibleSeconds + fadeSeconds) break;

                float height = Mathf.Max(lineHeight, _messageStyle.CalcHeight(new GUIContent(entry.Text), textWidth));
                int wrappedLines = Mathf.Max(1, Mathf.RoundToInt(height / lineHeight));
                int remainingLines = maximumLines - usedLines;
                if (remainingLines <= 0) break;
                wrappedLines = Mathf.Min(wrappedLines, remainingLines);
                height = Mathf.Min(height, lineHeight * wrappedLines);
                selected.Add(i);
                heights.Add(height);
                usedHeight += height + (selected.Count == 1 ? 0f : spacing);
                usedLines += wrappedLines;
                if (usedLines >= maximumLines) break;
            }

            if (selected.Count == 0) return;
            float padding = 7f * scale;
            float panelHeight = usedHeight + padding * 2f;
            float top = 18f * scale;
            Rect panel = new Rect(x, top, width, panelHeight);

            Color oldColor = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.78f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            float y = panel.y + padding;
            for (int position = selected.Count - 1; position >= 0; position--)
            {
                HistoryEntry entry = _history[selected[position]];
                float age = now - entry.AddedAt;
                float alpha = age <= visibleSeconds ? 1f : Mathf.Clamp01(1f - ((age - visibleSeconds) / fadeSeconds));
                GUI.color = new Color(1f, 1f, 1f, alpha);
                float height = heights[position];
                GUI.BeginGroup(new Rect(panel.x + padding, y, textWidth, height));
                GUI.Label(new Rect(0f, 0f, textWidth, height), entry.Text, _messageStyle);
                GUI.EndGroup();
                y += height + spacing;
            }
            GUI.color = oldColor;
        }

        private void EnsureStyles(float scale)
        {
            int fontSize = Mathf.RoundToInt(Mathf.Clamp(HistoryFontSize.Value, 11, 28) * scale);
            if (_messageStyle != null && _styleFontSize == fontSize) return;
            _styleFontSize = fontSize;
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(10, fontSize - 2),
                normal = { textColor = new Color(0.72f, 0.88f, 1f, 1f) }
            };
            _messageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = fontSize,
                richText = true,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        private Rect GetPanelRect(float scale)
        {
            float width;
            float height = CurrentHistoryHeight();
            float x = 18f * scale;
            float top = 90f * scale;

            Rect controlsRect;
            if (TryGetChatControlsRect(scale, out controlsRect))
            {
                x = controlsRect.xMin;
                top = controlsRect.yMax + 6f * scale;
            }
            else
            {
                controlsRect = default(Rect);
                Rect messageRect;
                if (_messageText != null && TryGetGuiRect(_messageText.rectTransform, out messageRect))
                {
                    if (messageRect.width > 100f) controlsRect.width = messageRect.width;
                    x = messageRect.xMin;
                }
            }

            float edgeMargin = controlsRect.width > 100f ? 0f : 10f * scale;
            width = ResolveWindowWidth(HistoryWidth.Value, scale, controlsRect.width, Screen.width, edgeMargin);
            x = Mathf.Clamp(x, edgeMargin, Screen.width - width - edgeMargin);
            top = Mathf.Clamp(top, 10f * scale, Screen.height - 210f * scale);
            height = Mathf.Min(height, Screen.height - top - 10f * scale);
            return new Rect(x, top, width, height);
        }

        internal static float ResolveWindowWidth(float configuredWidth, float scale, float nativeWidth,
            float screenWidth, float edgeMargin)
        {
            float preferredWidth = Mathf.Clamp(configuredWidth, 400f, 1600f) * scale;
            float usableWidth = Mathf.Max(1f, screenWidth - edgeMargin * 2f);
            return Mathf.Min(Mathf.Max(preferredWidth, nativeWidth), usableWidth);
        }

        private bool TryGetChatControlsRect(float scale, out Rect controlsRect)
        {
            controlsRect = default(Rect);
            if (_chatBox == null) return false;

            var rects = new List<Rect>();
            AddGuiRect(_chatInput == null ? null : _chatInput.GetComponent<RectTransform>(), rects);
            Button sendButton = ChatSendButtonField == null ? null : ChatSendButtonField.GetValue(_chatBox) as Button;
            Toggle alliesToggle = ChatAlliesToggleField == null ? null : ChatAlliesToggleField.GetValue(_chatBox) as Toggle;
            AddGuiRect(sendButton == null ? null : sendButton.GetComponent<RectTransform>(), rects);
            AddGuiRect(alliesToggle == null ? null : alliesToggle.GetComponent<RectTransform>(), rects);
            if (rects.Count == 0) return false;

            float minX = rects[0].xMin;
            float maxX = rects[0].xMax;
            float minY = rects[0].yMin;
            float maxY = rects[0].yMax;
            for (int i = 1; i < rects.Count; i++)
            {
                minX = Mathf.Min(minX, rects[i].xMin);
                maxX = Mathf.Max(maxX, rects[i].xMax);
                minY = Mathf.Min(minY, rects[i].yMin);
                maxY = Mathf.Max(maxY, rects[i].yMax);
            }

            float horizontalPadding = 12f * scale;
            float topPadding = 12f * scale;
            float bottomPadding = 26f * scale;
            minX = Mathf.Max(0f, minX - horizontalPadding);
            maxX = Mathf.Min(Screen.width, maxX + horizontalPadding);
            minY = Mathf.Max(0f, minY - topPadding);
            maxY = Mathf.Min(Screen.height, maxY + bottomPadding);
            controlsRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return controlsRect.width > 100f && controlsRect.height > 10f;
        }

        private static void AddGuiRect(RectTransform rectTransform, List<Rect> rects)
        {
            Rect rect;
            if (TryGetGuiRect(rectTransform, out rect)) rects.Add(rect);
        }

        private static bool TryGetGuiRect(RectTransform rectTransform, out Rect guiRect)
        {
            guiRect = default(Rect);
            if (rectTransform == null) return false;

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            if (maxX - minX <= 1f || maxY - minY <= 1f) return false;
            guiRect = new Rect(minX, Screen.height - maxY, maxX - minX, maxY - minY);
            return true;
        }

        private void TryPatchStatsBridge()
        {
            if (!ExpandStatsBridgeMessages.Value) return;
            Type bridgeType = AccessTools.TypeByName("NuclearOptionStatsBridge.Plugin");
            if (bridgeType == null)
            {
                Logger.LogInfo("Kelly's RIPPER Control Bridge was not found; its optional message expansion patch was skipped.");
                return;
            }

            MethodInfo splitMessage = AccessTools.Method(bridgeType, "SplitMessage", new[] { typeof(string), typeof(int) });
            if (splitMessage != null)
            {
                MethodInfo prefix = AccessTools.Method(typeof(Plugin), nameof(ExpandBridgeSplitLimit));
                _harmony.Patch(splitMessage, prefix: new HarmonyMethod(prefix));
                Logger.LogInfo("Kelly's RIPPER Control Bridge public, private, and Discord messages expanded to " + EffectiveMessageLimit + " characters per message.");
                return;
            }

            MethodInfo broadcast = AccessTools.Method(bridgeType, "Broadcast", new[] { typeof(string) });
            if (broadcast != null)
            {
                MethodInfo transpiler = AccessTools.Method(typeof(Plugin), nameof(ReplaceVanillaLimit));
                _harmony.Patch(broadcast, transpiler: new HarmonyMethod(transpiler));
                Logger.LogInfo("Legacy RIPPER broadcast messages expanded to " + EffectiveMessageLimit + " characters per message.");
                return;
            }

            Logger.LogWarning("Kelly's RIPPER Control Bridge was found, but no compatible message-splitting method was available.");
        }

        private static void ExpandBridgeSplitLimit(ref int __1)
        {
            __1 = EffectiveMessageLimit;
        }

        private void TryPatchSarcasticKillFeed()
        {
            if (!ExpandKillFeedAnnouncements.Value) return;
            Type killFeedType = AccessTools.TypeByName("SarcasticKillFeed.Plugin");
            MethodInfo broadcast = killFeedType == null ? null : AccessTools.Method(killFeedType, "Broadcast", new[] { typeof(string) });
            if (broadcast == null)
            {
                Logger.LogInfo("Kelly's KIA was not found; its optional announcement expansion patch was skipped.");
                return;
            }

            MethodInfo transpiler = AccessTools.Method(typeof(Plugin), nameof(ReplaceVanillaLimit));
            _harmony.Patch(broadcast, transpiler: new HarmonyMethod(transpiler));
            Logger.LogInfo("Kelly's KIA announcements expanded to " + EffectiveMessageLimit + " characters per message.");
        }

        private static IEnumerable<CodeInstruction> ReplaceVanillaLimit(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int && (int)instruction.operand == VanillaChatLimit)
                {
                    instruction.operand = EffectiveMessageLimit;
                }
                yield return instruction;
            }
        }

        [HarmonyPatch(typeof(MessageUI), "Awake")]
        private static class MessageUiAwakePatch
        {
            private static void Postfix(MessageUI __instance)
            {
                if (Instance != null && !Application.isBatchMode) Instance.Attach(__instance);
            }
        }

        [HarmonyPatch(typeof(MessageUI), nameof(MessageUI.GameMessage), new[] { typeof(string) })]
        private static class GameMessagePatch
        {
            private static void Prefix(string message)
            {
                if (Instance != null && !Application.isBatchMode) Instance.AddHistory(message);
            }
        }

        [HarmonyPatch(typeof(MessageUI), "KillFeed", new[] { typeof(string) })]
        private static class KillFeedPatch
        {
            private static void Prefix(string message)
            {
                if (Instance != null && !Application.isBatchMode) Instance.AddHistory(message);
            }
        }

        [HarmonyPatch(typeof(MessageUI), "LateUpdate")]
        private static class MessageUiLateUpdatePatch
        {
            private static void Postfix()
            {
                if (Instance != null && !Application.isBatchMode) Instance.AfterMessageUiLateUpdate();
            }
        }

        [HarmonyPatch(typeof(ChatBox), "Awake")]
        private static class ChatBoxAwakePatch
        {
            private static void Postfix(ChatBox __instance)
            {
                if (Instance != null && !Application.isBatchMode) Instance.Attach(__instance);
            }
        }

        [HarmonyPatch(typeof(ChatManager), "ValidateChatMessageSize")]
        private static class ValidateChatMessageSizePatch
        {
            private static bool Prefix(string message, ref bool __result)
            {
                if (!EnableLongPlayerMessages.Value) return true;
                __result = !string.IsNullOrEmpty(message) && message.Length <= EffectiveMessageLimit;
                return false;
            }
        }

        [HarmonyPatch]
        private static class PlayerMessageLimitPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                Type type = typeof(ChatManager);
                MethodInfo method;

                method = AccessTools.Method(type, "CmdSendChatMessage", new[] { typeof(string), typeof(bool), typeof(Mirage.INetworkPlayer) });
                if (method != null) yield return method;
                method = AccessTools.Method(type, "Skeleton_CmdSendChatMessage_-456754112");
                if (method != null) yield return method;
                method = AccessTools.Method(type, "UserCode_TargetReceiveMessage_1307761090");
                if (method != null) yield return method;
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (!EnableLongPlayerMessages.Value) return instructions;
                return ReplaceVanillaLimit(instructions);
            }
        }
    }
}
