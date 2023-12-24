using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;
using System.Diagnostics;
using Vintagestory.API.Common.Entities;

#pragma warning disable CS8602
namespace AnimationManagerLib.CollectibleBehaviors
{
    public class Animatable : CollectibleBehavior, ITexPositionSource // Based on code from TeacupAngel (https://github.com/TeacupAngel)
    {
        public AnimatorBase? Animator { get; set; }
        public Dictionary<string, AnimationMetaData> ActiveAnimationsByAnimCode { get; set; } = new Dictionary<string, AnimationMetaData>();

        // ITexPositionSource
        ITextureAtlasAPI? curAtlas;
        public Size2i? AtlasSize => curAtlas?.Size;

        public virtual TextureAtlasPosition? this[string textureCode]
        {
            get
            {
                AssetLocation? texturePath = null;
                CurrentShape?.Textures.TryGetValue(textureCode, out texturePath);

                if (texturePath == null)
                {
                    texturePath = new AssetLocation(textureCode);
                }

                return GetOrCreateTexPos(texturePath);
            }
        }

        protected TextureAtlasPosition? GetOrCreateTexPos(AssetLocation texturePath)
        {

            TextureAtlasPosition texpos = curAtlas[texturePath];

            if (texpos == null)
            {
                IAsset texAsset = mClientApi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (texAsset != null)
                {
                    curAtlas.GetOrInsertTexture(texturePath, out _, out texpos);
                }
                else
                {
                    mClientApi.World.Logger.Warning("Bullseye.CollectibleBehaviorAnimatable: Item {0} defined texture {1}, not no such texture found.", collObj.Code, texturePath);
                }
            }

            return texpos;
        }

        public Animatable(CollectibleObject collObj) : base(collObj)
        {
        }

        protected string CacheKey => "animatedCollectibleMeshes-" + collObj.Code.ToShortString();
        protected AnimationManagerLibSystem? modSystem;

        public Shape? CurrentShape { get; private set; }
        public bool RenderProceduralAnimations { get; set; }

        protected ICoreClientAPI? mClientApi;
        protected MultiTextureMeshRef? currentMeshRef;
        protected string? animatedShapePath;
        protected bool onlyWhenAnimating;
        protected float[] tmpMvMat = Mat4f.Create();

        public override void Initialize(JsonObject properties)
        {
            animatedShapePath = properties["animatedShape"].AsString(null);
            onlyWhenAnimating = properties["onlyWhenAnimating"].AsBool(true);

            base.Initialize(properties);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            modSystem = api.ModLoader.GetModSystem<AnimationManagerLibSystem>();

            if (api is ICoreClientAPI clientApi)
            {
                if (collObj is not Item)
                {
                    throw new InvalidOperationException("CollectibleBehaviorAnimatable can only be used on Items, not Blocks!");
                }

                mClientApi = clientApi;

                InitAnimatable();

                if (Animator == null || currentMeshRef == null) return;
            }
        }

        public virtual void InitAnimatable()
        {
            Item? item = (collObj as Item);

            curAtlas = mClientApi.ItemTextureAtlas;

            AssetLocation? loc = animatedShapePath != null ? new AssetLocation(animatedShapePath) : item?.Shape.Base.Clone();
            Debug.Assert(loc != null);
            loc = loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            CurrentShape = Shape.TryGet(mClientApi, loc);

            if (CurrentShape == null) return;

            MeshData meshData = InitializeMeshData(CacheKey, CurrentShape, this);
            currentMeshRef = InitializeMeshRef(meshData);

            Animator = GetAnimator(mClientApi, CacheKey, CurrentShape);
        }

