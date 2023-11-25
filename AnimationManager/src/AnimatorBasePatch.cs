using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AnimationManagerLib.Patches
{
    static class AnimatorBasePatch
    {
        public delegate void OnFrameHandler(Entity entity, float dt);
        public static event OnFrameHandler OnFrameCallback;

        public delegate void OnElementPoseUsedHandler(ElementPose pose);
        public static event OnElementPoseUsedHandler OnElementPoseUsedCallback;

        public static void Patch(string harmonyId)
        {
            {
                var OriginalMethod = AccessTools.Method(typeof(AnimationManager), nameof(AnimationManager.OnClientFrame));
                var PrefixMethod = AccessTools.Method(typeof(AnimatorBasePatch), nameof(OnFrame));
                new Harmony(harmonyId).Patch(OriginalMethod, prefix: new HarmonyMethod(PrefixMethod));
            }

            {
                var OriginalMethod = AccessTools.Method(typeof(ShapeElement), nameof(GetLocalTransformMatrix));
                var PrefixMethod = AccessTools.Method(typeof(AnimatorBasePatch), nameof(GetLocalTransformMatrix));
                new Harmony(harmonyId).Patch(OriginalMethod, prefix: new HarmonyMethod(PrefixMethod));
            }
        }

        public static void Unpatch(string harmonyId)
        {
            {
                var OriginalMethod = AccessTools.Method(typeof(ShapeElement), nameof(GetLocalTransformMatrix));
                new Harmony(harmonyId).Unpatch(OriginalMethod, HarmonyPatchType.Prefix, harmonyId);
            }

            {
                var OriginalMethod = AccessTools.Method(typeof(AnimationManager), nameof(AnimationManager.OnClientFrame));
                new Harmony(harmonyId).Unpatch(OriginalMethod, HarmonyPatchType.Prefix, harmonyId);
            }
        }

        public static void OnFrame(AnimationManager __instance, float dt)
        {
            Entity entity = (Entity)typeof(AnimationManager)
                                              .GetField("entity", BindingFlags.NonPublic | BindingFlags.Instance)
                                              .GetValue(__instance);
            OnFrameCallback?.Invoke(entity, dt);
        }
        public static void GetLocalTransformMatrix(ElementPose tf) => OnElementPoseUsedCallback?.Invoke(tf);
    }
}
 