using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;
using System.Diagnostics;

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
        protected MeshRef? currentMeshRef;
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

                // Don't bother registering a renderer if we can't get the proper data. Should hopefully save us from some crashes
                if (Animator == null || currentMeshRef == null) return;

                mClientApi.Event.RegisterItemstackRenderer(collObj, (inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize) => RenderHandFp(inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize), EnumItemRenderTarget.HandFp);
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

        public MeshRef InitializeMeshRef(MeshData meshdata)
        {
            MeshRef? meshRef = null;

            if (RuntimeEnv.MainThreadId == Environment.CurrentManagedThreadId)
            {
                meshRef = mClientApi.Render.UploadMesh(meshdata);
            }
            else
            {
                mClientApi.Event.EnqueueMainThreadTask(() => {
                    meshRef = mClientApi.Render.UploadMesh(meshdata);
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
            Debug.Assert(animCacheObj is Dictionary<string, AnimCacheEntry>);
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
            if (Animator == null || capi.IsGamePaused || target != EnumItemRenderTarget.HandFp) return; // We don't get entity here, so only do it for the FP target

            if (ActiveAnimationsByAnimCode.Count > 0 || Animator.ActiveAnimationCount > 0 || RenderProceduralAnimations)
            {
                if (RenderProceduralAnimations) modSystem.OnBeforeRender(Animator, renderinfo.dt);
                Animator.OnFrame(ActiveAnimationsByAnimCode, renderinfo.dt);
            }
        }

        public virtual void RenderHandFp(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, double posX, double posY, double posZ, float size, int color, bool rotate = false, bool showStackSize = true)
        {
            if (onlyWhenAnimating && ActiveAnimationsByAnimCode.Count == 0 && !RenderProceduralAnimations)
            {
                mClientApi.Render.RenderMesh(renderInfo.ModelRef);
            }
            else
            {
                IShaderProgram prevProg = mClientApi.Render.CurrentActiveShader;
                IShaderProgram prog;

                IRenderAPI rpi = mClientApi.Render;
                prevProg?.Stop();

                Debug.Assert(modSystem.AnimatedItemShaderProgram != null);
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

                Vec4f lightRGBSVec4f = mClientApi.World.BlockAccessor.GetLightRGBs((int)(mClientApi.World.Player.Entity.Pos.X + mClientApi.World.Player.Entity.LocalEyePos.X), (int)(mClientApi.World.Player.Entity.Pos.Y + mClientApi.World.Player.Entity.LocalEyePos.Y), (int)(mClientApi.World.Player.Entity.Pos.Z + mClientApi.World.Player.Entity.LocalEyePos.Z));
                int num16 = (int)inSlot.Itemstack.Collectible.GetTemperature(mClientApi.World, inSlot.Itemstack);
                float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num16);
                int num17 = GameMath.Clamp((num16 - 550) / 2, 0, 255);
                Vec4f rgbaGlowIn = new(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], (float)num17 / 255f);
                prog.Uniform("extraGlow", num17);
                prog.Uniform("rgbaAmbientIn", mClientApi.Ambient.BlendedAmbientColor);
                prog.Uniform("rgbaLightIn", lightRGBSVec4f);
                prog.Uniform("rgbaGlowIn", rgbaGlowIn);

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
                prog.Uniform("lightPosition", new Vec3f(outPos.X, outPos.Y, outPos.Z).Normalize());
                prog.UniformMatrix("toShadowMapSpaceMatrixFar", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixFar);
                prog.UniformMatrix("toShadowMapSpaceMatrixNear", mClientApi.Render.ShaderUniforms.ToShadowMapSpaceMatrixNear);
                prog.BindTexture2D("itemTex", renderInfo.TextureId, 0);
                prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);

                prog.UniformMatrices4x3(
                    "elementTransforms",
                    GlobalConstants.MaxAnimatedElements,
                    Animator?.TransformationMatrices4x3
                );

                mClientApi.Render.RenderMesh(currentMeshRef);

                prog?.Stop();
                prevProg?.Use();
            }
        }
    }
#pragma warning restore CS8602
}