        public MeshData InitializeMeshData(string cacheDictKey, Shape shape, ITexPositionSource texSource)
        {
            if (mClientApi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented yet.");

            shape.ResolveReferences(mClientApi.World.Logger, cacheDictKey);
            CacheInvTransforms(shape.Elements);
            shape.ResolveAndLoadJoints();

            mClientApi.Tesselator.TesselateShapeWithJointIds("collectible", shape, out MeshData meshdata, texSource, null);

            return meshdata;
        }

        public MultiTextureMeshRef InitializeMeshRef(MeshData meshdata)
        {
            MultiTextureMeshRef? meshRef = null;

            if (RuntimeEnv.MainThreadId == Environment.CurrentManagedThreadId)
            {
                meshRef = mClientApi.Render.UploadMultiTextureMesh(meshdata);
            }
            else
            {
                mClientApi.Event.EnqueueMainThreadTask(() => {
                    meshRef = mClientApi.Render.UploadMultiTextureMesh(meshdata);
                }, "uploadmesh");
            }

            Debug.Assert(meshRef != null);
            return meshRef;
        }

        public static AnimatorBase? GetAnimator(ICoreClientAPI capi, string cacheDictKey, Shape blockShape)
        {
            if (blockShape == null)
            {
                return null;
            }

            Dictionary<string, AnimCacheEntry>? animCache;
            capi.ObjectCache.TryGetValue("coAnimCache", out object? animCacheObj);
            animCache = animCacheObj as Dictionary<string, AnimCacheEntry>;
            if (animCache == null)
            {
                capi.ObjectCache["coAnimCache"] = animCache = new Dictionary<string, AnimCacheEntry>();
            }

            AnimatorBase animator;

            if (animCache.TryGetValue(cacheDictKey, out AnimCacheEntry? cacheObj))
            {
                animator = capi.Side == EnumAppSide.Client ?
                    new ClientAnimator(() => 1, cacheObj.RootPoses, cacheObj.Animations, cacheObj.RootElems, blockShape.JointsById) :
                    new ServerAnimator(() => 1, cacheObj.RootPoses, cacheObj.Animations, cacheObj.RootElems, blockShape.JointsById)
                ;
            }
            else
            {
                for (int i = 0; blockShape.Animations != null && i < blockShape.Animations.Length; i++)
                {
                    blockShape.Animations[i].GenerateAllFrames(blockShape.Elements, blockShape.JointsById);
                }

                animator = capi.Side == EnumAppSide.Client ?
                    new ClientAnimator(() => 1, blockShape.Animations, blockShape.Elements, blockShape.JointsById) :
                    new ServerAnimator(() => 1, blockShape.Animations, blockShape.Elements, blockShape.JointsById)
                ;

                animCache[cacheDictKey] = new AnimCacheEntry()
                {
                    Animations = blockShape.Animations,
                    RootElems = (animator as ClientAnimator)?.rootElements,
                    RootPoses = (animator as ClientAnimator)?.RootPoses
                };
            }

            return animator;
        }

        public static void CacheInvTransforms(ShapeElement[] elements)
        {
            if (elements == null) return;

            for (int i = 0; i < elements.Length; i++)
            {
                elements[i].CacheInverseTransformMatrix();
                CacheInvTransforms(elements[i].Children);
            }
        }

        public void StartAnimation(AnimationMetaData metaData)
        {
            if (mClientApi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented.");

            if (!ActiveAnimationsByAnimCode.ContainsKey(metaData.Code))
            {
                ActiveAnimationsByAnimCode[metaData.Code] = metaData;
            }
        }

        public void StopAnimation(string code, bool forceImmediate = false)
        {
            if (mClientApi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented.");
            if (Animator == null) return;
            
            if (ActiveAnimationsByAnimCode.ContainsKey(code) && forceImmediate)
            {
                RunningAnimation? anim = Array.Find(Animator.anims, (anim) => { return anim.Animation.Code == code; });
                Debug.Assert(anim != null);
                anim.EasingFactor = 0f;
            }

            ActiveAnimationsByAnimCode.Remove(code);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (RenderProceduralAnimations) modSystem.OnBeforeRender(Animator, renderinfo.dt);
            Animator.OnFrame(ActiveAnimationsByAnimCode, renderinfo.dt);

            /*if (Animator == null || capi.IsGamePaused || target != EnumItemRenderTarget.HandTp) return;

            if (true || ActiveAnimationsByAnimCode.Count > 0 || Animator.ActiveAnimationCount > 0 || RenderProceduralAnimations)
            {
                if (RenderProceduralAnimations) modSystem.OnBeforeRender(Animator, renderinfo.dt);
                Animator.OnFrame(ActiveAnimationsByAnimCode, renderinfo.dt);
            }*/
        }

        public Matrixf ItemModelMat = new();
        public float accum = 0;
        public void RenderHeldItem(float[] ModelMat, ICoreClientAPI capi, ItemSlot itemSlot, Entity entity, Vec4f lightrgbs, float dt, bool isShadowPass, bool right)
        {
            ItemStack itemStack = itemSlot?.Itemstack;
            if (itemStack == null)
            {
                return;
            }

            AttachmentPointAndPose attachmentPointAndPose = entity.AnimManager?.Animator?.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
            if (attachmentPointAndPose == null)
            {
                return;
            }

            IRenderAPI render = capi.Render;
            AttachmentPoint attachPoint = attachmentPointAndPose.AttachPoint;
            ItemRenderInfo itemStackRenderInfo = render.GetItemStackRenderInfo(itemSlot, right ? EnumItemRenderTarget.HandTp : EnumItemRenderTarget.HandTpOff, dt);
            IShaderProgram shaderProgram = null;
            if (itemStackRenderInfo?.Transform == null)
            {
                return;
            }

            ItemModelMat.Set(ModelMat).Mul(attachmentPointAndPose.AnimModelMatrix).Translate(itemStackRenderInfo.Transform.Origin.X, itemStackRenderInfo.Transform.Origin.Y, itemStackRenderInfo.Transform.Origin.Z)
                .Scale(itemStackRenderInfo.Transform.ScaleXYZ.X, itemStackRenderInfo.Transform.ScaleXYZ.Y, itemStackRenderInfo.Transform.ScaleXYZ.Z)
                .Translate(attachPoint.PosX / 16.0 + (double)itemStackRenderInfo.Transform.Translation.X, attachPoint.PosY / 16.0 + (double)itemStackRenderInfo.Transform.Translation.Y, attachPoint.PosZ / 16.0 + (double)itemStackRenderInfo.Transform.Translation.Z)
                .RotateX((float)(attachPoint.RotationX + (double)itemStackRenderInfo.Transform.Rotation.X) * (MathF.PI / 180f))
                .RotateY((float)(attachPoint.RotationY + (double)itemStackRenderInfo.Transform.Rotation.Y) * (MathF.PI / 180f))
                .RotateZ((float)(attachPoint.RotationZ + (double)itemStackRenderInfo.Transform.Rotation.Z) * (MathF.PI / 180f))
                .Translate(0f - itemStackRenderInfo.Transform.Origin.X, 0f - itemStackRenderInfo.Transform.Origin.Y, 0f - itemStackRenderInfo.Transform.Origin.Z);
            
            string textureSampleName = "tex";
            if (isShadowPass)
            {
                textureSampleName = "tex2d";
                render.CurrentActiveShader.BindTexture2D("tex2d", itemStackRenderInfo.TextureId, 0);
                float[] array = Mat4f.Mul(ItemModelMat.Values, capi.Render.CurrentModelviewMatrix, ItemModelMat.Values);
                Mat4f.Mul(array, capi.Render.CurrentProjectionMatrix, array);
                capi.Render.CurrentActiveShader.UniformMatrix("mvpMatrix", array);
                capi.Render.CurrentActiveShader.Uniform("origin", new Vec3f());
            }
            else
            {
                shaderProgram = modSystem.AnimatedItemShaderProgram;
                shaderProgram.Use();

                FillShaderValues(shaderProgram, itemStackRenderInfo, render, itemStack);
            }

            if (!itemStackRenderInfo.CullFaces)
            {
                render.GlDisableCullFace();
            }

            //render.RenderMesh(currentMeshRef);
            render.RenderMultiTextureMesh(currentMeshRef, textureSampleName);
            if (!itemStackRenderInfo.CullFaces)
            {
                render.GlEnableCullFace();
            }

            if (isShadowPass)
            {
                return;
            }

            //shaderProgram.Uniform("damageEffect", 0f);
            shaderProgram.Stop();
            float num3 = Math.Max(0f, 1f - (float)capi.World.BlockAccessor.GetDistanceToRainFall(entity.Pos.AsBlockPos) / 5f);
            AdvancedParticleProperties[] array2 = itemStack.Collectible?.ParticleProperties;
            if (itemStack.Collectible == null || capi.IsGamePaused)
            {
                return;
            }

            Vec4f vec4f = ItemModelMat.TransformVector(new Vec4f(itemStack.Collectible.TopMiddlePos.X, itemStack.Collectible.TopMiddlePos.Y, itemStack.Collectible.TopMiddlePos.Z, 1f));
            EntityPlayer entityPlayer = capi.World.Player.Entity;
            accum += dt;
            if (array2 != null && array2.Length != 0 && accum > 0.05f)
            {
                accum %= 0.025f;
                foreach (AdvancedParticleProperties advancedParticleProperties in array2)
                {
                    advancedParticleProperties.WindAffectednesAtPos = num3;
                    advancedParticleProperties.WindAffectednes = num3;
                    advancedParticleProperties.basePos.X = (double)vec4f.X + entity.Pos.X + (0.0 - (entity.Pos.X - entityPlayer.CameraPos.X));
                    advancedParticleProperties.basePos.Y = (double)vec4f.Y + entity.Pos.Y + (0.0 - (entity.Pos.Y - entityPlayer.CameraPos.Y));
                    advancedParticleProperties.basePos.Z = (double)vec4f.Z + entity.Pos.Z + (0.0 - (entity.Pos.Z - entityPlayer.CameraPos.Z));
                    entity.World.SpawnParticles(advancedParticleProperties);
                }
            }
        }

        public void FillShaderValues(IShaderProgram shaderProgram, ItemRenderInfo itemStackRenderInfo, IRenderAPI render, ItemStack itemStack)
        {
            Vec4f lightRGBSVec4f = mClientApi.World.BlockAccessor.GetLightRGBs((int)(mClientApi.World.Player.Entity.Pos.X + mClientApi.World.Player.Entity.LocalEyePos.X), (int)(mClientApi.World.Player.Entity.Pos.Y + mClientApi.World.Player.Entity.LocalEyePos.Y), (int)(mClientApi.World.Player.Entity.Pos.Z + mClientApi.World.Player.Entity.LocalEyePos.Z));

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

            int num = (int)itemStack.Collectible.GetTemperature(mClientApi.World, itemStack);
            float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num);
            int num2 = GameMath.Clamp((num - 500) / 3, 0, 255);
            shaderProgram.Uniform("extraGlow", num2);
            shaderProgram.Uniform("rgbaAmbientIn", render.AmbientColor);
            shaderProgram.Uniform("rgbaLightIn", lightRGBSVec4f);
            shaderProgram.Uniform("rgbaGlowIn", new Vec4f(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], (float)num2 / 255f));
            shaderProgram.Uniform("rgbaFogIn", render.FogColor);
            shaderProgram.Uniform("fogMinIn", render.FogMin);
            shaderProgram.Uniform("fogDensityIn", render.FogDensity);
            shaderProgram.Uniform("normalShaded", itemStackRenderInfo.NormalShaded ? 1 : 0);
            shaderProgram.UniformMatrix("projectionMatrix", render.CurrentProjectionMatrix);
            shaderProgram.UniformMatrix("viewMatrix", render.CameraMatrixOriginf);
            shaderProgram.UniformMatrix("modelMatrix", ItemModelMat.Values);

            shaderProgram.UniformMatrices4x3(
                "elementTransforms",
                GlobalConstants.MaxAnimatedElements,
                Animator?.TransformationMatrices4x3
            );
        }

        public void FillCustomShaderValues(IShaderProgram shaderProgram, ItemRenderInfo itemStackRenderInfo, IRenderAPI render, ItemStack itemStack)
        {
            Vec4f lightRGBSVec4f = mClientApi.World.BlockAccessor.GetLightRGBs((int)(mClientApi.World.Player.Entity.Pos.X + mClientApi.World.Player.Entity.LocalEyePos.X), (int)(mClientApi.World.Player.Entity.Pos.Y + mClientApi.World.Player.Entity.LocalEyePos.Y), (int)(mClientApi.World.Player.Entity.Pos.Z + mClientApi.World.Player.Entity.LocalEyePos.Z));

            shaderProgram.Uniform("alphaTest", collObj.RenderAlphaTest);
            
            shaderProgram.Uniform("normalShaded", itemStackRenderInfo.NormalShaded ? 1 : 0);
            shaderProgram.Uniform("overlayOpacity", itemStackRenderInfo.OverlayOpacity);
            if (itemStackRenderInfo.OverlayTexture != null && itemStackRenderInfo.OverlayOpacity > 0f)
            {
                shaderProgram.Uniform("tex2dOverlay", itemStackRenderInfo.OverlayTexture.TextureId);
                shaderProgram.Uniform("overlayTextureSize", new Vec2f(itemStackRenderInfo.OverlayTexture.Width, itemStackRenderInfo.OverlayTexture.Height));
                shaderProgram.Uniform("baseTextureSize", new Vec2f(itemStackRenderInfo.TextureSize.Width, itemStackRenderInfo.TextureSize.Height));
                TextureAtlasPosition textureAtlasPosition = mClientApi.Render.GetTextureAtlasPosition(itemStack);
                shaderProgram.Uniform("baseUvOrigin", new Vec2f(textureAtlasPosition.x1, textureAtlasPosition.y1));
            }

            int num = (int)itemStack.Collectible.GetTemperature(mClientApi.World, itemStack);
            float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num);
            int num2 = GameMath.Clamp((num - 500) / 3, 0, 255);
            Vec4f rgbaGlowIn = new Vec4f(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], (float)num2 / 255f);
            shaderProgram.Uniform("extraGlow", num2);
            shaderProgram.Uniform("rgbaAmbientIn", mClientApi.Ambient.BlendedAmbientColor);
            shaderProgram.Uniform("rgbaLightIn", lightRGBSVec4f);
            shaderProgram.Uniform("rgbaGlowIn", rgbaGlowIn);
            shaderProgram.Uniform("lightPosition", GetLightPosition(ItemModelMat.Values));
            shaderProgram.UniformMatrix("toShadowMapSpaceMatrixFar", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixFar);
            shaderProgram.UniformMatrix("toShadowMapSpaceMatrixNear", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixNear);
            shaderProgram.BindTexture2D("itemTex", itemStackRenderInfo.TextureId, 0);

