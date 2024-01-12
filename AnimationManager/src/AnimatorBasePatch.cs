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
using Vintagestory.GameContent;

namespace AnimationManagerLib.Patches;

static class AnimatorBasePatch
{
    public delegate void OnFrameHandler(Entity entity, float dt);
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
                prefix: new HarmonyMethod(AccessTools.Method(typeof(AnimatorBasePatch), nameof(OnFrame)))
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
                prefix: new HarmonyMethod(AccessTools.Method(typeof(AnimatorBasePatch), nameof(CalculateMatrices)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityShapeRenderer).GetMethod("RenderHeldItem", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(AnimatorBasePatch), nameof(RenderHeldItem)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityPlayer).GetMethod("updateEyeHeight", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(EyeHightController), nameof(EyeHightController.UpdateEyeHeight)))
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
    }

    public static void OnFrame(Vintagestory.API.Common.AnimationManager __instance, float dt)
    {
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        Entity? entity = (Entity?)typeof(Vintagestory.API.Common.AnimationManager)
                                          .GetField("entity", BindingFlags.NonPublic | BindingFlags.Instance)
                                          ?.GetValue(__instance);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields 

        foreach (string code in __instance.ActiveAnimationsByAnimCode.Select(entry => entry.Key).Where(SuppressedAnimations.Contains))
        {
            __instance.StopAnimation(code);
        }

        if (entity != null) OnFrameCallback?.Invoke(entity, dt);
    }
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

            OnCalculateWeightCallback?.Invoke(outFramePose, ref weightSum);

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

            OnElementPoseUsedCallback?.Invoke(outFramePose, ref weightSumCopy);


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

internal static class EyeHightController
{
    public static float Amplitude = 1.0f;
    public static float Frequency = 1.0f;
    public static float SprintFrequencyEffect = 1.0f;
    public static float SprintAmplitudeEffect = 1.0f;
    public static float SneakEffect = 1.0f;
    public static float Offset = 1.0f;
    public static float LiquidEffect = 1.0f;


