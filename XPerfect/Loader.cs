#if ML
using MelonLoader;

[assembly: MelonInfo(typeof(XPerfect.Loader), "XPerfect", "3.1.0", "8100print & Kkitut")]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace XPerfect {
    public class Loader : MelonMod {
        public static Loader Instance { get; private set; }

        public override void OnInitializeMelon() {
            Instance = this;
            if (!Main.InitializeML()) {
                ModLogger.Error("Failed to initialize XPerfect due to safe patch failure. Aborting mod load.");
                return;
            }
        }

        public override void OnDeinitializeMelon() {
            Main.Dispose();
            Instance = null;
        }
    }
}
#endif
