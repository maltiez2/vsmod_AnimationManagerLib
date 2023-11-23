using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationManager<TAnimationComposer> : API.IAnimationManager
        where TAnimationComposer : IAnimationComposer<PlayerModelAnimationFrame>, new()
    {
        public const float VanillaAnimationWeight = 1f;
        
        private readonly ICoreClientAPI mClientApi;
        private readonly IAnimationSynchronizer mSynchronizer;
        private readonly Dictionary<long, IAnimationComposer<PlayerModelAnimationFrame>> mComposers = new();
        private readonly Dictionary<Guid, long> mEntitiesByRuns = new();
        private readonly Dictionary<AnimationId, IAnimation<PlayerModelAnimationFrame>> mAnimations = new();
        private readonly Dictionary<Guid, AnimationRequestWithStatus> mRequests = new();
        private readonly Dictionary<long, Patches.AnimatorBasePatch.OnFrameHandler> mHandlers = new();

        public PlayerModelAnimationManager(ICoreClientAPI api, IAnimationSynchronizer synchronizer)
        {
            mClientApi = api;
            mSynchronizer = synchronizer;
        }

        bool API.IAnimationManager.Register(AnimationId id, JsonObject definition) => throw new NotImplementedException();
        bool API.IAnimationManager.Register(AnimationId id, AnimationMetaData metaData) => throw new NotImplementedException();
        bool API.IAnimationManager.Register(AnimationId id, string playerAnimationCode) => throw new NotImplementedException();
        Guid API.IAnimationManager.Run(long entityId, params AnimationRequest[] requests) => Run(Guid.NewGuid(), entityId, true, requests);
        Guid API.IAnimationManager.Run(long entityId, bool synchronize, params AnimationRequest[] requests) => Run(Guid.NewGuid(), entityId, synchronize, requests);
        void API.IAnimationManager.Stop(Guid runId) => Stop(runId);
        void IDisposable.Dispose() => throw new NotImplementedException();

        private void OnRenderFrame(float secondsElapsed, long entityId)
        {
            if (!mComposers.ContainsKey(entityId)) return;
            
            TimeSpan timeSpan = TimeSpan.FromSeconds(secondsElapsed);
            Composition<PlayerModelAnimationFrame> composition = mComposers[entityId].Compose(new (entityId), timeSpan);
            ApplyAnimation(entityId, composition);
        }

        private void RegisterHandler(long entityId)
        {
            Patches.AnimatorBasePatch.OnFrameHandler handler = (AnimatorBase animator, float dt) => OnFrameHandler(animator, dt, entityId);
            mHandlers.Add(entityId, handler);
            Patches.AnimatorBasePatch.OnFrameCallback += handler;
        }

        private void UnregisterHandler(long entityId)
        {
            if (!mHandlers.ContainsKey(entityId)) return;
            Patches.AnimatorBasePatch.OnFrameCallback -= mHandlers[entityId];
            mHandlers.Remove(entityId);
        }

        private void OnFrameHandler(AnimatorBase animator, float dt, long entityId)
        {
            Entity entity = mClientApi.World.GetEntityById(entityId);

            if (entity == null)
            {
                OnNullEntity(entityId);
                return;
            }

            if (animator != entity.AnimManager.Animator) return;

            OnRenderFrame(dt, entityId);
        }

        private void OnNullEntity(long entityId)
        {
            UnregisterHandler(entityId);
            foreach ((Guid runId, _) in mEntitiesByRuns.Where((key, value) => value == entityId))
            {
                Stop(runId);
            }
        }
        
        private Guid Run(Guid id, long entityId, bool synchronize, params AnimationRequest[] requests)
        {
            Debug.Assert(requests.Length > 0);

            mRequests.Add(id, new(entityId, synchronize, requests));

            var composer = TryAddComposer(id, entityId);

            foreach (AnimationId animationId in requests.Select(request => request.AnimationId))
            {
                composer.Register(animationId, mAnimations[animationId]);
            }

            composer.Run((AnimationRequest)mRequests[id].Next(), () => ComposerCallback(id));

            return id;
        }

        private bool ComposerCallback(Guid id)
        {
            if (mRequests[id].Finished()) return true;
            
            mComposers[mEntitiesByRuns[id]].Run((AnimationRequest)mRequests[id].Next(), () => ComposerCallback(id));

            return false;
        }

        private void Stop(Guid runId)
        {
            if (!mRequests.ContainsKey(runId)) return;

            long entityId = mEntitiesByRuns[runId];
            var composer = mComposers[entityId];

            composer.Stop((AnimationRequest)mRequests[runId].Last());
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
            public AnimationRequest? Last() => mNextRequestIndex < mRequests.Length ? mRequests[mNextRequestIndex] : null;
            public bool Finished() => mNextRequestIndex < mRequests.Length;

        }

        private IAnimationComposer<PlayerModelAnimationFrame> TryAddComposer(Guid id, long entityId)
        {
            if (mEntitiesByRuns.ContainsKey(id)) return mComposers[mEntitiesByRuns[id]];

            mEntitiesByRuns.Add(id, entityId);

            if (mComposers.ContainsKey(entityId)) return mComposers[entityId];

            IAnimationComposer<PlayerModelAnimationFrame> composer = Activator.CreateInstance(typeof(TAnimationComposer)) as IAnimationComposer<PlayerModelAnimationFrame>;
            composer.Init(mClientApi, null); // @TODO Fix null
            mComposers.Add(entityId, composer);
            RegisterHandler(entityId);
            return composer;
        }

        private void ApplyAnimation(long entityId, Composition<PlayerModelAnimationFrame> composition)
        {
            IAnimator animator = mClientApi.World.GetEntityById(entityId).AnimManager.Animator;

            composition.ToAverage.ApplyByAverage(animator, VanillaAnimationWeight, composition.Weight);
            composition.ToAdd.ApplyByAddition(animator);
        }
    }
}
