using HarmonyLib;
using Vintagestory.API.Common;

namespace AnimationManagerLib.Patches
{
    static class ClientAnimatorPatch
    {
        public delegate void OnFrameHandler(AnimatorBase animator, float dt);
        public static event OnFrameHandler OnFrameCallback;
        public static ICoreAPI Api { get; set; }

        public static void Patch(string harmonyId)
        {
            var OriginalMethod = AccessTools.Method(typeof(AnimatorBase), nameof(AnimatorBase.OnFrame));
            var PostfixMethod = AccessTools.Method(typeof(ClientAnimatorPatch), nameof(OnFrame));
            new Harmony(harmonyId).Patch(OriginalMethod, prefix: new HarmonyMethod(PostfixMethod));
        }

        public static void Unpatch(string harmonyId)
        {
            var OriginalMethod = AccessTools.Method(typeof(AnimatorBase), nameof(AnimatorBase.OnFrame));
            new Harmony(harmonyId).Unpatch(OriginalMethod, HarmonyPatchType.Prefix, harmonyId);
        }

        public static void OnFrame(AnimatorBase __instance, float dt) => OnFrameCallback?.Invoke(__instance, dt);
    }
}
