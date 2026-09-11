using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

internal static class HarmonySmokeTest
{
    private static string _gameDirectory;
    private static string _pluginDirectory;

    private static int Main(string[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            Console.Error.WriteLine("usage: HarmonySmokeTest <game-directory> <plugin-directory> [ripper-bridge-dll]");
            return 2;
        }

        _gameDirectory = Path.GetFullPath(args[0]);
        _pluginDirectory = Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

        Assembly pluginAssembly = Assembly.LoadFrom(Path.Combine(_pluginDirectory, "NuclearOptionChatEnhancer.dll"));
        Type pluginType = pluginAssembly.GetType("NuclearOptionChatEnhancer.Plugin", true);
        var config = new ConfigFile(Path.Combine(_pluginDirectory, "HarmonySmokeTest.cfg"), false);
        SetConfig(pluginType, "EnableLongPlayerMessages", config.Bind("Smoke", "LongMessages", true, string.Empty));
        SetConfig(pluginType, "MaximumMessageLength", config.Bind("Smoke", "MaximumLength", 1024, string.Empty));

        var expected = new Dictionary<MethodBase, string>
        {
            { AccessTools.Method(AccessTools.TypeByName("MessageUI"), "Awake"), "history UI attachment" },
            { AccessTools.Method(AccessTools.TypeByName("MessageUI"), "GameMessage", new[] { typeof(string) }), "history capture" },
            { AccessTools.Method(AccessTools.TypeByName("MessageUI"), "KillFeed", new[] { typeof(string) }), "combat-feed capture" },
            { AccessTools.Method(AccessTools.TypeByName("MessageUI"), "LateUpdate"), "native-feed suppression" },
            { AccessTools.Method(AccessTools.TypeByName("ChatBox"), "Awake"), "input limit" },
            { AccessTools.Method(AccessTools.TypeByName("NuclearOption.Chat.ChatManager"), "ValidateChatMessageSize"), "message validation" },
            { AccessTools.Method(AccessTools.TypeByName("NuclearOption.Chat.ChatManager"), "CmdSendChatMessage"), "client serialization" },
            { AccessTools.Method(AccessTools.TypeByName("NuclearOption.Chat.ChatManager"), "Skeleton_CmdSendChatMessage_-456754112"), "server deserialization" },
            { AccessTools.Method(AccessTools.TypeByName("NuclearOption.Chat.ChatManager"), "UserCode_TargetReceiveMessage_1307761090"), "client sanitization" }
        };

        foreach (KeyValuePair<MethodBase, string> pair in expected)
        {
            if (pair.Key == null) throw new InvalidOperationException("Target missing for " + pair.Value + ".");
            Console.WriteLine("PASS target " + pair.Value + ": " + pair.Key.Name);
        }

        var contextHarmony = new Harmony("com.kellydripper.nuclearoption.chatenhancer.contexttest");
        try
        {
            foreach (string patchName in new[] { "NativeKillContextPatch", "NativePilotContextPatch", "NativeRepairContextPatch" })
            {
                Type patch = pluginType.GetNestedType(patchName, BindingFlags.NonPublic);
                if (patch == null) throw new MissingMemberException(patchName);
                if (contextHarmony.CreateClassProcessor(patch).Patch().Count != 1)
                    throw new InvalidOperationException("Native context target missing: " + patchName);
                MethodInfo finalizer = patch.GetMethod("Finalizer", BindingFlags.Static | BindingFlags.NonPublic);
                Type infoType = pluginAssembly.GetType("NuclearOptionChatEnhancer.CombatInfo", true);
                object saved = Activator.CreateInstance(infoType);
                FieldInfo kind = infoType.GetField("Kind", BindingFlags.Instance | BindingFlags.NonPublic);
                kind.SetValue(saved, Enum.Parse(kind.FieldType, "Rescue"));
                var exception = new InvalidOperationException("Simulated original failure");
                if (!ReferenceEquals(finalizer.Invoke(null, new[] { saved, exception }), exception))
                    throw new InvalidOperationException("Finalizer swallowed the original exception.");
                FieldInfo current = pluginType.GetField("_currentCombat", BindingFlags.Static | BindingFlags.NonPublic);
                if (kind.GetValue(current.GetValue(null)).ToString() != "Rescue")
                    throw new InvalidOperationException("Finalizer failed to restore nested context.");
                current.SetValue(null, Activator.CreateInstance(infoType));
                Console.WriteLine("PASS native metadata patch and exception-safe context: " + patchName);
            }
            if (AccessTools.Field(AccessTools.TypeByName("PersistentUnit"), "player") == null)
                throw new MissingFieldException("PersistentUnit", "player");
            Console.WriteLine("PASS persistent player ownership metadata");
            foreach (string name in new[] { "GameMessagePatch", "KillFeedPatch" })
            {
                string expectedCall = name == "GameMessagePatch" ? "AddHistory" : "AddCombatHistory";
                MethodInfo prefix = pluginType.GetNestedType(name, BindingFlags.NonPublic).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var calls = PatchProcessor.GetOriginalInstructions(prefix).Where(i => i.operand is MethodInfo).Select(i => ((MethodInfo)i.operand).Name).ToArray();
                if (!calls.Contains(expectedCall) || calls.Contains(expectedCall == "AddHistory" ? "AddCombatHistory" : "AddHistory"))
                    throw new InvalidOperationException("Incorrect channel routing: " + name);
            }
            Console.WriteLine("PASS native combat and chat/server capture paths remain separate");
        }
        finally { contextHarmony.UnpatchSelf(); }

        MethodInfo resolveWindowWidth = pluginType.GetMethod("ResolveWindowWidth", BindingFlags.Static | BindingFlags.NonPublic);
        if (resolveWindowWidth == null) throw new MissingMethodException(pluginType.FullName, "ResolveWindowWidth");
        AssertWidth(resolveWindowWidth, 760f, 1f, 146f, 1920f, 0f, 760f,
            "configured width wins over narrow native controls");
        AssertWidth(resolveWindowWidth, 760f, 0.75f, 1000f, 1280f, 10f, 1000f,
            "wider native controls are preserved");
        AssertWidth(resolveWindowWidth, 1600f, 1.75f, 100f, 1920f, 10f, 1900f,
            "window width is clamped to the visible screen");

        MethodInfo replaceLimit = pluginType.GetMethod("ReplaceVanillaLimit", BindingFlags.Static | BindingFlags.NonPublic);
        if (replaceLimit == null) throw new MissingMethodException(pluginType.FullName, "ReplaceVanillaLimit");
        string[] transpiledTargets =
        {
            "CmdSendChatMessage",
            "Skeleton_CmdSendChatMessage_-456754112",
            "UserCode_TargetReceiveMessage_1307761090"
        };
        Type chatManager = AccessTools.TypeByName("NuclearOption.Chat.ChatManager");
        foreach (string name in transpiledTargets)
        {
            MethodInfo target = AccessTools.Method(chatManager, name);
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target).ToList();
            int vanillaConstants = original.Count(IsVanillaLimit);
            if (vanillaConstants == 0) throw new InvalidOperationException("No 128-character constant found in " + name + ".");

            var rewritten = (IEnumerable<CodeInstruction>)replaceLimit.Invoke(null, new object[] { original });
            List<CodeInstruction> output = rewritten.ToList();
            if (output.Any(IsVanillaLimit)) throw new InvalidOperationException("128-character constant remained in " + name + ".");
            int extendedConstants = output.Count(i => IsIntegerConstant(i, 1024));
            if (extendedConstants < vanillaConstants) throw new InvalidOperationException("Not every limit was rewritten in " + name + ".");
            Console.WriteLine("PASS IL rewrite " + name + ": " + vanillaConstants + " limit constant(s) -> 1024");
        }

