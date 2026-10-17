using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace XPerfect {
    public static class SafePatch {
        private static readonly Dictionary<string, Type> TypeCache = new(StringComparer.Ordinal);
        private static Assembly _assemblyCSharp;
        private static Type[] _csharpTypes;

        public static Type GetTypeSafe(string typeName) {
            if (string.IsNullOrEmpty(typeName)) return null;
            if (TypeCache.TryGetValue(typeName, out var cached)) return cached;

            _assemblyCSharp ??= AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            Type type = null;
            if (_assemblyCSharp != null) {
                type = _assemblyCSharp.GetType(typeName);
                if (type == null) {
                    _csharpTypes ??= _assemblyCSharp.GetTypes();
                    for (int i = 0; i < _csharpTypes.Length; i++) {
                        if (_csharpTypes[i].Name == typeName) {
                            type = _csharpTypes[i];
                            break;
                        }
                    }
                }
            }

            if (type == null) {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
                    if (a == _assemblyCSharp) continue;
                    try {
                        type = a.GetType(typeName);
                        if (type != null) break;
                        var types = a.GetTypes();
                        for (int i = 0; i < types.Length; i++) {
                            if (types[i].Name == typeName) {
                                type = types[i];
                                break;
                            }
                        }
                        if (type != null) break;
                    } catch { }
                }
            }

            if (type != null) TypeCache[typeName] = type;
            return type;
        }

        public static MethodBase GetMethodSafe(Type type, string methodName, Type[] args = null, bool allowStatic = true) {
            if (type == null) return null;
            BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            if (allowStatic) bf |= BindingFlags.Static;

            if (methodName == ".ctor") {
                return args is not null
                    ? type.GetConstructor(bf, null, args, null)
                        ?? throw new($"[{nameof(SafePatch)}] Constructor with specified args not found in {type.Name}")
                    : type.GetConstructors(bf).FirstOrDefault()
                        ?? throw new($"[{nameof(SafePatch)}] No constructors found in {type.Name}");
            }

            if (methodName == ".cctor") {
                return type.TypeInitializer
                    ?? throw new($"[{nameof(SafePatch)}] Static constructor not found in {type.Name}");
            }

            if (args is not null) {
                return type.GetMethod(methodName, bf, null, args, null)
                    ?? throw new($"[{nameof(SafePatch)}] Method {methodName} with specified args not found in {type.Name}");
            }

            var method = type.GetMethods(bf).FirstOrDefault(m => m.Name == methodName)
                ?? throw new($"[{nameof(SafePatch)}] Method {methodName} not found in {type.Name}");
            return method;
        }

        public static MethodBase GetMethodSafe(string typeName, string methodName, Type[] args = null, bool allowStatic = true) {
            var type = GetTypeSafe(typeName) ?? throw new($"[{nameof(SafePatch)}] Type not found: {typeName}");
            return GetMethodSafe(type, methodName, args, allowStatic);
        }
    }

    public static class SafePatchManager {
        public static Task<bool> PatchTask { get; internal set; }

        public static bool ApplyAll(HarmonyLib.Harmony harmony) {
            var sw = System.Diagnostics.Stopwatch.StartNew();
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

                sw.Stop();
                ModLogger.Log($"All safe patches applied successfully in {sw.ElapsedMilliseconds}ms.");
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
