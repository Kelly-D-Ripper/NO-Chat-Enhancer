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

namespace NuclearOptionChatEnhancer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kellydripper.nuclearoption.chatenhancer";
        public const string PluginName = "NuclearOptionChatEnhancer";
        public const string PluginVersion = "1.0.1";
        private const int VanillaChatLimit = 128;

        private static readonly FieldInfo MessageTextField = AccessTools.Field(typeof(MessageUI), "messageText");
        private static readonly FieldInfo KillFeedTextField = AccessTools.Field(typeof(MessageUI), "killFeedText");
        private static readonly FieldInfo ChatField = AccessTools.Field(typeof(MessageUI), "chat");
        private static readonly FieldInfo ChatInputField = AccessTools.Field(typeof(ChatBox), "input");

        internal static Plugin Instance;
        internal static ConfigEntry<bool> EnableHistory;
        internal static ConfigEntry<int> HistoryEntries;
        internal static ConfigEntry<int> HistoryWidth;
        internal static ConfigEntry<int> HistoryHeight;
        internal static ConfigEntry<int> HistoryFontSize;
        internal static ConfigEntry<bool> ClearHistoryOnSceneChange;
        internal static ConfigEntry<bool> EnableLongPlayerMessages;
        internal static ConfigEntry<int> MaximumMessageLength;
        internal static ConfigEntry<bool> ExpandStatsBridgeMessages;
        internal static ConfigEntry<bool> ExpandKillFeedAnnouncements;

        private readonly List<string> _history = new List<string>();
        private Harmony _harmony;
        private TextMeshProUGUI _messageText;
        private TextMeshProUGUI _killFeedText;
        private ChatBox _chatBox;
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

            EnableLongPlayerMessages = Config.Bind("LongMessages", "EnablePlayerMessages", true,
                "Raise the 128-character player chat limit. The same setting and limit must be installed on the server and every client that sends or receives long player chat.");
            MaximumMessageLength = Config.Bind("LongMessages", "MaximumLength", 1024,
                "Maximum player chat and expanded stats-response length (128-4096).");
            ExpandStatsBridgeMessages = Config.Bind("LongMessages", "ExpandStatsBridgeResponses", true,
                "When NuclearOptionStatsBridge is installed on the server, stop splitting its responses every 128 characters.");
            ExpandKillFeedAnnouncements = Config.Bind("LongMessages", "ExpandKillFeedAnnouncements", true,
                "When SarcasticKillFeed is installed on the server, stop shortening its kill announcements to 128 characters.");
        }

        private void OnDestroy()
        {
            SuppressOriginalFeeds(false);
            if (_harmony != null) _harmony.UnpatchSelf();
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        internal void Attach(MessageUI messageUi)
        {
            SuppressOriginalFeeds(false);
            _messageText = MessageTextField == null ? null : MessageTextField.GetValue(messageUi) as TextMeshProUGUI;
            _killFeedText = KillFeedTextField == null ? null : KillFeedTextField.GetValue(messageUi) as TextMeshProUGUI;
            _chatBox = ChatField == null ? null : ChatField.GetValue(messageUi) as ChatBox;
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
            ApplyInputLimit(chatBox);
        }

        private static void ApplyInputLimit(ChatBox chatBox)
        {
            if (chatBox == null || ChatInputField == null) return;
            TMP_InputField input = ChatInputField.GetValue(chatBox) as TMP_InputField;
            if (input != null) input.characterLimit = EnableLongPlayerMessages.Value ? EffectiveMessageLimit : VanillaChatLimit;
        }

        internal void AddHistory(string message)
        {
            if (!EnableHistory.Value || string.IsNullOrEmpty(message)) return;

            bool wasAtBottom = _lastMaximumScroll <= 1f || _scrollPosition.y >= _lastMaximumScroll - 20f;
            string[] lines = message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string line in lines) _history.Add(line);

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

            SuppressOriginalFeeds(chatOpen && EnableHistory.Value);
            _chatWasOpen = chatOpen;
        }

        private bool IsChatOpen()
        {
            return _chatBox != null && _chatBox.gameObject != null && _chatBox.gameObject.activeInHierarchy;
        }

        private void SuppressOriginalFeeds(bool suppress)
        {
            SetTextSuppressed(_messageText, suppress, ref _messageTextSuppressed, ref _messageTextWasEnabled);
            SetTextSuppressed(_killFeedText, suppress, ref _killFeedTextSuppressed, ref _killFeedTextWasEnabled);
        }

        private static void SetTextSuppressed(TextMeshProUGUI text, bool suppress, ref bool isSuppressed, ref bool wasEnabled)
        {
            if (text == null) { isSuppressed = false; return; }
            if (suppress && !isSuppressed)
            {
                wasEnabled = text.enabled;
                text.enabled = false;
                isSuppressed = true;
            }
            else if (!suppress && isSuppressed)
            {
                text.enabled = wasEnabled;
                isSuppressed = false;
            }
        }

        private float CurrentScale()
        {
            return Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.75f);
        }

        private float CurrentHistoryHeight()
        {
            float configured = Mathf.Clamp(HistoryHeight.Value, 200, 900) * CurrentScale();
            return Mathf.Min(configured, Screen.height * 0.52f);
        }

        private void OnGUI()
        {
            if (!EnableHistory.Value || !IsChatOpen()) return;

            float scale = CurrentScale();
            EnsureStyles(scale);
            Rect panel = GetPanelRect(scale);
            float headerHeight = 30f * scale;
            Rect header = new Rect(panel.x + 10f * scale, panel.y + 3f * scale, panel.width - 20f * scale, headerHeight);
            Rect viewport = new Rect(panel.x + 7f * scale, panel.y + headerHeight, panel.width - 14f * scale, panel.height - headerHeight - 7f * scale);

            Color oldColor = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.985f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(header, "MESSAGE HISTORY  (mouse wheel / PgUp / PgDn / Home / End)", _headerStyle);

            float contentWidth = Mathf.Max(100f, viewport.width - 24f * scale);
            float spacing = 4f * scale;
            float[] heights = new float[_history.Count];
            float contentHeight = 2f * scale;
            for (int i = 0; i < _history.Count; i++)
            {
                heights[i] = Mathf.Max(_messageStyle.lineHeight, _messageStyle.CalcHeight(new GUIContent(_history[i]), contentWidth));
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
            _scrollPosition = GUI.BeginScrollView(viewport, _scrollPosition, content, false, false);
            float y = 1f * scale;
            for (int i = 0; i < _history.Count; i++)
            {
                GUI.Label(new Rect(2f * scale, y, contentWidth - 4f * scale, heights[i]), _history[i], _messageStyle);
                y += heights[i] + spacing;
            }
            GUI.EndScrollView();
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
            float configuredWidth = Mathf.Clamp(HistoryWidth.Value, 400, 1600) * scale;
            float width = Mathf.Min(configuredWidth, Screen.width * 0.65f);
            float height = CurrentHistoryHeight();
            float x = 18f * scale;
            float bottom = Screen.height - 110f * scale;

            RectTransform rectTransform = _messageText == null ? null : _messageText.rectTransform;
            if (rectTransform != null)
            {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
                Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                float minX = first.x;
                float minY = first.y;
                for (int i = 1; i < corners.Length; i++)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                    minX = Mathf.Min(minX, point.x);
                    minY = Mathf.Min(minY, point.y);
                }
                x = minX;
                bottom = Screen.height - minY + 4f * scale;
            }

            width = Mathf.Min(width, Screen.width - 20f * scale);
            height = Mathf.Min(height, Screen.height - 30f * scale);
            x = Mathf.Clamp(x, 10f * scale, Screen.width - width - 10f * scale);
            float y = Mathf.Clamp(bottom - height, 10f * scale, Screen.height - height - 10f * scale);
            return new Rect(x, y, width, height);
        }

        private void TryPatchStatsBridge()
        {
            if (!ExpandStatsBridgeMessages.Value) return;
            Type bridgeType = AccessTools.TypeByName("NuclearOptionStatsBridge.Plugin");
            MethodInfo broadcast = bridgeType == null ? null : AccessTools.Method(bridgeType, "Broadcast", new[] { typeof(string) });
            if (broadcast == null)
            {
                Logger.LogInfo("NuclearOptionStatsBridge was not found; its optional response expansion patch was skipped.");
                return;
            }

            MethodInfo transpiler = AccessTools.Method(typeof(Plugin), nameof(ReplaceVanillaLimit));
            _harmony.Patch(broadcast, transpiler: new HarmonyMethod(transpiler));
            Logger.LogInfo("NuclearOptionStatsBridge responses expanded to " + EffectiveMessageLimit + " characters per message.");
        }

        private void TryPatchSarcasticKillFeed()
        {
            if (!ExpandKillFeedAnnouncements.Value) return;
            Type killFeedType = AccessTools.TypeByName("SarcasticKillFeed.Plugin");
            MethodInfo broadcast = killFeedType == null ? null : AccessTools.Method(killFeedType, "Broadcast", new[] { typeof(string) });
            if (broadcast == null)
            {
                Logger.LogInfo("SarcasticKillFeed was not found; its optional announcement expansion patch was skipped.");
                return;
            }

            MethodInfo transpiler = AccessTools.Method(typeof(Plugin), nameof(ReplaceVanillaLimit));
            _harmony.Patch(broadcast, transpiler: new HarmonyMethod(transpiler));
            Logger.LogInfo("SarcasticKillFeed announcements expanded to " + EffectiveMessageLimit + " characters per message.");
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