    private static bool CanStandUp(EntityPlayer __instance)
    {
        Field<Cuboidf, EntityPlayer> tmpCollBox = new(typeof(EntityPlayer), "tmpCollBox", __instance);

        tmpCollBox.Value.Set(__instance.SelectionBox);
        bool flag = __instance.World.CollisionTester.IsColliding(__instance.World.BlockAccessor, tmpCollBox.Value, __instance.Pos.XYZ, alsoCheckTouch: false);
        tmpCollBox.Value.Y2 = __instance.Properties.CollisionBoxSize.Y;
        tmpCollBox.Value.Y1 += 1f;
        return !__instance.World.CollisionTester.IsColliding(__instance.World.BlockAccessor, tmpCollBox.Value, __instance.Pos.XYZ, alsoCheckTouch: false) || flag;
    }
    private static void UpdateLocalEyePosImmersiveFpMode(EntityPlayer __instance)
    {
        if (__instance.AnimManager.Animator == null) return;

        Field<bool, EntityPlayer> holdPosition = new(typeof(EntityPlayer), "holdPosition", __instance);
        Field<float, EntityPlayer> secondsDead = new(typeof(EntityPlayer), "secondsDead", __instance);
        Field<float[], EntityPlayer> prevAnimModelMatrix = new(typeof(EntityPlayer), "prevAnimModelMatrix", __instance);
        Field<float, EntityPlayer> sidewaysSwivelAngle = new(typeof(EntityPlayer), "sidewaysSwivelAngle", __instance);

        AttachmentPointAndPose apap = __instance.AnimManager.Animator.GetAttachmentPointPose("Eyes");
        AttachmentPoint ap = apap.AttachPoint;

        float[] ModelMat = Mat4f.Create();
        Matrixf tmpModelMat = new();

        float bodyYaw = __instance.BodyYaw;
        float rotX = __instance.Properties.Client.Shape != null ? __instance.Properties.Client.Shape.rotateX : 0;
        float rotY = __instance.Properties.Client.Shape != null ? __instance.Properties.Client.Shape.rotateY : 0;
        float rotZ = __instance.Properties.Client.Shape != null ? __instance.Properties.Client.Shape.rotateZ : 0;
        float bodyPitch = __instance.WalkPitch;

        float lookOffset = (__instance.SidedPos.Pitch - GameMath.PI) / 9f;
        if (!__instance.Alive) lookOffset /= secondsDead.Value * 10;

        bool wasHoldPos = holdPosition.Value;
        holdPosition.Value = false;

        for (int i = 0; i < __instance.AnimManager.Animator.RunningAnimations.Length; i++)
        {
            RunningAnimation anim = __instance.AnimManager.Animator.RunningAnimations[i];
            if (anim.Running && anim.EasingFactor >= anim.meta.HoldEyePosAfterEasein)
            {
                if (!wasHoldPos)
                {
                    prevAnimModelMatrix.Value = (float[])apap.AnimModelMatrix.Clone();
                }
                holdPosition.Value = true;
                break;
            }
        }


        tmpModelMat
            .Set(ModelMat)
            .RotateX(__instance.SidedPos.Roll + rotX * GameMath.DEG2RAD)
            .RotateY(bodyYaw + (180 + rotY) * GameMath.DEG2RAD)
            .RotateZ(bodyPitch + rotZ * GameMath.DEG2RAD)
            .Scale(__instance.Properties.Client.Size, __instance.Properties.Client.Size, __instance.Properties.Client.Size)
            .Translate(-0.5f, 0, -0.5f)
            .RotateX(sidewaysSwivelAngle.Value)
            .Translate(ap.PosX / 16f - lookOffset * 1.3f, ap.PosY / 16f, ap.PosZ / 16f)
            .Mul(holdPosition.Value ? prevAnimModelMatrix.Value : apap.AnimModelMatrix)
            .Translate(0.07f, __instance.Alive ? 0.0f : 0.2f * Math.Min(1, secondsDead.Value), 0f)
        ;

        float[] pos = new float[4] { 0, 0, 0, 1 };
        float[] endVec = Mat4f.MulWithVec4(tmpModelMat.Values, pos);

        __instance.LocalEyePos.Set(endVec[0], endVec[1], endVec[2]);
    }
    private static double CalculateStepHeight(EntityPlayer __instance, float dt, EntityControls controls, bool walking)
    {
        Field<double, EntityPlayer> walkCounter = new(typeof(EntityPlayer), "walkCounter", __instance);

        double frequency = Frequency * dt * controls.MovespeedMultiplier * __instance.GetWalkSpeedMultiplier(0.3) * (controls.Sprint ? 0.9 * SprintFrequencyEffect : 1.2) * (controls.Sneak ? 1.2f : 1);

        walkCounter.Value = walking ? walkCounter.Value + frequency : 0;
        walkCounter.Value = walkCounter.Value % GameMath.TWOPI;

        double sneakDiv = controls.Sneak ? 5 * SneakEffect : 1.8;
        double sprint = controls.Sprint ? 0.07 * SprintAmplitudeEffect : 0;
        double amplitude = (__instance.FeetInLiquid ? 0.8 * LiquidEffect : 1 + sprint) / (3 * sneakDiv) * Amplitude;
        double offset = -0.2 / sneakDiv * Offset;

        double stepHeight = -Math.Max(0, Math.Abs(GameMath.Sin(5.5f * walkCounter.Value) * amplitude) + offset);

        return stepHeight;
    }
    private static void CalculateCollisionBox(EntityPlayer __instance, float dt, EntityControls controls)
    {
        __instance.PrevFrameCanStandUp = !controls.Sneak && CanStandUp(__instance);

        double newEyeheight = __instance.Properties.EyeHeight;
        double newModelHeight = __instance.Properties.CollisionBoxSize.Y;

        if (controls.FloorSitting)
        {
            newEyeheight *= 0.5f;
            newModelHeight *= 0.55f;
        }
        else if ((controls.Sneak || !__instance.PrevFrameCanStandUp) && !controls.IsClimbing && !controls.IsFlying)
        {
            newEyeheight *= 0.8f;
            newModelHeight *= 0.8f;
        }
        else if (!__instance.Alive)
        {
            newEyeheight *= 0.25f;
            newModelHeight *= 0.25f;
        }

        double diff = (newEyeheight - __instance.LocalEyePos.Y) * 5 * dt;
        __instance.LocalEyePos.Y = diff > 0 ? Math.Min(__instance.LocalEyePos.Y + diff, newEyeheight) : Math.Max(__instance.LocalEyePos.Y + diff, newEyeheight);

        diff = (newModelHeight - __instance.OriginSelectionBox.Y2) * 5 * dt;
        __instance.OriginSelectionBox.Y2 = __instance.SelectionBox.Y2 = (float)(diff > 0 ? Math.Min(__instance.SelectionBox.Y2 + diff, newModelHeight) : Math.Max(__instance.SelectionBox.Y2 + diff, newModelHeight));

        diff = (newModelHeight - __instance.OriginCollisionBox.Y2) * 5 * dt;
        __instance.OriginCollisionBox.Y2 = __instance.CollisionBox.Y2 = (float)(diff > 0 ? Math.Min(__instance.CollisionBox.Y2 + diff, newModelHeight) : Math.Max(__instance.CollisionBox.Y2 + diff, newModelHeight));
    }

