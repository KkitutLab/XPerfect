using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace XPerfect {
    public static class SafePatch {
        public static MethodBase GetMethodSafe(string typeName, string methodName, Type[] args = null, bool allowStatic = true) {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => {
                    try { return a.GetTypes(); } catch { return []; }
                })
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName)
                ?? throw new($"[{nameof(SafePatch)}] Type not found: {typeName}");

            BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            if (allowStatic) bf |= BindingFlags.Static;

            if (methodName == ".ctor") {
                return args is not null
                    ? type.GetConstructor(bf, null, args, null)
                        ?? throw new($"[{nameof(SafePatch)}] Constructor with specified args not found in {typeName}")
                    : type.GetConstructors(bf).FirstOrDefault()
                        ?? throw new($"[{nameof(SafePatch)}] No constructors found in {typeName}");
            }

            if (methodName == ".cctor") {
                return type.TypeInitializer
                    ?? throw new($"[{nameof(SafePatch)}] Static constructor not found in {typeName}");
            }

            if (args is not null) {
                return type.GetMethod(methodName, bf, null, args, null)
                    ?? throw new($"[{nameof(SafePatch)}] Method {methodName} with specified args not found in {typeName}");
            }

            var method = type.GetMethods(bf).FirstOrDefault(m => m.Name == methodName)
                ?? throw new($"[{nameof(SafePatch)}] Method {methodName} not found in {typeName}");
            return method;
        }
    }

    public static class SafePatchManager {
        public static bool ApplyAll(HarmonyLib.Harmony harmony) {
            try {
                Apply(harmony, "HitMarginPatch", "scrMisc", "SelectHitMarginByTimeBoundary", null,
                    postfix: typeof(HitMarginPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                    priority: Priority.High);

                Apply(harmony, "MistakesManagerAddHitPatch", "scrMarginTracker", "AddHit", null,
                    postfix: typeof(MistakesManagerAddHitPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                    priority: Priority.Normal);

                Apply(harmony, "LevelStartPatch", "scrController", "Start_Rewind", null,
                    postfix: typeof(LevelStartPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

                Apply(harmony, "CheckpointRestorePatch", "scrMarginTracker", "RevertToLastCheckpoint", null,
                    postfix: typeof(CheckpointRestorePatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

                Apply(harmony, "XPerfectOnlySwitchChosenPatch", "scrPlanet", "SwitchChosen", null,
                    postfix: typeof(XPerfectOnlySwitchChosenPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

                Apply(harmony, "UpdateLayoutPatch", "scrHitErrorMeter", "UpdateLayout", null,
                    postfix: typeof(MeterVisualPatch).GetMethod("UpdateLayoutPostfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

                Apply(harmony, "HitMarginPerfectTickSetterPatch", "Persistence", "set_hitMarginPerfectTick", null,
                    postfix: typeof(MeterVisualPatch).GetMethod("HitMarginPerfectTickSetterPostfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

                Apply(harmony, "DetailedResultsPatch", "DetailedResults", "GenerateResults", null,
                    prefix: typeof(DetailedResultsPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                    postfix: typeof(DetailedResultsPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

                try {
                    Apply(harmony, "EditorSwitchToEditModePatch", "scnEditor", "SwitchToEditMode", null,
                        postfix: typeof(EditorSwitchToEditModePatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
                } catch { }

                try {
                    Apply(harmony, "IsCountedPatch", "HitMarginHelper", "IsCounted", null,
                        postfix: typeof(XPerfectOnlyIsCountedPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
                } catch {
                    try {
                        Apply(harmony, "IsCountedPatch", "scrMistakesManager", "IsCounted", null,
                            postfix: typeof(XPerfectOnlyIsCountedPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
                    } catch { }
                }

                ModLogger.Log("All safe patches applied successfully.");
                return true;
            } catch (Exception ex) {
                ModLogger.Error($"CRITICAL: SafePatch failed! Aborting mod load.\n{ex}");
                try { harmony.UnpatchSelf(); } catch { }
                return false;
            }
        }

        private static void Apply(HarmonyLib.Harmony harmony, string name, string typeName, string methodName, Type[] args,
            MethodInfo postfix = null, MethodInfo prefix = null, int priority = -1) {
            var target = SafePatch.GetMethodSafe(typeName, methodName, args, true);
            if (target is null) throw new($"Target method missing: {typeName}.{methodName}");

            HarmonyMethod hmPrefix = prefix is not null ? new(prefix) : null;
            if (hmPrefix is not null && priority >= 0) hmPrefix.priority = priority;

            HarmonyMethod hmPostfix = postfix is not null ? new(postfix) : null;
            if (hmPostfix is not null && priority >= 0) hmPostfix.priority = priority;

            harmony.Patch(target, prefix: hmPrefix, postfix: hmPostfix);
            ModLogger.Log($"Applied safe patch {name} on {typeName}.{methodName}");
        }
    }
}
