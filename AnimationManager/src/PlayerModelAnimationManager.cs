using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationManager<TAnimationComposer> : API.IAnimationManager
        where TAnimationComposer : IComposer<PlayerModelAnimationFrame>, new()
    {
        public const float VanillaAnimationWeight = 1f;
        
        private readonly ICoreClientAPI mClientApi;
        private readonly ISynchronizer mSynchronizer;
        
        private readonly AnimationApplier mApplier;

        private readonly Dictionary<long, IComposer<PlayerModelAnimationFrame>> mComposers = new();
        private readonly Dictionary<Guid, long> mEntitiesByRuns = new();
        private readonly Dictionary<AnimationId, IAnimation<PlayerModelAnimationFrame>> mAnimations = new();
        private readonly Dictionary<AnimationId, string> mAnimationCodes = new();
        private readonly Dictionary<Guid, AnimationRequestWithStatus> mRequests = new();
        private readonly Dictionary<long, Composition<PlayerModelAnimationFrame>> mCompositions = new();
        private readonly HashSet<Guid> mSynctonizedPackets = new();
        private long mCurrentRenderedEntity;

        public PlayerModelAnimationManager(ICoreClientAPI api, ISynchronizer synchronizer)
        {
            mClientApi = api;
            mSynchronizer = synchronizer;
            mApplier = new(api);
            Patches.AnimatorBasePatch.OnElementPoseUsedCallback += OnApplyAnimation;
            RegisterHandler();
        }

        bool API.IAnimationManager.Register(AnimationId id, JsonObject definition) => throw new NotImplementedException();
        bool API.IAnimationManager.Register(AnimationId id, AnimationMetaData metaData) => throw new NotImplementedException();
        bool API.IAnimationManager.Register(AnimationId id, string playerAnimationCode) => mAnimationCodes.TryAdd(id, playerAnimationCode);
        Guid API.IAnimationManager.Run(long entityId, params AnimationRequest[] requests) => Run(Guid.NewGuid(), entityId, true, requests);
        Guid API.IAnimationManager.Run(long entityId, bool synchronize, params AnimationRequest[] requests) => Run(Guid.NewGuid(), entityId, synchronize, requests);
        Guid API.IAnimationManager.Run(long entityId, Guid runId, params AnimationRequest[] requests) => Run(runId, entityId, false, requests);
        void API.IAnimationManager.Stop(Guid runId) => Stop(runId);
        void IDisposable.Dispose() => throw new NotImplementedException();

        private void OnRenderFrame(float secondsElapsed, long entityId)
        {
            mCompositions.Clear();
            TimeSpan timeSpan = TimeSpan.FromSeconds(secondsElapsed);
            Composition<PlayerModelAnimationFrame> composition = mComposers[entityId].Compose(new (entityId), timeSpan);
            mApplier.AddAnimation(entityId, composition);
        }

        private void RegisterHandler()
        {
            Patches.AnimatorBasePatch.OnFrameCallback += OnFrameHandler;
        }

        private void UnregisterHandler()
        {
            Patches.AnimatorBasePatch.OnFrameCallback -= OnFrameHandler;
        }

        private void OnFrameHandler(Entity entity, float dt)
        {
            long entityId = entity.EntityId;

            if (!mComposers.ContainsKey(entityId)) return;

            mClientApi.Logger.Error("Rendering for: {0}, {1}", (entity as EntityPlayer)?.GetName(), dt);

            mApplier.Clear();
            OnRenderFrame(dt, entityId);
            mCurrentRenderedEntity = entityId;
        }

        private void OnApplyAnimation(ElementPose pose)
        {
            mApplier.ApplyAnimation(pose);
        }

        private void OnNullEntity(long entityId)
        {
            foreach ((Guid runId, _) in mEntitiesByRuns.Where((key, value) => value == entityId))
            {
                Stop(runId);
            }
        }
        
        private Guid Run(Guid id, long entityId, bool synchronize, params AnimationRequest[] requests)
        {
            Debug.Assert(requests.Length > 0);

            if (synchronize)
            {
                AnimationRunPacket packet = new()
                {
                    RunId = id,
                    EntityId = entityId,
                    Requests = requests
                };

                mSynctonizedPackets.Add(id);
                mSynchronizer.Sync(packet);
            }

            mClientApi?.Logger.Error("**************** Animator type: {0}", mClientApi.World.GetEntityById(entityId)?.AnimManager.Animator.GetType().FullName);

            mRequests.Add(id, new(entityId, synchronize, requests));

            var composer = TryAddComposer(id, entityId);

            foreach (AnimationId animationId in requests.Select(request => request.Animation))
            {
                composer.Register(animationId, GetAnimation(animationId, entityId));
            }

            composer.Run((AnimationRequest)mRequests[id].Next(), () => ComposerCallback(id));

            return id;
        }

        private bool ComposerCallback(Guid id)
        {
            if (!mRequests.ContainsKey(id)) return true;
            if (mRequests[id].Finished()) return true;

            AnimationRequest? request = mRequests[id].Next();
            
            if (request == null) return true;

            mComposers[mEntitiesByRuns[id]].Run((AnimationRequest)request, () => ComposerCallback(id));

            return false;
        }

        private void Stop(Guid runId)
        {
            if (!mRequests.ContainsKey(runId)) return;

            if (mSynctonizedPackets.Contains(runId))
            {
                AnimationStopPacket packet = new()
                {
                    RunId = runId
                };

                mSynctonizedPackets.Remove(runId);
                mSynchronizer.Sync(packet);
            }

            mClientApi.Logger.Error("Stopping: {0}", runId);

            long entityId = mEntitiesByRuns[runId];
            var composer = mComposers[entityId];
            AnimationRequest? request = mRequests[runId].Last();
            if (request != null) composer.Stop((AnimationRequest)request);
            mRequests.Remove(runId);
        }

        private sealed class AnimationRequestWithStatus
        {
            private readonly AnimationRequest[] mRequests;
            private int mNextRequestIndex = 0;

            public bool Synchronize { get; set; }
            public long EntityId { get; set; }

            public AnimationRequestWithStatus(long entityId, bool synchronize, AnimationRequest[] requests)
            {
                mRequests = requests;
                Synchronize = synchronize;
                EntityId = entityId;
            }

            public AnimationRequest? Next() => mNextRequestIndex < mRequests.Length ? mRequests[mNextRequestIndex++] : null;
            public AnimationRequest? Last() => mNextRequestIndex < mRequests.Length ? mRequests[mNextRequestIndex] : mRequests[mRequests.Length - 1];
            public bool Finished() => mNextRequestIndex >= mRequests.Length;

        }

        private IComposer<PlayerModelAnimationFrame> TryAddComposer(Guid id, long entityId)
        {
            if (mEntitiesByRuns.ContainsKey(id)) return mComposers[mEntitiesByRuns[id]];

            mEntitiesByRuns.Add(id, entityId);

            if (mComposers.ContainsKey(entityId)) return mComposers[entityId];

            IComposer<PlayerModelAnimationFrame> composer = Activator.CreateInstance(typeof(TAnimationComposer)) as IComposer<PlayerModelAnimationFrame>;
            composer.Init(mClientApi, new()); // @TODO Fix null ?
            composer.SetAnimatorType<PlayerModelAnimator<PlayerModelAnimationFrame>>();
            mComposers.Add(entityId, composer);
            return composer;
        }

        private IAnimation<PlayerModelAnimationFrame> GetAnimation(AnimationId animationId, long entityId)
        {
            if (mAnimations.ContainsKey(animationId)) return mAnimations[animationId];

            Debug.Assert(mAnimationCodes.ContainsKey(animationId));

            var animation = AnimationProvider.Get(mClientApi, entityId, mAnimationCodes[animationId]);

            mAnimations.Add(animationId, animation);

            return animation;
        }
    }

    public class AnimationApplier
    {
        public Dictionary<ElementPose, (string name, Composition<PlayerModelAnimationFrame> composition)> Poses { get; private set; }

        private readonly ICoreAPI mApi;

        public AnimationApplier(ICoreAPI api)
        {
            Poses = new();
            mApi = api;
        }

        public bool ApplyAnimation(ElementPose pose)
        {
            if (pose == null || !Poses.ContainsKey(pose)) return false;

            (string name, var composition) = Poses[pose];

            mApi.Logger.Warning("*** ApplyAnimation for {0}", pose.ForElement?.Name);

            composition.ToAverage.ApplyByAverage(pose, name, 1, composition.Weight);
            composition.ToAdd.ApplyByAddition(pose, name);

            Poses.Remove(pose);

            return true;
        }

        public void AddAnimation(long entityId, Composition<PlayerModelAnimationFrame> composition)
        {
            IAnimator animator = mApi.World.GetEntityById(entityId).AnimManager.Animator;

            foreach (string name in composition.ToAverage.mPoses.Keys)
            {
                Poses[animator.GetPosebyName(name)] = (name, composition);
            }

            foreach (string name in composition.ToAdd.mPoses.Keys)
            {
                Poses[animator.GetPosebyName(name)] = (name, composition);
            }
        }

        public void Clear()
        {
            Poses.Clear();
        }
    }

    static public class AnimationProvider
    {
        static public IAnimation<PlayerModelAnimationFrame> Get(ICoreClientAPI api, long entityId, string name)
        {
            List<PlayerModelAnimationFrame> constructedKeyFrames = new();
            List<ushort> keyFramesToFrames = new();

            (AnimationKeyFrame[] keyFrames, AnimationMetaData metaData) = AnimationData(api, entityId, name);

            Debug.Assert(metaData != null);

            foreach (AnimationKeyFrame frame in keyFrames)
            {
                constructedKeyFrames.Add(ConstructFrame(frame.Elements, metaData));
                keyFramesToFrames.Add((ushort)frame.Frame);
            }

            return new PlayerModelAnimation<PlayerModelAnimationFrame>(constructedKeyFrames.ToArray(), keyFramesToFrames.ToArray(), new PlayerModelAnimationFrame());
        }
        static private PlayerModelAnimationFrame ConstructFrame(Dictionary<string, AnimationKeyFrameElement> elements, AnimationMetaData metaData)
        {
            Dictionary<string, PlayerModelAnimationPose> poses = new();

            foreach ((string element, var transform) in elements)
            {
                EnumAnimationBlendMode blendMode = metaData.ElementBlendMode.ContainsKey(element) ? metaData.ElementBlendMode[element] : EnumAnimationBlendMode.Average;
                float weight = metaData.ElementWeight.ContainsKey(element) ? metaData.ElementWeight[element] : 1;

                poses.Add(element, new PlayerModelAnimationPose(transform, blendMode, weight));
            }

            return new PlayerModelAnimationFrame(poses, metaData);
        }
        static private (AnimationKeyFrame[] keyFrames, AnimationMetaData metaData) AnimationData(ICoreClientAPI api, long entityId, string name)
        {
            Entity entity = api.World.GetEntityById(entityId);
            entity.Properties.Client.AnimationsByMetaCode.TryGetValue(name, out AnimationMetaData metaData);
            Shape shape = entity.Properties.Client.LoadedShapeForEntity;
            Dictionary<uint, Animation> animations = shape.AnimationsByCrc32;
            uint crc32 = Utils.ToCrc32(name);
            return (animations[crc32].KeyFrames, metaData);
        }
    }
}