            Matrixf modelViewMat = new();
            List<float> values = new();
            foreach (double item in render.CameraMatrixOrigin)
            {
                values.Add((float)item);
            }
            modelViewMat.Values = values.ToArray();
            modelViewMat.Mul(ItemModelMat.Values);

            shaderProgram.UniformMatrix("projectionMatrix", mClientApi.Render.CurrentProjectionMatrix);
            shaderProgram.UniformMatrix("modelViewMatrix", modelViewMat.Values);
        }

        public Vec3f GetLightPosition(float[] modelMat)
        {
            float[] tmpVals = new float[4];
            Vec4f outPos = new();
            float[] array = Mat4f.Create();
            Mat4f.RotateY(array, array, mClientApi.World.Player.Entity.SidedPos.Yaw);
            Mat4f.RotateX(array, array, mClientApi.World.Player.Entity.SidedPos.Pitch);
            Mat4f.Mul(array, array, modelMat);
            tmpVals[0] = mClientApi.Render.ShaderUniforms.LightPosition3D.X;
            tmpVals[1] = mClientApi.Render.ShaderUniforms.LightPosition3D.Y;
            tmpVals[2] = mClientApi.Render.ShaderUniforms.LightPosition3D.Z;
            tmpVals[3] = 0f;
            Mat4f.MulWithVec4(array, tmpVals, outPos);
            return new Vec3f(outPos.X, outPos.Y, outPos.Z).Normalize();
        }

