using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationManager<TAnimationComposer> : API.IAnimationManager
        where TAnimationComposer : IComposer, new()
    {
        public const float VanillaAnimationWeight = 1f;
        
        private readonly ICoreClientAPI mClientApi;
        private readonly ISynchronizer mSynchronizer;
        private readonly AnimationApplier mApplier;

        private readonly Dictionary<long, IComposer> mComposers = new();
        private readonly Dictionary<AnimationId, IAnimation> mAnimations = new();
        
        private readonly Dictionary<Guid, long> mEntitiesByRuns = new();
        private readonly Dictionary<AnimationId, string> mAnimationCodes = new();
        private readonly Dictionary<Guid, AnimationRequestWithStatus> mRequests = new();
        private readonly Dictionary<long, AnimationFrame> mAnimationFrames = new();
        private readonly HashSet<Guid> mSynchronizedPackets = new();


        public PlayerModelAnimationManager(ICoreClientAPI api, ISynchronizer synchronizer)
        {
            mClientApi = api;
            mSynchronizer = synchronizer;
            mApplier = new(api);
            RegisterHandlers();
        }

        bool API.IAnimationManager.Register(AnimationId id, string playerAnimationCode) => mAnimationCodes.TryAdd(id, playerAnimationCode);
        Guid API.IAnimationManager.Run(long entityId, params AnimationRequest[] requests) => Run(Guid.NewGuid(), entityId, true, requests);
        Guid API.IAnimationManager.Run(long entityId, bool synchronize, params AnimationRequest[] requests) => Run(Guid.NewGuid(), entityId, synchronize, requests);
        Guid API.IAnimationManager.Run(long entityId, Guid runId, params AnimationRequest[] requests) => Run(runId, entityId, false, requests);
        void API.IAnimationManager.Stop(Guid runId) => Stop(runId);

        private void OnRenderFrame(float secondsElapsed, long entityId)
        {
            mAnimationFrames.Clear();
            TimeSpan timeSpan = TimeSpan.FromSeconds(secondsElapsed);
            AnimationFrame composition = mComposers[entityId].Compose(timeSpan);
            mApplier.AddAnimation(entityId, composition);
        }

        private void RegisterHandlers()
        {
            Patches.AnimatorBasePatch.OnElementPoseUsedCallback += OnApplyAnimation;
            Patches.AnimatorBasePatch.OnFrameCallback += OnFrameHandler;
        }

        private void UnregisterHandlers()
        {
            Patches.AnimatorBasePatch.OnElementPoseUsedCallback -= OnApplyAnimation;
            Patches.AnimatorBasePatch.OnFrameCallback -= OnFrameHandler;
        }

        private void OnFrameHandler(Entity entity, float dt)
        {
            long entityId = entity.EntityId;

            ValidateEntities();

            if (!mComposers.ContainsKey(entityId)) return;

            mApplier.Clear();
            OnRenderFrame(dt, entityId);
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

        private void ValidateEntities()
        {
            foreach (long entityId in mComposers.Keys.Where( (id, _) => mClientApi.World.GetEntityById(id) == null ))
            {
                if (mClientApi.World.GetEntityById(entityId) == null) OnNullEntity(entityId);
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

                mSynchronizedPackets.Add(id);
                mSynchronizer.Sync(packet);
            }

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

            if (mSynchronizedPackets.Contains(runId))
            {
                AnimationStopPacket packet = new()
                {
                    RunId = runId
                };

                mSynchronizedPackets.Remove(runId);
                mSynchronizer.Sync(packet);
            }

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

        private IComposer TryAddComposer(Guid id, long entityId)
        {
            if (mEntitiesByRuns.ContainsKey(id)) return mComposers[mEntitiesByRuns[id]];

            mEntitiesByRuns.Add(id, entityId);

            if (mComposers.ContainsKey(entityId)) return mComposers[entityId];

            IComposer composer = Activator.CreateInstance(typeof(TAnimationComposer)) as IComposer;
            composer.SetAnimatorType<Animator>();
            mComposers.Add(entityId, composer);
            return composer;
        }

        private IAnimation GetAnimation(AnimationId animationId, long entityId)
        {
            if (mAnimations.ContainsKey(animationId)) return mAnimations[animationId];

            Debug.Assert(mAnimationCodes.ContainsKey(animationId));

            var animation = AnimationProvider.Get(mClientApi, animationId.Category, entityId, mAnimationCodes[animationId]);

            mAnimations.Add(animationId, animation);

            return animation;
        }

        private bool mDisposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!mDisposedValue)
            {
                if (disposing)
                {
                    UnregisterHandlers();
                }

                mDisposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class AnimationApplier
    {
        public Dictionary<ElementPose, (string name, AnimationFrame composition)> Poses { get; private set; }
        static public Dictionary<uint, string> PosesNames { get; set; }

        private readonly ICoreAPI mApi;

        public AnimationApplier(ICoreAPI api)
        {
            Poses = new();
            PosesNames = new();
            mApi = api;
        }

        public bool ApplyAnimation(ElementPose pose)
        {
            if (pose == null || !Poses.ContainsKey(pose)) return false;

            (string name, var composition) = Poses[pose];

            composition.Apply(pose, 1, Utils.ToCrc32(name));

            Poses.Remove(pose);

            return true;
        }

        public void AddAnimation(long entityId, AnimationFrame composition)
        {
            Vintagestory.API.Common.IAnimator animator = mApi.World.GetEntityById(entityId).AnimManager.Animator;

            foreach ((var id, _) in composition.Elements)
            {
                string name = PosesNames[id.ElementNameHash];
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
        static public IAnimation Get(ICoreClientAPI api, CategoryId category, long entityId, string name)
        {
            List<AnimationFrame> constructedKeyFrames = new();
            List<ushort> keyFramesToFrames = new();

            (AnimationKeyFrame[] keyFrames, AnimationMetaData metaData) = AnimationData(api, entityId, name);

            Debug.Assert(metaData != null);

            foreach (AnimationKeyFrame frame in keyFrames)
            {
                constructedKeyFrames.Add(new AnimationFrame(frame.Elements, metaData, category));
                keyFramesToFrames.Add((ushort)frame.Frame);
                AddPosesNames(frame);
            }

            return new Animation(constructedKeyFrames.ToArray(), keyFramesToFrames.ToArray());
        }
        static private void AddPosesNames(AnimationKeyFrame frame)
        {
            foreach ((string poseName, _) in frame.Elements)
            {
                uint hash = Utils.ToCrc32(poseName);
                if (!AnimationApplier.PosesNames.ContainsKey(hash))
                {
                    AnimationApplier.PosesNames[hash] = (string)poseName.Clone();
                }
            }
        }
        static private (AnimationKeyFrame[] keyFrames, AnimationMetaData metaData) AnimationData(ICoreClientAPI api, long entityId, string name)
        {
            Entity entity = api.World.GetEntityById(entityId);
            entity.Properties.Client.AnimationsByMetaCode.TryGetValue(name, out AnimationMetaData metaData);
            Shape shape = entity.Properties.Client.LoadedShapeForEntity;
            Dictionary<uint, Vintagestory.API.Common.Animation> animations = shape.AnimationsByCrc32;
            uint crc32 = Utils.ToCrc32(name);
            return (animations[crc32].KeyFrames, metaData);
        }
    }
}
