using AnimationManagerLib.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;


namespace AnimationManagerLib.CollectibleBehaviors;

public class Animatable : CollectibleBehavior // Based on code from TeacupAngel (https://github.com/TeacupAngel)
{
    public bool RenderProceduralAnimations { get; set; }
    public Shape? CurrentShape => CurrentAnimatableShape?.Shape;
    public AnimatableShape? CurrentAnimatableShape => (mCurrentFirstPerson ? mShapeFirstPerson : mShape) ?? mShape ?? mShapeFirstPerson;

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
        mAnimatedShapePath = properties["animated-shape-fp"].AsString(null);
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

        mShape = AnimatableShape.Create(mClientApi, mAnimatedShapePath ?? item.Shape.Base.ToString() ?? mAnimatedShapeFirstPersonPath ?? "");
        mShapeFirstPerson = AnimatableShape.Create(mClientApi, mAnimatedShapeFirstPersonPath ?? item.Shape.Base.ToString() ?? mAnimatedShapePath ?? "");
    }

    public void StartAnimation(AnimationMetaData metaData)
    {
        if (mClientApi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented.");

        if (!mActiveAnimationsByCode.ContainsKey(metaData.Code))
        {
            mActiveAnimationsByCode[metaData.Code] = metaData;
        }
    }

    public void StopAnimation(string code, bool forceImmediate = false)
    {
        if (mClientApi == null) return;
        if (mClientApi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented.");
        if (mShape == null) return;

        if (mActiveAnimationsByCode.ContainsKey(code) && forceImmediate)
        {
            RunningAnimation? animation = Array.Find(mShape.Animator.anims, (animation) => { return animation.Animation.Code == code; });
            Debug.Assert(animation != null);
            animation.EasingFactor = 0f;
        }

        mActiveAnimationsByCode.Remove(code);
    }

    public virtual void BeforeRender(ICoreClientAPI clientApi, ItemStack itemStack, EnumItemRenderTarget target, ref ItemRenderInfo renderInfo)
    {
        CalculateAnimation(mShape?.Animator, clientApi, target, ref renderInfo);
        CalculateAnimation(mShapeFirstPerson?.Animator, clientApi, target, ref renderInfo);
    }

    public void RenderHeldItem(float[] modelMat, ICoreClientAPI api, ItemSlot itemSlot, Entity entity, Vec4f lightrgbs, float dt, bool isShadowPass, bool right)
    {
        mCurrentFirstPerson = IsFirstPerson(entity);

        if (CurrentAnimatableShape == null || itemSlot.Itemstack == null || mModSystem?.AnimatedItemShaderProgram == null) return;

        ItemRenderInfo? itemStackRenderInfo = PrepareShape(api, mItemModelMat, modelMat, itemSlot, entity, right, dt);

        if (itemStackRenderInfo == null) return;

        if (isShadowPass)
        {
            ShadowPass(api, itemStackRenderInfo, mItemModelMat, CurrentAnimatableShape);
        }
        else
        {
            RenderShape(mModSystem.AnimatedItemShaderProgram, api.World, CurrentAnimatableShape, itemStackRenderInfo, api.Render, itemSlot.Itemstack, lightrgbs, mItemModelMat);
            SpawnParticles(mItemModelMat, itemSlot.Itemstack, dt, ref mTimeAccumulation, api, entity);
        }
    }

    protected virtual void CalculateAnimation(AnimatorBase? animator, ICoreClientAPI clientApi, EnumItemRenderTarget target, ref ItemRenderInfo renderInfo)
    {
        if (
            animator != null &&
            !clientApi.IsGamePaused &&
            target == EnumItemRenderTarget.HandTp &&
            (
                mActiveAnimationsByCode.Count > 0 ||
                animator.ActiveAnimationCount > 0 ||
                RenderProceduralAnimations ||
                !mOnlyWhenAnimating
            )
        )
        {
            if (RenderProceduralAnimations) mModSystem?.OnBeforeRender(animator, renderInfo.dt);
            animator.OnFrame(mActiveAnimationsByCode, renderInfo.dt);
        }
    }

    protected static bool IsFirstPerson(Entity entity)
    {
        return AnimationTarget.GetTargetType(entity) == AnimationTargetType.EntityFirstPerson || AnimationTarget.GetTargetType(entity) == AnimationTargetType.EntityImmersiveFirstPerson;
    }

    protected static ItemRenderInfo? PrepareShape(ICoreClientAPI api, Matrixf itemModelMat, float[] ModelMat, ItemSlot itemSlot, Entity entity, bool right, float dt)
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

        itemModelMat.Set(ModelMat).Mul(attachmentPointAndPose.AnimModelMatrix).Translate(itemStackRenderInfo.Transform.Origin.X, itemStackRenderInfo.Transform.Origin.Y, itemStackRenderInfo.Transform.Origin.Z)
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

    protected static void RenderShape(IShaderProgram shaderProgram, IWorldAccessor world, AnimatableShape shape, ItemRenderInfo itemStackRenderInfo, IRenderAPI render, ItemStack itemStack, Vec4f lightrgbs, Matrixf itemModelMat)
    {
        string textureSampleName = "tex";

        shaderProgram.Use();
        FillShaderValues(shaderProgram, itemStackRenderInfo, render, itemStack, lightrgbs, itemModelMat, world, shape.Animator);

        if (!itemStackRenderInfo.CullFaces)
        {
            render.GlDisableCullFace();
        }

        render.RenderMultiTextureMesh(shape.MeshRef, textureSampleName);
        if (!itemStackRenderInfo.CullFaces)
        {
            render.GlEnableCullFace();
        }

        shaderProgram.Uniform("damageEffect", 0f);
        shaderProgram.Stop();
    }

    protected static void SpawnParticles(Matrixf itemModelMat, ItemStack itemStack, float dt, ref float timeAccumulation, ICoreClientAPI api, Entity entity)
    {
        if (itemStack.Collectible?.ParticleProperties == null) return;

        float windStrength = Math.Max(0f, 1f - api.World.BlockAccessor.GetDistanceToRainFall(entity.Pos.AsBlockPos) / 5f);
        AdvancedParticleProperties[] particleProperties = itemStack.Collectible.ParticleProperties;
        if (itemStack.Collectible == null || api.IsGamePaused)
        {
            return;
        }

        EntityPlayer entityPlayer = api.World.Player.Entity;

        Vec4f vec4f = itemModelMat.TransformVector(new Vec4f(itemStack.Collectible.TopMiddlePos.X, itemStack.Collectible.TopMiddlePos.Y, itemStack.Collectible.TopMiddlePos.Z, 1f));
        timeAccumulation += dt;
        if (particleProperties != null && particleProperties.Length != 0 && timeAccumulation > 0.05f)
        {
            timeAccumulation %= 0.025f;
            foreach (AdvancedParticleProperties advancedParticleProperties in particleProperties)
            {
                advancedParticleProperties.WindAffectednesAtPos = windStrength;
                advancedParticleProperties.WindAffectednes = windStrength;
                advancedParticleProperties.basePos.X = vec4f.X + entity.Pos.X + (0.0 - (entity.Pos.X - entityPlayer.CameraPos.X));
                advancedParticleProperties.basePos.Y = vec4f.Y + entity.Pos.Y + (0.0 - (entity.Pos.Y - entityPlayer.CameraPos.Y));
                advancedParticleProperties.basePos.Z = vec4f.Z + entity.Pos.Z + (0.0 - (entity.Pos.Z - entityPlayer.CameraPos.Z));
                entity.World.SpawnParticles(advancedParticleProperties);
            }
        }
    }

    protected static void FillShaderValues(IShaderProgram shaderProgram, ItemRenderInfo itemStackRenderInfo, IRenderAPI render, ItemStack itemStack, Vec4f lightrgbs, Matrixf itemModelMatrix, IWorldAccessor world, AnimatorBase animator)
    {
        shaderProgram.Uniform("dontWarpVertices", 0);
        shaderProgram.Uniform("addRenderFlags", 0);
        shaderProgram.Uniform("normalShaded", 1);
        shaderProgram.Uniform("rgbaTint", ColorUtil.WhiteArgbVec);
        shaderProgram.Uniform("alphaTest", itemStackRenderInfo.AlphaTest);
        shaderProgram.Uniform("damageEffect", itemStackRenderInfo.DamageEffect);
        shaderProgram.Uniform("overlayOpacity", itemStackRenderInfo.OverlayOpacity);
        if (itemStackRenderInfo.OverlayTexture != null && itemStackRenderInfo.OverlayOpacity > 0f)
        {
            shaderProgram.Uniform("tex2dOverlay", itemStackRenderInfo.OverlayTexture.TextureId);
            shaderProgram.Uniform("overlayTextureSize", new Vec2f(itemStackRenderInfo.OverlayTexture.Width, itemStackRenderInfo.OverlayTexture.Height));
            shaderProgram.Uniform("baseTextureSize", new Vec2f(itemStackRenderInfo.TextureSize.Width, itemStackRenderInfo.TextureSize.Height));
            TextureAtlasPosition textureAtlasPosition = render.GetTextureAtlasPosition(itemStack);
            shaderProgram.Uniform("baseUvOrigin", new Vec2f(textureAtlasPosition.x1, textureAtlasPosition.y1));
        }

        int num = (int)itemStack.Collectible.GetTemperature(world, itemStack);
        float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num);
        int num2 = GameMath.Clamp((num - 500) / 3, 0, 255);
        shaderProgram.Uniform("extraGlow", num2);
        shaderProgram.Uniform("rgbaAmbientIn", render.AmbientColor);
        shaderProgram.Uniform("rgbaLightIn", lightrgbs);
        shaderProgram.Uniform("rgbaGlowIn", new Vec4f(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], num2 / 255f));
        shaderProgram.Uniform("rgbaFogIn", render.FogColor);
        shaderProgram.Uniform("fogMinIn", render.FogMin);
        shaderProgram.Uniform("fogDensityIn", render.FogDensity);
        shaderProgram.Uniform("normalShaded", itemStackRenderInfo.NormalShaded ? 1 : 0);
        shaderProgram.UniformMatrix("projectionMatrix", render.CurrentProjectionMatrix);
        shaderProgram.UniformMatrix("viewMatrix", render.CameraMatrixOriginf);
        shaderProgram.UniformMatrix("modelMatrix", itemModelMatrix.Values);

        shaderProgram.UniformMatrices4x3(
            "elementTransforms",
            GlobalConstants.MaxAnimatedElements,
            animator?.TransformationMatrices4x3
        );
    }
}
