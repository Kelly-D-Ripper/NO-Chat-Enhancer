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
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: HarmonySmokeTest <game-directory> <plugin-directory>");
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

        return 0;
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
