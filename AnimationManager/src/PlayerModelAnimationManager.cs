using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationManager<TAnimationComposer> : API.IAnimationManager
        where TAnimationComposer : IAnimationComposer<PlayerModelAnimationFrame>, new()
    {
        private readonly ICoreClientAPI mClientApi;
        private readonly IAnimationSynchronizer mSynchronizer;
        private readonly Dictionary<long, IAnimationComposer<PlayerModelAnimationFrame>> mComposers = new();
        private readonly Dictionary<Guid, long> mEntitiesByRuns = new();
        private readonly Dictionary<AnimationIdentifier, IAnimation<PlayerModelAnimationFrame>> mAnimations = new();
        private readonly Dictionary<Guid, AnimationRequestWithStatus> mRequests = new();

        public PlayerModelAnimationManager(ICoreClientAPI api, IAnimationSynchronizer synchronizer)
        {
            mClientApi = api;
            mSynchronizer = synchronizer;
        }

        bool API.IAnimationManager.Register(AnimationIdentifier id, JsonObject definition) => throw new NotImplementedException();
        bool API.IAnimationManager.Register(AnimationIdentifier id, AnimationMetaData metaData) => throw new NotImplementedException();
        bool API.IAnimationManager.Register(AnimationIdentifier id, string playerAnimationCode) => throw new NotImplementedException();
        Guid API.IAnimationManager.Run(long entityId, params AnimationRequest[] requests) => Run(Guid.NewGuid(), entityId, true, requests);
        Guid API.IAnimationManager.Run(long entityId, bool synchronize, params AnimationRequest[] requests) => Run(Guid.NewGuid(), entityId, synchronize, requests);
        void API.IAnimationManager.Stop(Guid runId) => Stop(runId);
        void IDisposable.Dispose() => throw new NotImplementedException();

        private void OnRenderFrame(float timeElapsed, long entityId)
        {
            if (!mComposers.ContainsKey(entityId)) return;
            
            TimeSpan timeSpan = TimeSpan.FromSeconds(timeElapsed);
            var composer = mComposers[entityId];
            PlayerModelAnimationFrame animation = composer.Compose(new() { EntityId = entityId }, timeSpan);
            ApplyAnimation(entityId, animation);
        }
        
        private Guid Run(Guid id, long entityId, bool synchronize, params AnimationRequest[] requests)
        {
            Debug.Assert(requests.Length > 0);

            mRequests.Add(id, new(entityId, synchronize, requests));

            var composer = TryAddComposer(id, entityId);

            foreach (AnimationIdentifier animationId in requests.Select(request => request.AnimationId))
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
            return composer;
        }

        private void ApplyAnimation(long entityId, PlayerModelAnimationFrame animation)
        {

        }
    }
}
