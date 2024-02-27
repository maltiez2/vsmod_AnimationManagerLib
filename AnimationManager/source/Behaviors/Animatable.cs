using AnimationManagerLib.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib.CollectibleBehaviors;

public class Animatable : CollectibleBehavior // Based on code from TeacupAngel (https://github.com/TeacupAngel)
{
    public bool RenderProceduralAnimations { get; set; }
    public Shape? CurrentShape => CurrentAnimatableShape?.Shape;
    public AnimatableShape? CurrentAnimatableShape => (mCurrentFirstPerson ? mShapeFirstPerson : mShape) ?? mShape ?? mShapeFirstPerson;
    public Shape? FirstPersonShape => mShapeFirstPerson?.Shape;
    public Shape? ThirdPersonShape => mShape?.Shape;

    protected Dictionary<string, AnimationMetaData> mActiveAnimationsByCode = new();
    protected AnimationManagerLibSystem? mModSystem;
    protected ICoreClientAPI? mClientApi;
    protected string? mAnimatedShapePath;
    protected string? mAnimatedShapeFirstPersonPath;
    protected bool mOnlyWhenAnimating;
    protected AnimatableShape? mShape;
    protected AnimatableShape? mShapeFirstPerson;
    protected Matrixf mItemModelMat = new();
    protected float mTimeAccumulation = 0;
    protected bool mCurrentFirstPerson = false;

    public Animatable(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        mAnimatedShapePath = properties["animated-shape"].AsString(null);
        mAnimatedShapeFirstPersonPath = properties["animated-shape-fp"].AsString(null);
        mOnlyWhenAnimating = properties["only-when-animating"].AsBool(true);

        base.Initialize(properties);
    }

    public override void OnLoaded(ICoreAPI api)
    {
        mModSystem = api.ModLoader.GetModSystem<AnimationManagerLibSystem>();

        if (api is ICoreClientAPI clientApi)
        {
            if (collObj is not Item)
            {
                throw new InvalidOperationException("CollectibleBehaviorAnimatable can only be used on Items, not Blocks!");
            }

            mClientApi = clientApi;

            InitAnimatable();
        }
    }

    public virtual void InitAnimatable()
    {
        Item? item = (collObj as Item);

        if (mClientApi == null || (item?.Shape == null && mAnimatedShapePath == null && mAnimatedShapeFirstPersonPath == null)) return;

        mShape = AnimatableShape.Create(mClientApi, mAnimatedShapePath ?? mAnimatedShapeFirstPersonPath ?? item.Shape.Base.ToString() ?? "");
        mShapeFirstPerson = AnimatableShape.Create(mClientApi, mAnimatedShapeFirstPersonPath ?? mAnimatedShapePath ?? item.Shape.Base.ToString() ?? "");
    }

