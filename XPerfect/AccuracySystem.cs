using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace XPerfect {
    public static class AccuracyState {
        public static bool HasPendingJudge { get; private set; }

        public static void RecordJudge() {
            HasPendingJudge = true;
        }

        public static void ConsumeJudge() {
            HasPendingJudge = false;
        }

        public static int GetXPerfectCount() {
            scrMarginTracker tracker = GetTracker();
            if (tracker is null) return 0;
            return RDC.auto
                ? tracker.GetHits(HitMargin.XPerfect, HitMargin.PerfectMinus, HitMargin.PerfectPlus)
                : tracker.GetHits(HitMargin.XPerfect);
        }

        public static int GetPlusPerfectCount() {
            scrMarginTracker tracker = GetTracker();
            return tracker is null || RDC.auto
                ? 0
                : tracker.GetHits(HitMargin.PerfectPlus);
        }

        public static int GetMinusPerfectCount() {
            scrMarginTracker tracker = GetTracker();
            return tracker is null || RDC.auto
                ? 0
                : tracker.GetHits(HitMargin.PerfectMinus);
        }

        private static scrMarginTracker GetTracker() {
            if (scrMistakesManager.marginTrackers is null || scrMistakesManager.marginTrackers.Length == 0) {
                return null;
            }

            return scrMistakesManager.marginTrackers[0];
        }

        public static void Reset() {
            HasPendingJudge = false;
        }
    }

    public static class HitMarginPatch {
        public static void Postfix(HitMargin __result) {
            try {
                if (!Main.Enabled) return;
                if (scrController.instance is null || scrConductor.instance is null) return;
                if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl) return;
                if (!CompetitiveModePrefs.enableCompetitiveMode) return;

                if (IsTrackedMargin(__result))
                    AccuracyState.RecordJudge();

                if (Main.Settings.XPerfectOnly && __result != HitMargin.XPerfect) {
                    if (ADOBase.controller is not null && !ADOBase.controller.noFail && !ADOBase.controller.currFloor.freeroam) {
                        XPerfectOnlyState.PendingFailMargin = __result;
                        XPerfectOnlyState.ShouldFailPlayer = true;
                    }
                }
            } catch (Exception ex) {
                ModLogger.Error($"[XPerfect] HitMargin error: {ex}");
            }
        }

        private static bool IsTrackedMargin(HitMargin margin) {
            return margin == HitMargin.XPerfect ||
                   margin == HitMargin.PerfectMinus ||
                   margin == HitMargin.PerfectPlus;
        }
    }

    public static class MistakesManagerAddHitPatch {
        public static void Postfix(HitMargin hit) {
            try {
                if (!Main.Enabled) return;
                if (hit != HitMargin.XPerfect && hit != HitMargin.PerfectMinus &&
                    hit != HitMargin.PerfectPlus && hit != HitMargin.Auto) return;
                if (scrController.instance is null || scrConductor.instance is null) return;
                if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl) return;

                if (!AccuracyState.HasPendingJudge) return;

                AccuracyState.ConsumeJudge();

                CounterDisplay.Refresh();
            } catch (Exception ex) {
                ModLogger.Error($"[XPerfect] AddHit error: {ex}");
            }
        }
    }

    public static class LevelStartPatch {
        public static void Postfix() {
            AccuracyState.Reset();
            XPerfectOnlyState.ShouldFailPlayer = false;
            CounterDisplay.Refresh();
            try { MeterVisualPatch.RefreshAllMeters(); } catch { }
        }
    }

    public static class CheckpointRestorePatch {
        public static void Postfix() {
            CounterDisplay.Refresh();
        }
    }

    public static class XPerfectOnlyState {
        public static bool ShouldFailPlayer;
        public static HitMargin PendingFailMargin;
    }

    public static class CompetitiveModePrefs {
        public static bool runtimeDisabled = false;
        public static bool enableCompetitiveMode {
            get {
                return Persistence.generalPrefs.GetBool("enableCompetitiveMode", false) && !CompetitiveModePrefs.runtimeDisabled;
            }
        }
    }

    public static class XPerfectOnlySwitchChosenPatch {
        public static void Postfix() {
            try {
                if (!XPerfectOnlyState.ShouldFailPlayer) return;

                HitMargin failedMargin = XPerfectOnlyState.PendingFailMargin;

                if (!CompetitiveModePrefs.enableCompetitiveMode) return;
                if (!Main.Enabled || !Main.Settings.XPerfectOnly) return;

                var ctrl = scrController.instance;
                if (ctrl is null || ctrl.playerOne is null || !ctrl.playerOne.alive) return;

                string failMessage = RDString.Get("status.purePerfectOnlyExplainer", new Dictionary<string, object> {
                    { "judgement", failedMargin.ToLocalized(null) }
                });

                ctrl.playerOne.Die(false, false, failMessage, false);
            } catch (Exception ex) {
                ModLogger.Error($"[XPerfect] XPerfectOnlySwitchChosenPatch error: {ex}");
            }
        }
    }

    public static class XPerfectOnlyIsCountedPatch {
        public static void Postfix(HitMargin hitMargin, HitMarginLimit? hitMarginLimit, ref bool __result) {
            if (!Main.Enabled || !Main.Settings.XPerfectOnly)
                return;

            if (!XPerfectOnlyState.ShouldFailPlayer)
                return;

            if (hitMargin != HitMargin.XPerfect)
                __result = false;
        }
    }

    public static class DetailedResultsPatch {
        private static int _savedPerfectText = 0;
        private static bool _hasSaved = false;
        private static int _depth = 0;

        public static void Prefix() {
            if (!Main.Enabled || !Main.Settings.MergePerfects) return;
            try {
                if (_depth == 0) {
                    _savedPerfectText = Persistence.generalPrefs.GetInt("hitMarginPerfectText", 0);
                    _hasSaved = true;
                }
                _depth++;
                Persistence.generalPrefs.SetInt("hitMarginPerfectText", 0);
            } catch (Exception ex) {
                ModLogger.Error($"[DetailedResultsPatch/Prefix] {ex}");
            }
        }

        public static void Postfix(scrMarginTracker marginTracker, ref string __result) {
            if (_depth > 0) {
                _depth--;
                if (_depth == 0 && _hasSaved) {
                    try {
                        Persistence.generalPrefs.SetInt("hitMarginPerfectText", _savedPerfectText);
                    } catch { }
                    _hasSaved = false;
                }
            }

            if (!Main.Enabled || !Main.Settings.MergePerfects) return;
            if (string.IsNullOrEmpty(__result) || marginTracker is null) return;

            try {
                int pm = marginTracker.GetHits(HitMargin.PerfectMinus);
                int xp = marginTracker.GetHits(HitMargin.XPerfect);
                int pp = marginTracker.GetHits(HitMargin.PerfectPlus);
                int auto = marginTracker.GetHits(HitMargin.Auto);

                string detail = $" <color=#60FF4E>[+{pp}/</color><color=#FFFFFF>{xp + auto}</color><color=#60FF4E>/-{pm}]</color>";
                if (__result.Contains(detail)) return;

                const string sep = "     ";
                int nl = __result.IndexOf('\n');
                string first = nl >= 0 ? __result.Substring(0, nl) : __result;
                string rest = nl >= 0 ? __result.Substring(nl) : "";
                string[] toks = first.Split(new[] { sep }, StringSplitOptions.None);
                if (toks.Length < 2) {
                    __result = __result + detail;
                    return;
                }
                toks[1] = toks[1] + detail;
                __result = string.Join(sep, toks) + rest;
            } catch (Exception ex) {
                ModLogger.Error($"[DetailedResultsPatch/Postfix] {ex}");
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal static void SelfCheck() {
            string test = "e: 1     p: 10     l: 2\nsecond line";
            string detail = " <color=#60FF4E>[+1/</color><color=#FFFFFF>8</color><color=#60FF4E>/-1]</color>";
            const string sep = "     ";
            int nl = test.IndexOf('\n');
            string first = nl >= 0 ? test.Substring(0, nl) : test;
            string rest = nl >= 0 ? test.Substring(nl) : "";
            string[] toks = first.Split(new[] { sep }, StringSplitOptions.None);
            toks[1] = toks[1] + detail;
            string res = string.Join(sep, toks) + rest;
            System.Diagnostics.Debug.Assert(res.Contains(detail));
            System.Diagnostics.Debug.Assert(res.EndsWith("\nsecond line"));
        }
    }
}