        if (args.Length == 3)
        {
            string bridgePath = Path.GetFullPath(args[2]);
            Assembly bridgeAssembly = Assembly.LoadFrom(bridgePath);
            Type bridgeType = bridgeAssembly.GetType("NuclearOptionStatsBridge.Plugin", true);
            MethodInfo splitMessage = bridgeType.GetMethod("SplitMessage", BindingFlags.Static | BindingFlags.NonPublic);
            if (splitMessage == null) throw new MissingMethodException(bridgeType.FullName, "SplitMessage");

            MethodInfo prefix = pluginType.GetMethod("ExpandBridgeSplitLimit", BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix == null) throw new MissingMethodException(pluginType.FullName, "ExpandBridgeSplitLimit");
            var harmony = new Harmony("com.kellydripper.nuclearoption.chatenhancer.smoketest");
            try
            {
                harmony.Patch(splitMessage, prefix: new HarmonyMethod(prefix));
                object iterator = splitMessage.Invoke(null, new object[] { new string('x', 300), 128 });
                int[] capturedLimits = iterator.GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => field.FieldType == typeof(int) && field.Name.IndexOf("maximum", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(field => (int)field.GetValue(iterator))
                    .ToArray();
                if (!capturedLimits.Contains(1024))
                    throw new InvalidOperationException("RIPPER splitter did not capture the expanded 1024-character limit.");
                Console.WriteLine("PASS RIPPER public/private message expansion: splitter captured 1024 characters");
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }

        return 0;
    }

    private static void AssertWidth(MethodInfo method, float configuredWidth, float scale, float nativeWidth,
        float screenWidth, float edgeMargin, float expected, string description)
    {
        float actual = (float)method.Invoke(null,
            new object[] { configuredWidth, scale, nativeWidth, screenWidth, edgeMargin });
        if (Math.Abs(actual - expected) > 0.01f)
            throw new InvalidOperationException(description + " returned " + actual + " instead of " + expected + ".");
        Console.WriteLine("PASS layout width: " + description + " -> " + actual);
    }

    private static bool IsVanillaLimit(CodeInstruction instruction)
    {
        return IsIntegerConstant(instruction, 128);
    }

    private static bool IsIntegerConstant(CodeInstruction instruction, int value)
    {
        return instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int && (int)instruction.operand == value;
    }

    private static void SetConfig(Type pluginType, string name, object value)
    {
        FieldInfo field = pluginType.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null) throw new MissingFieldException(pluginType.FullName, name);
        field.SetValue(null, value);
    }

    private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
    {
        string fileName = new AssemblyName(args.Name).Name + ".dll";
        string[] roots =
        {
            _pluginDirectory,
            Path.Combine(_gameDirectory, "BepInEx", "core"),
            Path.Combine(_gameDirectory, "NuclearOption_Data", "Managed")
        };
        foreach (string root in roots)
        {
            string candidate = Path.Combine(root, fileName);
            if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
        }
        return null;
    }
}