    private static bool Stepped(EntityPlayer __instance)
    {
        Field<int, EntityPlayer> direction = new(typeof(EntityPlayer), "direction", __instance);
        return direction.Value == -1;
    }
    private static void Step(EntityPlayer __instance, IPlayer player)
    {
        Field<int, EntityPlayer> direction = new(typeof(EntityPlayer), "direction", __instance);
        __instance.PlayStepSound(player, __instance.PlayInsideSound(player));
        direction.Value = -1;
    }
    private static void Unstep(EntityPlayer __instance)
    {
        Field<int, EntityPlayer> direction = new(typeof(EntityPlayer), "direction", __instance);
        direction.Value = 1;
    }
    private static void PlayStepSound(EntityPlayer __instance, IPlayer player, bool moving, bool walking, double stepHeight)
    {
        if (!walking || !moving) return;

        Field<double, EntityPlayer> prevStepHeight = new(typeof(EntityPlayer), "prevStepHeight", __instance);
        bool movingDown = stepHeight <= prevStepHeight.Value;

        if (movingDown)
        {
            Unstep(__instance);
        }
        else if (!Stepped(__instance))
        {
            Step(__instance, player);
        }
    }

    public static bool UpdateEyeHeight(EntityPlayer __instance, float dt)
    {
        Field<EntityControls, EntityAgent> servercontrols = new(typeof(EntityAgent), "servercontrols", __instance);
        Field<float, EntityPlayer> secondsDead = new(typeof(EntityPlayer), "secondsDead", __instance);
        Field<double, EntityPlayer> prevStepHeight = new(typeof(EntityPlayer), "prevStepHeight", __instance);

        IPlayer player = __instance.World.PlayerByUid(__instance.PlayerUID);

        if (__instance.World.Side != EnumAppSide.Client) return false;

        __instance.PrevFrameCanStandUp = true;

        if (player?.WorldData?.CurrentGameMode == null || player.WorldData?.CurrentGameMode == EnumGameMode.Spectator) return false;

        EntityControls controls = __instance.MountedOn != null ? __instance.MountedOn.Controls : servercontrols.Value;

        CalculateCollisionBox(__instance, dt, controls);

        bool moving = (controls.TriesToMove && __instance.SidedPos.Motion.LengthSq() > 0.00001) && !controls.NoClip && !controls.DetachedMode;
        bool walking = moving && __instance.OnGround;

        __instance.LocalEyePos.X = 0;
        __instance.LocalEyePos.Z = 0;

        if (__instance.MountedOn?.LocalEyePos != null)
        {
            __instance.LocalEyePos.Set(__instance.MountedOn.LocalEyePos);
        }

        // Immersive fp mode has its own way of setting the eye pos
        // but we still need to run above non-ifp code for the hitbox
        if (player.ImmersiveFpMode || !__instance.Alive)
        {
            secondsDead.Value = __instance.Alive ? 0 : secondsDead.Value + dt;
            UpdateLocalEyePosImmersiveFpMode(__instance);
        }

        double stepHeight = CalculateStepHeight(__instance, dt, controls, walking);

        ICoreClientAPI? clientApi = __instance.World.Api as ICoreClientAPI;
        if (clientApi?.Settings.Bool["viewBobbing"] == true && clientApi.Render.CameraType == EnumCameraMode.FirstPerson)
        {
            __instance.LocalEyePos.Y += stepHeight / 3f * dt * 60f;
        }

        PlayStepSound(__instance, player, moving, walking, stepHeight);

        prevStepHeight.Value = stepHeight;

        return false;
    }

}

internal sealed class Field<TValue, TInstance>
{
    public TValue? Value
    {
        get
        {
            return (TValue?)mFieldInfo?.GetValue(mInstance);
        }
        set
        {
            mFieldInfo?.SetValue(mInstance, value);
        }
    }

    private readonly FieldInfo? mFieldInfo;
    private readonly TInstance mInstance;

    public Field(Type from, string field, TInstance instance)
    {
        mInstance = instance;
        mFieldInfo = from.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
    }
}

internal sealed class Property<TValue, TInstance>
{
    public TValue? Value
    {
        get
        {
            return (TValue?)mPropertyInfo?.GetValue(mInstance);
        }
        set
        {
            mPropertyInfo?.SetValue(mInstance, value);
        }
    }

    private readonly PropertyInfo? mPropertyInfo;
    private readonly TInstance mInstance;

    public Property(Type from, string property, TInstance instance)
    {
        mInstance = instance;
        mPropertyInfo = from.GetProperty(property, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
    }
}