        public virtual void RenderHandFp(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat)
        {
            IShaderProgram prevProg = mClientApi.Render.CurrentActiveShader;
            IShaderProgram prog;

            prevProg?.Stop();
            Debug.Assert(modSystem.AnimatedItemShaderProgram != null);

            Vec4f lightRGBSVec4f = mClientApi.World.BlockAccessor.GetLightRGBs((int)(mClientApi.World.Player.Entity.Pos.X + mClientApi.World.Player.Entity.LocalEyePos.X), (int)(mClientApi.World.Player.Entity.Pos.Y + mClientApi.World.Player.Entity.LocalEyePos.Y), (int)(mClientApi.World.Player.Entity.Pos.Z + mClientApi.World.Player.Entity.LocalEyePos.Z));
            int num16 = (int)inSlot.Itemstack.Collectible.GetTemperature(mClientApi.World, inSlot.Itemstack);
            float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num16);
            int num17 = GameMath.Clamp((num16 - 550) / 2, 0, 255);
            Vec4f rgbaGlowIn = new(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], (float)num17 / 255f);
            float[] tmpVals = new float[4];
            Vec4f outPos = new();
            float[] array = Mat4f.Create();
            Mat4f.RotateY(array, array, mClientApi.World.Player.Entity.SidedPos.Yaw);
            Mat4f.RotateX(array, array, mClientApi.World.Player.Entity.SidedPos.Pitch);
            Mat4f.Mul(array, array, modelMat.Values);
            tmpVals[0] = mClientApi.Render.ShaderUniforms.LightPosition3D.X;
            tmpVals[1] = mClientApi.Render.ShaderUniforms.LightPosition3D.Y;
            tmpVals[2] = mClientApi.Render.ShaderUniforms.LightPosition3D.Z;
            tmpVals[3] = 0f;
            Mat4f.MulWithVec4(array, tmpVals, outPos);
            Vec3f lightPosition = new Vec3f(outPos.X, outPos.Y, outPos.Z).Normalize();

            prog = modSystem.AnimatedItemShaderProgram;
            prog.Use();
            prog.Uniform("alphaTest", collObj.RenderAlphaTest);
            prog.UniformMatrix("modelViewMatrix", modelMat.Values);
            prog.Uniform("normalShaded", renderInfo.NormalShaded ? 1 : 0);
            prog.Uniform("overlayOpacity", renderInfo.OverlayOpacity);
            if (renderInfo.OverlayTexture != null && renderInfo.OverlayOpacity > 0f)
            {
                prog.Uniform("tex2dOverlay", renderInfo.OverlayTexture.TextureId);
                prog.Uniform("overlayTextureSize", new Vec2f(renderInfo.OverlayTexture.Width, renderInfo.OverlayTexture.Height));
                prog.Uniform("baseTextureSize", new Vec2f(renderInfo.TextureSize.Width, renderInfo.TextureSize.Height));
                TextureAtlasPosition textureAtlasPosition = mClientApi.Render.GetTextureAtlasPosition(inSlot.Itemstack);
                prog.Uniform("baseUvOrigin", new Vec2f(textureAtlasPosition.x1, textureAtlasPosition.y1));
            }
            prog.Uniform("extraGlow", num17);
            prog.Uniform("rgbaAmbientIn", mClientApi.Ambient.BlendedAmbientColor);
            prog.Uniform("rgbaLightIn", lightRGBSVec4f);
            prog.Uniform("rgbaGlowIn", rgbaGlowIn);
            prog.Uniform("lightPosition", lightPosition);
            prog.UniformMatrix("toShadowMapSpaceMatrixFar", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixFar);
            prog.UniformMatrix("toShadowMapSpaceMatrixNear", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixNear);
            prog.BindTexture2D("itemTex", renderInfo.TextureId, 0);
            prog.UniformMatrix("projectionMatrix", mClientApi.Render.CurrentProjectionMatrix);

            prog.UniformMatrices4x3(
                "elementTransforms",
                GlobalConstants.MaxAnimatedElements,
                Animator?.TransformationMatrices4x3
            );

            //mClientApi.Render.RenderMesh(currentMeshRef);

            prog.Stop();
            prevProg?.Use();
        }
    }
#pragma warning restore CS8602
}
