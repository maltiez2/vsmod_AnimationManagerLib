using AnimationManagerLib.CollectibleBehaviors;
using AnimationManagerLib.EntityRenderers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

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

            new Harmony(harmonyId).Patch(
                    typeof(EntityShapeRenderer).GetMethod("RenderHeldItem", AccessTools.all),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AnimatorBasePatch), nameof(RenderHeldItem)))
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
        public static bool RenderHeldItem(EntityShapeRenderer __instance, float dt, bool isShadowPass, bool right)
        {
            if (!isShadowPass && right)
            {
                ItemSlot? slot = (__instance.entity as EntityPlayer)?.RightHandItemSlot;
                Animatable? behavior = slot?.Itemstack?.Item?.GetBehavior<AnimatableProcedural>();

                if (slot == null || behavior == null) return true;

                ItemRenderInfo renderInfo = __instance.capi.Render.GetItemStackRenderInfo(slot, EnumItemRenderTarget.HandTp, dt);

                behavior.BeforeRender(__instance.capi, slot.Itemstack, EnumItemRenderTarget.HandFp, ref renderInfo);

                (string textureName, _) = slot.Itemstack.Item.Textures.First();

                TextureAtlasPosition atlasPos = __instance.capi.ItemTextureAtlas.GetPosition(slot.Itemstack.Item, textureName);

                renderInfo.TextureId = atlasPos.atlasTextureId;

#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                Vec4f? lightrgbs = (Vec4f?)typeof(EntityShapeRenderer)
                                                  .GetField("lightrgbs", BindingFlags.NonPublic | BindingFlags.Instance)
                                                  ?.GetValue(__instance);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields

                behavior.RenderHeldItem(__instance.ModelMat, __instance.capi, slot, __instance.entity, lightrgbs, dt, isShadowPass, right);

                return false;
            }


            return true;
        }

        public static Matrixf GetModelMatrix(float[] modelMat, ItemRenderInfo itemStackRenderInfo, bool right, Entity entity)
        {
            AttachmentPointAndPose attachmentPointAndPose = entity.AnimManager?.Animator?.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
            if (attachmentPointAndPose == null)
            {
                return null;
            }

            AttachmentPoint attachPoint = attachmentPointAndPose.AttachPoint;

            Matrixf itemModelMat = new();
            itemModelMat.Values = (float[])modelMat.Clone();
            itemModelMat.Mul(attachmentPointAndPose.AnimModelMatrix).Translate(itemStackRenderInfo.Transform.Origin.X, itemStackRenderInfo.Transform.Origin.Y, itemStackRenderInfo.Transform.Origin.Z)
                .Scale(itemStackRenderInfo.Transform.ScaleXYZ.X, itemStackRenderInfo.Transform.ScaleXYZ.Y, itemStackRenderInfo.Transform.ScaleXYZ.Z)
                .Translate(attachPoint.PosX / 16.0 + (double)itemStackRenderInfo.Transform.Translation.X, attachPoint.PosY / 16.0 + (double)itemStackRenderInfo.Transform.Translation.Y, attachPoint.PosZ / 16.0 + (double)itemStackRenderInfo.Transform.Translation.Z)
                .RotateX((float)(attachPoint.RotationX + (double)itemStackRenderInfo.Transform.Rotation.X) * (MathF.PI / 180f))
                .RotateY((float)(attachPoint.RotationY + (double)itemStackRenderInfo.Transform.Rotation.Y) * (MathF.PI / 180f))
                .RotateZ((float)(attachPoint.RotationZ + (double)itemStackRenderInfo.Transform.Rotation.Z) * (MathF.PI / 180f));


            return itemModelMat;
        }
    }
}
 