    [Obsolete("Not supported currently")]
    public void StartAnimation(AnimationMetaData metaData, Entity entity)
    {
        if (mClientApi?.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented.");

        if (!mActiveAnimationsByCode.ContainsKey(metaData.Code))
        {
            mActiveAnimationsByCode[metaData.Code] = metaData;
        }
    }

    [Obsolete("Not supported currently")]
    public void StopAnimation(string code, Entity entity, bool forceImmediate = false)
    {
        if (mClientApi == null) return;
        if (mClientApi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented.");
        if (mShape == null) return;

        var animator = mShape.GetAnimator(entity.EntityId);

        if (animator != null && mActiveAnimationsByCode.ContainsKey(code) && forceImmediate)
        {
            RunningAnimation? animation = Array.Find(animator.anims, (animation) => { return animation.Animation.Code == code; });
            Debug.Assert(animation != null);
            animation.EasingFactor = 0f;
        }

        mActiveAnimationsByCode.Remove(code);
    }

    public virtual void BeforeRender(ICoreClientAPI clientApi, ItemStack itemStack, Entity player, EnumItemRenderTarget target, float dt)
    {
        mCurrentFirstPerson = IsFirstPerson(player);

        CalculateAnimation(CurrentAnimatableShape?.GetAnimator(player.EntityId), clientApi, player, target, dt);
    }

    public virtual void RenderShape(IShaderProgram shaderProgram, IWorldAccessor world, AnimatableShape shape, ItemRenderInfo itemStackRenderInfo, IRenderAPI render, ItemStack itemStack, Vec4f lightrgbs, Matrixf itemModelMat, ItemSlot itemSlot, Entity entity, float dt)
    {
        CurrentAnimatableShape?.Render(shaderProgram, itemStackRenderInfo, render, itemStack, lightrgbs, itemModelMat, entity, dt);
    }

    public void RenderHeldItem(float[] modelMat, ICoreClientAPI api, ItemSlot itemSlot, Entity entity, Vec4f lightrgbs, float dt, bool isShadowPass, bool right, ItemRenderInfo renderInfo)
    {
        if (CurrentAnimatableShape == null || itemSlot.Itemstack == null || mModSystem?.AnimatedItemShaderProgram == null) return;

        if (mOnlyWhenAnimating && !RenderProceduralAnimations)
        {
            mClientApi?.Render.RenderMultiTextureMesh(renderInfo.ModelRef);
            return;
        }

        ItemRenderInfo? itemStackRenderInfo = PrepareShape(api, mItemModelMat, modelMat, itemSlot, entity, right, dt);

        if (itemStackRenderInfo == null) return;

        if (isShadowPass)
        {
            ShadowPass(api, itemStackRenderInfo, mItemModelMat, CurrentAnimatableShape);
        }
        else
        {
            IShaderProgram? shader = mCurrentFirstPerson ? mModSystem.AnimatedItemShaderProgramFirstPerson : mModSystem.AnimatedItemShaderProgram;

            if (shader == null)
            {
                mClientApi?.Logger.Debug("[Animation manager] Shader is null");
                return;
            }

            RenderShape(shader, api.World, CurrentAnimatableShape, itemStackRenderInfo, api.Render, itemSlot.Itemstack, lightrgbs, mItemModelMat, itemSlot, entity, dt);
        }
    }

    protected virtual void CalculateAnimation(AnimatorBase? animator, ICoreClientAPI clientApi, Entity entity, EnumItemRenderTarget target, float dt)
    {
        if (
            animator != null &&
            !clientApi.IsGamePaused &&
            target == EnumItemRenderTarget.HandFp &&
            (
                mActiveAnimationsByCode.Count > 0 ||
                animator.ActiveAnimationCount > 0 ||
                RenderProceduralAnimations ||
                !mOnlyWhenAnimating
            )
        )
        {
            if (RenderProceduralAnimations) mModSystem?.OnBeforeRender(animator, entity, dt);
            animator.OnFrame(mActiveAnimationsByCode, dt);
        }
    }

    protected static bool IsFirstPerson(Entity entity)
    {
        return AnimationTarget.GetEntityTargetType(entity) == AnimationTargetType.EntityFirstPerson || AnimationTarget.GetEntityTargetType(entity) == AnimationTargetType.EntityImmersiveFirstPerson;
    }

    protected static ItemRenderInfo? PrepareShape(ICoreClientAPI api, Matrixf itemModelMat, float[] modelMat, ItemSlot itemSlot, Entity entity, bool right, float dt)
    {
        ItemStack? itemStack = itemSlot?.Itemstack;
        if (itemStack == null)
        {
            return null;
        }

        AttachmentPointAndPose? attachmentPointAndPose = entity.AnimManager?.Animator?.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
        if (attachmentPointAndPose == null)
        {
            return null;
        }

        AttachmentPoint attachPoint = attachmentPointAndPose.AttachPoint;
        ItemRenderInfo itemStackRenderInfo = api.Render.GetItemStackRenderInfo(itemSlot, right ? EnumItemRenderTarget.HandTp : EnumItemRenderTarget.HandTpOff, dt);
        if (itemStackRenderInfo?.Transform == null)
        {
            return null;
        }

        itemModelMat.Set(modelMat).Mul(attachmentPointAndPose.AnimModelMatrix).Translate(itemStackRenderInfo.Transform.Origin.X, itemStackRenderInfo.Transform.Origin.Y, itemStackRenderInfo.Transform.Origin.Z)
            .Scale(itemStackRenderInfo.Transform.ScaleXYZ.X, itemStackRenderInfo.Transform.ScaleXYZ.Y, itemStackRenderInfo.Transform.ScaleXYZ.Z)
            .Translate(attachPoint.PosX / 16.0 + itemStackRenderInfo.Transform.Translation.X, attachPoint.PosY / 16.0 + itemStackRenderInfo.Transform.Translation.Y, attachPoint.PosZ / 16.0 + itemStackRenderInfo.Transform.Translation.Z)
            .RotateX((float)(attachPoint.RotationX + itemStackRenderInfo.Transform.Rotation.X) * (MathF.PI / 180f))
            .RotateY((float)(attachPoint.RotationY + itemStackRenderInfo.Transform.Rotation.Y) * (MathF.PI / 180f))
            .RotateZ((float)(attachPoint.RotationZ + itemStackRenderInfo.Transform.Rotation.Z) * (MathF.PI / 180f))
            .Translate(0f - itemStackRenderInfo.Transform.Origin.X, 0f - itemStackRenderInfo.Transform.Origin.Y, 0f - itemStackRenderInfo.Transform.Origin.Z);

        return itemStackRenderInfo;
    }

    protected static void ShadowPass(ICoreClientAPI api, ItemRenderInfo itemStackRenderInfo, Matrixf itemModelMat, AnimatableShape shape)
    {
        IRenderAPI render = api.Render;

        string textureSampleName = "tex2d";
        render.CurrentActiveShader.BindTexture2D("tex2d", itemStackRenderInfo.TextureId, 0);
        float[] array = Mat4f.Mul(itemModelMat.Values, api.Render.CurrentModelviewMatrix, itemModelMat.Values);
        Mat4f.Mul(array, api.Render.CurrentProjectionMatrix, array);
        api.Render.CurrentActiveShader.UniformMatrix("mvpMatrix", array);
        api.Render.CurrentActiveShader.Uniform("origin", new Vec3f());

        if (!itemStackRenderInfo.CullFaces)
        {
            render.GlDisableCullFace();
        }

        render.RenderMultiTextureMesh(shape.MeshRef, textureSampleName);
        if (!itemStackRenderInfo.CullFaces)
        {
            render.GlEnableCullFace();
        }
    }
}
