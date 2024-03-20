using AnimationManagerLib.CollectibleBehaviors;
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

namespace AnimationManagerLib.Patches;

internal static class AnimatorPatch
{
    public delegate void OnFrameHandler(Vintagestory.API.Common.AnimationManager manager, Entity entity, float dt);
    public static event OnFrameHandler? OnFrameCallback;

    public delegate void OnElementPoseUsedHandler(ElementPose pose, ref float weight);
    public static event OnElementPoseUsedHandler? OnElementPoseUsedCallback;

    public delegate void OnCalculateWeight(ElementPose pose, ref float weight);
    public static event OnCalculateWeight? OnCalculateWeightCallback;

    public static HashSet<string> SuppressedAnimations { get; } = new();

    public static void Patch(string harmonyId)
    {
        new Harmony(harmonyId).Patch(
                AccessTools.Method(typeof(Vintagestory.API.Common.AnimationManager), nameof(Vintagestory.API.Common.AnimationManager.OnClientFrame)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(AnimatorPatch), nameof(OnFrame)))
            );

        new Harmony(harmonyId).Patch(
            typeof(ClientAnimator).GetMethod("calculateMatrices", AccessTools.all, new Type[] {
                typeof(int),
                typeof(float),
                typeof(List<ElementPose>),
                typeof(ShapeElementWeights[][]),
                typeof(float[]),
                typeof(List<ElementPose>[]),
                typeof(List<ElementPose>[]),
                typeof(int)
            }),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(AnimatorPatch), nameof(CalculateMatrices)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityShapeRenderer).GetMethod("RenderHeldItem", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(AnimatorPatch), nameof(RenderHeldItem)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityPlayer).GetMethod("updateEyeHeight", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(EyeHightController), nameof(EyeHightController.UpdateEyeHeight)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityPlayerShapeRenderer).GetMethod("loadModelMatrixForPlayer", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(PlayerModelMatrixController), nameof(PlayerModelMatrixController.LoadModelMatrixForPlayer)))
            );

        
    }

    public static void Unpatch(string harmonyId)
    {
        new Harmony(harmonyId).Unpatch(typeof(ClientAnimator).GetMethod("calculateMatrices", AccessTools.all, new Type[] {
                typeof(int),
                typeof(float),
                typeof(List<ElementPose>),
                typeof(ShapeElementWeights[][]),
                typeof(float[]),
                typeof(List<ElementPose>[]),
                typeof(List<ElementPose>[]),
                typeof(int)
            }), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityShapeRenderer).GetMethod("RenderHeldItem", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityPlayer).GetMethod("updateEyeHeight", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(AccessTools.Method(typeof(Vintagestory.API.Common.AnimationManager), nameof(Vintagestory.API.Common.AnimationManager.OnClientFrame)), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityPlayerShapeRenderer).GetMethod("loadModelMatrixForPlayer", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
    }

    public static void OnFrame(Vintagestory.API.Common.AnimationManager __instance, float dt)
    {
        Entity? entity = (Entity?)typeof(Vintagestory.API.Common.AnimationManager)
                                          .GetField("entity", BindingFlags.NonPublic | BindingFlags.Instance)
                                          ?.GetValue(__instance);

        foreach (string code in __instance.ActiveAnimationsByAnimCode.Select(entry => entry.Key).Where(SuppressedAnimations.Contains))
        {
            __instance.StopAnimation(code);
        }

        if (entity != null)
        {
            try
            {
                OnFrameCallback?.Invoke(__instance, entity, dt);
            }
            catch (Exception exception)
            {
#if DEBUG
                entity.Api.Logger.Error($"[AM lib] Exception while calling 'OnFrameCallback': {exception}");
#endif
            }
        }
    }
    public static bool RenderHeldItem(EntityShapeRenderer __instance, float dt, bool isShadowPass, bool right)
    {
        if (!isShadowPass && right)
        {
            ItemSlot? slot = (__instance.entity as EntityPlayer)?.RightHandItemSlot;
            Animatable? behavior = slot?.Itemstack?.Item?.GetBehavior<AnimatableProcedural>()
                ?? slot?.Itemstack?.Item?.GetBehavior<AnimatableAttachable>()
                ?? slot?.Itemstack?.Item?.GetBehavior<Animatable>();

            if (slot == null || behavior == null) return true;

            ItemRenderInfo renderInfo = __instance.capi.Render.GetItemStackRenderInfo(slot, EnumItemRenderTarget.HandTp, dt);

            behavior.BeforeRender(__instance.capi, slot.Itemstack, __instance.entity, EnumItemRenderTarget.HandFp, dt);

            (string textureName, _) = slot.Itemstack.Item.Textures.First();

            TextureAtlasPosition atlasPos = __instance.capi.ItemTextureAtlas.GetPosition(slot.Itemstack.Item, textureName);

            renderInfo.TextureId = atlasPos.atlasTextureId;

            Vec4f? lightrgbs = (Vec4f?)typeof(EntityShapeRenderer)
                                              .GetField("lightrgbs", BindingFlags.NonPublic | BindingFlags.Instance)
                                              ?.GetValue(__instance);

            behavior.RenderHeldItem(__instance.ModelMat, __instance.capi, slot, __instance.entity, lightrgbs, dt, isShadowPass, right, renderInfo);

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
            .Translate(attachPoint.PosX / 16.0 + itemStackRenderInfo.Transform.Translation.X, attachPoint.PosY / 16.0 + itemStackRenderInfo.Transform.Translation.Y, attachPoint.PosZ / 16.0 + itemStackRenderInfo.Transform.Translation.Z)
            .RotateX((float)(attachPoint.RotationX + itemStackRenderInfo.Transform.Rotation.X) * (MathF.PI / 180f))
            .RotateY((float)(attachPoint.RotationY + itemStackRenderInfo.Transform.Rotation.Y) * (MathF.PI / 180f))
            .RotateZ((float)(attachPoint.RotationZ + itemStackRenderInfo.Transform.Rotation.Z) * (MathF.PI / 180f));


        return itemModelMat;
    }


    private static bool CalculateMatrices(
        ClientAnimator __instance,
        int animVersion,
        float dt,
        List<ElementPose> outFrame,
        ShapeElementWeights[][] weightsByAnimationAndElement,
        float[] modelMatrix,
        List<ElementPose>[] nowKeyFrameByAnimation,
        List<ElementPose>[] nextInKeyFrameByAnimation,
        int depth
    )
    {
        Field<float[], ClientAnimator> localTransformMatrix = new(typeof(ClientAnimator), "localTransformMatrix", __instance);
        Field<List<ElementPose>[][], ClientAnimator> frameByDepthByAnimation = new(typeof(ClientAnimator), "frameByDepthByAnimation", __instance);
        Field<List<ElementPose>[][], ClientAnimator> nextFrameTransformsByAnimation = new(typeof(ClientAnimator), "nextFrameTransformsByAnimation", __instance);
        Field<ShapeElementWeights[][][], ClientAnimator> weightsByAnimationAndElement_this = new(typeof(ClientAnimator), "weightsByAnimationAndElement", __instance);

        depth++;
        List<ElementPose>[] nowChildKeyFrameByAnimation = frameByDepthByAnimation.Value[depth];
        List<ElementPose>[] nextChildKeyFrameByAnimation = nextFrameTransformsByAnimation.Value[depth];
        ShapeElementWeights[][] childWeightsByAnimationAndElement = weightsByAnimationAndElement_this.Value[depth];


        for (int childPoseIndex = 0; childPoseIndex < outFrame.Count; childPoseIndex++)
        {
            ElementPose outFramePose = outFrame[childPoseIndex];
            ShapeElement elem = outFramePose.ForElement;

            SetMat(outFramePose, modelMatrix);
            Mat4f.Identity(localTransformMatrix.Value);

            outFramePose.Clear();

            float weightSum = SumWeights(childPoseIndex, __instance, weightsByAnimationAndElement);
            float weightSumCopy = weightSum;

            try
            {
                OnCalculateWeightCallback?.Invoke(outFramePose, ref weightSum);
            }
            catch (Exception exception)
            {
#if DEBUG
                Console.WriteLine($"[AM lib] Exception while calling 'OnCalculateWeightCallback': {exception}");
#endif
            }

            CalculateAnimationForElements(
                    __instance,
                    nowChildKeyFrameByAnimation,
                    nextChildKeyFrameByAnimation,
                    childWeightsByAnimationAndElement,
                    nowKeyFrameByAnimation,
                    nextInKeyFrameByAnimation,
                    weightsByAnimationAndElement,
                    outFramePose,
                    ref weightSum,
                    childPoseIndex
                    );

            try
            {
                OnElementPoseUsedCallback?.Invoke(outFramePose, ref weightSumCopy);
            }
            catch (Exception exception)
            {
#if DEBUG
                Console.WriteLine($"[AM lib] Exception while calling 'OnElementPoseUsedCallback': {exception}");
#endif
            }
            


            elem.GetLocalTransformMatrix(animVersion, localTransformMatrix.Value, outFramePose);
            Mat4f.Mul(outFramePose.AnimModelMatrix, outFramePose.AnimModelMatrix, localTransformMatrix.Value);

            CalculateElementTransformMatrices(__instance, elem, outFramePose);

            if (outFramePose.ChildElementPoses != null)
            {
                CalculateMatrices(
                    __instance,
                    animVersion,
                    dt,
                    outFramePose.ChildElementPoses,
                    childWeightsByAnimationAndElement,
                    outFramePose.AnimModelMatrix,
                    nowChildKeyFrameByAnimation,
                    nextChildKeyFrameByAnimation,
                    depth
                );
            }

        }

        return false;
    }

    private static void SetMat(ElementPose __instance, float[] modelMatrix)
    {
        for (int i = 0; i < 16; i++)
        {
            __instance.AnimModelMatrix[i] = modelMatrix[i];
        }
    }

    private static float SumWeights(int childPoseIndex, AnimatorBase __instance, ShapeElementWeights[][] weightsByAnimationAndElement)
    {
        Field<int, AnimatorBase> activeAnimCount = new(typeof(AnimatorBase), "activeAnimCount", __instance);

        float weightSum = 0f;
        for (int animIndex = 0; animIndex < activeAnimCount.Value; animIndex++)
        {
            RunningAnimation anim = __instance.CurAnims[animIndex];
            ShapeElementWeights sew = weightsByAnimationAndElement[animIndex][childPoseIndex];

            if (sew.BlendMode != EnumAnimationBlendMode.Add)
            {
                weightSum += sew.Weight * anim.EasingFactor;
            }
        }

        return weightSum;
    }

    private static void CalculateAnimationForElements(
        ClientAnimator __instance,
        List<ElementPose>[] nowChildKeyFrameByAnimation,
        List<ElementPose>[] nextChildKeyFrameByAnimation,
        ShapeElementWeights[][] childWeightsByAnimationAndElement,
        List<ElementPose>[] nowKeyFrameByAnimation,
        List<ElementPose>[] nextInKeyFrameByAnimation,
        ShapeElementWeights[][] weightsByAnimationAndElement,
        ElementPose outFramePose,
        ref float weightSum,
        int childPoseIndex
    )
    {
        Field<int, AnimatorBase> activeAnimCount = new(typeof(AnimatorBase), "activeAnimCount", __instance);

        for (int animIndex = 0; animIndex < activeAnimCount.Value; animIndex++)
        {
            Field<int[], ClientAnimator> prevFrameArray = new(typeof(ClientAnimator), "prevFrame", __instance);
            Field<int[], ClientAnimator> nextFrameArray = new(typeof(ClientAnimator), "nextFrame", __instance);

            RunningAnimation anim = __instance.CurAnims[animIndex];
            ShapeElementWeights sew = weightsByAnimationAndElement[animIndex][childPoseIndex];
            CalcBlendedWeight(anim, weightSum / sew.Weight, sew.BlendMode);

            ElementPose nowFramePose = nowKeyFrameByAnimation[animIndex][childPoseIndex];
            ElementPose nextFramePose = nextInKeyFrameByAnimation[animIndex][childPoseIndex];

            int prevFrame = prevFrameArray.Value[animIndex];
            int nextFrame = nextFrameArray.Value[animIndex];

            // May loop around, so nextFrame can be smaller than prevFrame
            float keyFrameDist = nextFrame > prevFrame ? (nextFrame - prevFrame) : (anim.Animation.QuantityFrames - prevFrame + nextFrame);
            float curFrameDist = anim.CurrentFrame >= prevFrame ? (anim.CurrentFrame - prevFrame) : (anim.Animation.QuantityFrames - prevFrame + anim.CurrentFrame);

            float lerp = curFrameDist / keyFrameDist;

            outFramePose.Add(nowFramePose, nextFramePose, lerp, anim.BlendedWeight);

            nowChildKeyFrameByAnimation[animIndex] = nowFramePose.ChildElementPoses;
            childWeightsByAnimationAndElement[animIndex] = sew.ChildElements;

            nextChildKeyFrameByAnimation[animIndex] = nextFramePose.ChildElementPoses;
        }
    }

    private static void CalcBlendedWeight(RunningAnimation __instance, float weightSum, EnumAnimationBlendMode blendMode)
    {
        if (weightSum == 0f)
        {
            __instance.BlendedWeight = __instance.EasingFactor;
        }
        else
        {
            __instance.BlendedWeight = GameMath.Clamp((blendMode == EnumAnimationBlendMode.Add) ? __instance.EasingFactor : (__instance.EasingFactor / Math.Max(__instance.meta.WeightCapFactor, weightSum)), 0f, 1f);
        }
    }

    private static void CalculateElementTransformMatrices(ClientAnimator __instance, ShapeElement element, ElementPose pose)
    {
        Field<HashSet<int>, ClientAnimator> jointsDone = new(typeof(ClientAnimator), "jointsDone", __instance);
        Field<float[], ClientAnimator> tmpMatrix = new(typeof(ClientAnimator), "tmpMatrix", __instance);

        if (element.JointId > 0 && !jointsDone.Value.Contains(element.JointId))
        {
            Mat4f.Mul(tmpMatrix.Value, pose.AnimModelMatrix, element.inverseModelTransform);

            int index = 12 * element.JointId;
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[0];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[1];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[2];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[4];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[5];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[6];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[8];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[9];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[10];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[12];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[13];
            __instance.TransformationMatrices4x3[index++] = tmpMatrix.Value[14];

            jointsDone.Value.Add(element.JointId);
        }
    }
}
