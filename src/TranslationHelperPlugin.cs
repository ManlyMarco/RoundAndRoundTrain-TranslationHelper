// By ManlyMarco
// GPLv3 license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using XUnity.AutoTranslator.Plugin.Core;

namespace RoundTrain_TranslationHelper
{
    [BepInDependency("gravydevsupreme.xunity.autotranslator")]
    [BepInPlugin(GUID, "RoundAndRoundTrain Translation Helper", Version)]
    public class TranslationHelperPlugin : BaseUnityPlugin
    {
        public const string Version = "1.0";
        public const string GUID = "RoundAndRoundTrain.TranslationHelper";

        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(TranslationHelperPlugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ScenarioManager), "LoadCSV", typeof(TextAsset))]
        private static void TranslationHook(ref List<string> __result, TextAsset file)
        {
            //Console.WriteLine(file.ToString());
            for (int i = 0; i < __result.Count; i++)
            {
                var orig = __result[i];
                if (!orig.StartsWith("#"))
                {
                    if (AutoTranslator.Default.TryTranslate(orig, out var tl))
                        __result[i] = tl;
                }
                else if (orig.StartsWith("#speaker="))
                {
                    var name = orig.Substring(9);
                    if (AutoTranslator.Default.TryTranslate(name, out var tl))
                        __result[i] = "#speaker=" + tl;
                }
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(ScenarioManager), "MsgRead", MethodType.Enumerator)]
        private static IEnumerable<CodeInstruction> FixNewlinesTpl(IEnumerable<CodeInstruction> instructions)
        {
            // completely disable splitting lines, by default space means newline
            return new CodeMatcher(instructions)
                   .MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)' '))
                   .SetOperandAndAdvance((sbyte)';')
                   .Instructions();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(ScenarioManager), "BacklogLoad")]
        private static IEnumerable<CodeInstruction> FixNewlinesBacklog(IEnumerable<CodeInstruction> instructions)
        {
            // completely disable splitting lines, by default space means newline
            return new CodeMatcher(instructions)
                   .MatchForward(false, new CodeMatch(OpCodes.Ldstr, " "))
                   .SetOperandAndAdvance(";;;")
                   .Instructions();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(ConfigManager), "TestLoad", MethodType.Enumerator)]
        private static IEnumerable<CodeInstruction> FixPreviewTpl(IEnumerable<CodeInstruction> instructions)
        {
            // Preview text asset is loaded separately and needs special handling
            return new CodeMatcher(instructions)
                   .MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(TextAsset), nameof(TextAsset.text))))
                   .Set(OpCodes.Call, AccessTools.Method(typeof(TranslationHelperPlugin), nameof(TranslationHelperPlugin.PreviewTextReplace)))
                   .Instructions();
        }

        private static string PreviewTextReplace(TextAsset instance)
        {
            return string.Join("\n",
                               instance.text
                                       .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                                       .Select(x => AutoTranslator.Default.TryTranslate(x, out var t) ? t : x));
        }
    }
    
    [BepInPlugin("RoundAndRoundTrain.Cheats", "RoundAndRoundTrain Cheats", "1.0")]
    public class CheatsPlugin : BaseUnityPlugin
    {
        private ConfigEntry<KeyboardShortcut> _resetBtn;
        private ConfigEntry<KeyboardShortcut> _devmodeBtn;

        private void Awake()
        {
            _resetBtn = Config.Bind("Cheats", "Reset time", new KeyboardShortcut(KeyCode.F11));
            _devmodeBtn = Config.Bind("Cheats", "Enable DevMode menu", new KeyboardShortcut(KeyCode.F10));
        }

        private void Update()
        {
            if (_resetBtn.Value.IsDown())
            {
                DataStore.Instance.PlayData.playPrm["ti"] = 0;
                var tv = Traverse.CreateWithType("UIManager").Property("Instance");
                tv.Method("DebugPanelUpdate").GetValue();
                tv.Method("TimerUpdate").GetValue();
            }
            else if (_devmodeBtn.Value.IsDown())
            {
                DataStore.Instance.SystemData.devMode = !DataStore.Instance.SystemData.devMode;
                Traverse.CreateWithType("UIManager").Property("Instance").Method("DebugLoad").GetValue();
            }
        }
    }
}
