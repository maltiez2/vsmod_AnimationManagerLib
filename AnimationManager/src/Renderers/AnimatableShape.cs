using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib;
public class AnimatableShape : ITexPositionSource
{
    public AnimatorBase Animator { get; private set; }
    public Shape Shape { get; private set; }
    public MultiTextureMeshRef MeshRef { get; private set; }
    public ITextureAtlasAPI Atlas { get; private set; }
    public string CacheKey { get; private set; }

    private readonly ICoreClientAPI mClientApi;

    public static AnimatableShape? Create(ICoreClientAPI api, string shapePath)
    {
        string cacheKey = $"shapeEditorCollectibleMeshes-{shapePath.GetHashCode()}";
        AssetLocation shapeLocation = new(shapePath);
        shapeLocation = shapeLocation.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
        Shape? currentShape = Shape.TryGet(api, shapeLocation);
        currentShape?.ResolveReferences(api.Logger, cacheKey);
        AnimatorBase? animator = GetAnimator(api, cacheKey, currentShape);

        if (currentShape == null || animator == null) return null;

        return new AnimatableShape(api, cacheKey, currentShape, animator);
    }

    private AnimatableShape(ICoreClientAPI api, string cacheKey, Shape currentShape, AnimatorBase animator)
    {
        mClientApi = api;
        CacheKey = cacheKey;
        Shape = currentShape;
        Animator = animator;
        Atlas = api.ItemTextureAtlas;

        MeshData meshData = InitializeMeshData(api, cacheKey, currentShape, this);
        MeshRef = InitializeMeshRef(api, meshData);
    }

    private static MeshData InitializeMeshData(ICoreClientAPI clientApi, string cacheDictKey, Shape shape, ITexPositionSource texSource)
    {
        shape.ResolveReferences(clientApi.World.Logger, cacheDictKey);
        CacheInvTransforms(shape.Elements);
        shape.ResolveAndFindJoints(clientApi.Logger, cacheDictKey);

        clientApi.Tesselator.TesselateShapeWithJointIds("collectible", shape, out MeshData meshData, texSource, null);

        return meshData;
    }
    private static MultiTextureMeshRef InitializeMeshRef(ICoreClientAPI clientApi, MeshData meshData)
    {
        MultiTextureMeshRef? meshRef = null;

        if (RuntimeEnv.MainThreadId == Environment.CurrentManagedThreadId)
        {
            meshRef = clientApi.Render.UploadMultiTextureMesh(meshData);
        }
        else
        {
            clientApi.Event.EnqueueMainThreadTask(() =>
            {
                meshRef = clientApi.Render.UploadMultiTextureMesh(meshData);
            }, "uploadmesh");
        }

        Debug.Assert(meshRef != null);
        return meshRef;
    }
    private static void CacheInvTransforms(ShapeElement[] elements)
    {
        if (elements == null) return;

        for (int i = 0; i < elements.Length; i++)
        {
            elements[i].CacheInverseTransformMatrix();
            CacheInvTransforms(elements[i].Children);
        }
    }
    private static AnimatorBase? GetAnimator(ICoreClientAPI clientApi, string cacheDictKey, Shape? blockShape)
    {
        if (blockShape == null)
        {
            return null;
        }

        Dictionary<string, AnimCacheEntry>? animationCache;
        clientApi.ObjectCache.TryGetValue("coAnimCache", out object? animCacheObj);
        animationCache = animCacheObj as Dictionary<string, AnimCacheEntry>;
        if (animationCache == null)
        {
            clientApi.ObjectCache["coAnimCache"] = animationCache = new Dictionary<string, AnimCacheEntry>();
        }

        AnimatorBase animator;

        if (animationCache.TryGetValue(cacheDictKey, out AnimCacheEntry? cacheObj))
        {
            animator = clientApi.Side == EnumAppSide.Client ?
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

            animator = clientApi.Side == EnumAppSide.Client ?
                new ClientAnimator(() => 1, blockShape.Animations, blockShape.Elements, blockShape.JointsById) :
                new ServerAnimator(() => 1, blockShape.Animations, blockShape.Elements, blockShape.JointsById)
            ;

            animationCache[cacheDictKey] = new AnimCacheEntry()
            {
                Animations = blockShape.Animations,
                RootElems = (animator as ClientAnimator)?.rootElements,
                RootPoses = (animator as ClientAnimator)?.RootPoses
            };
        }

        return animator;
    }


    public Size2i? AtlasSize => Atlas?.Size;
    public virtual TextureAtlasPosition? this[string textureCode]
    {
        get
        {
            AssetLocation? texturePath = null;
            Shape?.Textures.TryGetValue(textureCode, out texturePath);

            if (texturePath == null)
            {
                texturePath = new AssetLocation(textureCode);
            }

            return GetOrCreateTexPos(texturePath);
        }
    }
    private TextureAtlasPosition? GetOrCreateTexPos(AssetLocation texturePath)
    {
        if (Atlas == null) return null;
        
        TextureAtlasPosition texturePosition = Atlas[texturePath];

        if (texturePosition == null)
        {
            IAsset texAsset = mClientApi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
            if (texAsset != null)
            {
                Atlas.GetOrInsertTexture(texturePath, out _, out texturePosition);
            }
            else
            {
                mClientApi.World.Logger.Warning($"Bullseye.CollectibleBehaviorAnimatable: texture {texturePath}, not no such texture found.");
            }
        }

        return texturePosition;
    }
}