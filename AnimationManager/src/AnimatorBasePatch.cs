using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AnimationManagerLib.Patches
{
    static class AnimatorBasePatch
    {
        public delegate void OnFrameHandler(Entity entity, float dt);
        public static event OnFrameHandler? OnFrameCallback;

        public delegate void OnElementPoseUsedHandler(ElementPose pose);
        public static event OnElementPoseUsedHandler? OnElementPoseUsedCallback;

        public static void Patch(string harmonyId)
        {
            new Harmony(harmonyId).Patch(
                    AccessTools.Method(typeof(Vintagestory.API.Common.AnimationManager), nameof(Vintagestory.API.Common.AnimationManager.OnClientFrame)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AnimatorBasePatch), nameof(OnFrame)))
                );

            new Harmony(harmonyId).Patch(
                    AccessTools.Method(typeof(ShapeElement), nameof(GetLocalTransformMatrix)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AnimatorBasePatch), nameof(GetLocalTransformMatrix)))
                );
        }

        public static void Unpatch(string harmonyId)
        {
            new Harmony(harmonyId).Unpatch(AccessTools.Method(typeof(ShapeElement), nameof(GetLocalTransformMatrix)), HarmonyPatchType.Prefix, harmonyId);
            new Harmony(harmonyId).Unpatch(AccessTools.Method(typeof(Vintagestory.API.Common.AnimationManager), nameof(Vintagestory.API.Common.AnimationManager.OnClientFrame)), HarmonyPatchType.Prefix, harmonyId);
        }

        public static void OnFrame(Vintagestory.API.Common.AnimationManager __instance, float dt)
        {
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
            Entity? entity = (Entity?)typeof(Vintagestory.API.Common.AnimationManager)
                                              .GetField("entity", BindingFlags.NonPublic | BindingFlags.Instance)
                                              ?.GetValue(__instance);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
            
            if (entity != null) OnFrameCallback?.Invoke(entity, dt);
        }
        public static void GetLocalTransformMatrix(ElementPose tf) => OnElementPoseUsedCallback?.Invoke(tf);
    }
}
 