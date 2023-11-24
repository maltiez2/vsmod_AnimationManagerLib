using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;

namespace AnimationManagerLib.Patches
{
    static class AnimatorBasePatch
    {
        public delegate void OnFrameHandler(AnimatorBase animator, float dt);
        public static event OnFrameHandler OnFrameCallback;

        public delegate void OnElementPoseUsedHandler(ElementPose pose);
        public static event OnElementPoseUsedHandler OnElementPoseUsedCallback;

        public static void Patch(string harmonyId)
        {
            {
                var OriginalMethod = typeof(ClientAnimator).GetMethod("calculateMatrices", AccessTools.all, new Type[] { typeof(float) });
                var PostfixMethod = AccessTools.Method(typeof(AnimatorBasePatch), nameof(OnFrame));
                new Harmony(harmonyId).Patch(OriginalMethod, postfix: new HarmonyMethod(PostfixMethod));
            }

            {
                var OriginalMethod = AccessTools.Method(typeof(ShapeElement), nameof(GetLocalTransformMatrix));
                var PostfixMethod = AccessTools.Method(typeof(AnimatorBasePatch), nameof(GetLocalTransformMatrix));
                new Harmony(harmonyId).Patch(OriginalMethod, prefix: new HarmonyMethod(PostfixMethod));
            }
        }

        public static void Unpatch(string harmonyId)
        {
            {
                var OriginalMethod = AccessTools.Method(typeof(ShapeElement), nameof(GetLocalTransformMatrix));
                new Harmony(harmonyId).Unpatch(OriginalMethod, HarmonyPatchType.Prefix, harmonyId);
            }

            {
                var OriginalMethod = typeof(ClientAnimator).GetMethod("calculateMatrices", AccessTools.all, new Type[] { typeof(float) });
                new Harmony(harmonyId).Unpatch(OriginalMethod, HarmonyPatchType.Postfix, harmonyId);
            }
        }

        public static void OnFrame(AnimatorBase __instance, float dt) => OnFrameCallback?.Invoke(__instance, dt);
        public static void GetLocalTransformMatrix(ElementPose tf) => OnElementPoseUsedCallback?.Invoke(tf);
    }
}
 