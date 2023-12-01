﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnimationManagerLib.API;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationManager<TAnimationComposer> : API.IAnimationManager
        where TAnimationComposer : IComposer, new()
    {
        public const float VanillaAnimationWeight = 1f;
        
        private readonly ICoreClientAPI mClientApi;
        private readonly ISynchronizer mSynchronizer;
        private readonly AnimationApplier mApplier;

        private readonly Dictionary<AnimationTarget, IComposer> mComposers = new();
        private readonly Dictionary<AnimationId, IAnimation> mAnimations = new();
        
        private readonly Dictionary<Guid, AnimationTarget> mEntitiesByRuns = new();
        private readonly Dictionary<Guid, AnimationRequestWithStatus> mRequests = new();
        private readonly Dictionary<AnimationTarget, AnimationFrame> mAnimationFrames = new();
        private readonly HashSet<Guid> mSynchronizedPackets = new();


        public PlayerModelAnimationManager(ICoreClientAPI api, ISynchronizer synchronizer)
        {
            mClientApi = api;
            mSynchronizer = synchronizer;
            mApplier = new(api);
#if DEBUG
            api.ModLoader.GetModSystem<VSImGui.VSImGuiModSystem>().SetUpImGuiWindows += SetUpDebugWindow;
#endif
        }

        public bool Register(AnimationId id, AnimationData animation) => mAnimations.TryAdd(id, ConstructAnimation(id, animation));
        public Guid Run(AnimationTarget animationTarget, params AnimationRequest[] requests) => Run(Guid.NewGuid(), animationTarget, true, requests);
        public Guid Run(AnimationTarget animationTarget, bool synchronize, params AnimationRequest[] requests) => Run(Guid.NewGuid(), animationTarget, synchronize, requests);
        public Guid Run(AnimationTarget animationTarget, Guid runId, params AnimationRequest[] requests) => Run(runId, animationTarget, false, requests);
        public void Stop(Guid runId)
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

            AnimationTarget animationTarget = mEntitiesByRuns[runId];
            var composer = mComposers[animationTarget];
            AnimationRequest? request = mRequests[runId].Last();
            if (request != null) composer.Stop((AnimationRequest)request);
            mRequests.Remove(runId);
        }

        public void OnFrameHandler(Entity entity, float dt)
        {
            AnimationTarget animationTarget = new(entity.EntityId);

            ValidateEntities();

            if (!mComposers.ContainsKey(animationTarget)) return;

            mApplier.Clear();

            mAnimationFrames.Clear();
            TimeSpan timeSpan = TimeSpan.FromSeconds(dt);
            AnimationFrame composition = mComposers[animationTarget].Compose(timeSpan);
            mApplier.AddAnimation(animationTarget.EntityId.Value, composition);
        }
        public void OnFrameHandler(Vintagestory.API.Common.IAnimator animator, float dt)
        {
            AnimationTarget animationTarget = new(AnimationTargetType.HeldItemFp);

            if (!mComposers.ContainsKey(animationTarget)) return;

            mApplier.Clear();

            mAnimationFrames.Clear();
            TimeSpan timeSpan = TimeSpan.FromSeconds(dt);
            AnimationFrame composition = mComposers[animationTarget].Compose(timeSpan);
            mApplier.AddAnimation(animator, composition);
        }
        public void OnApplyAnimation(ElementPose pose)
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
            foreach (long entityId in mComposers.Keys.Where( (id, _) => id.EntityId != null && mClientApi.World.GetEntityById(id.EntityId.Value) == null).Select(id => id.EntityId.Value))
            {
                if (mClientApi.World.GetEntityById(entityId) == null) OnNullEntity(entityId);
            }
        }
        
        private Guid Run(Guid id, AnimationTarget animationTarget, bool synchronize, params AnimationRequest[] requests)
        {
            Debug.Assert(requests.Length > 0);

            if (synchronize && animationTarget.TargetType != AnimationTargetType.HeldItemFp)
            {
                AnimationRunPacket packet = new()
                {
                    RunId = id,
                    AnimationTarget = animationTarget,
                    Requests = requests
                };

                mSynchronizedPackets.Add(id);
                mSynchronizer.Sync(packet);
            }

            mRequests.Add(id, new(animationTarget, synchronize, requests));

            var composer = TryAddComposer(id, animationTarget);

            foreach (AnimationId animationId in requests.Select(request => request.Animation))
            {
                composer.Register(animationId, mAnimations[animationId]);
            }

            composer.Run((AnimationRequest)mRequests[id].Next(), () => ComposerCallback(id));

            return id;
        }

        private bool ComposerCallback(Guid id)
        {
            if (!mRequests.ContainsKey(id)) return true;
            if (mRequests[id].Finished()) return true;

            AnimationRequest? request = mRequests[id].Next();
            
            if (request == null)
            {
                mRequests.Remove(id);
                return true;
            }

            mComposers[mEntitiesByRuns[id]].Run((AnimationRequest)request, () => ComposerCallback(id));

            return false;
        }

        private IComposer TryAddComposer(Guid id, AnimationTarget animationTarget)
        {
            if (mEntitiesByRuns.ContainsKey(id)) return mComposers[mEntitiesByRuns[id]];

            mEntitiesByRuns.Add(id, animationTarget);

            if (mComposers.ContainsKey(animationTarget)) return mComposers[animationTarget];

            IComposer composer = Activator.CreateInstance(typeof(TAnimationComposer)) as IComposer;
            composer.SetAnimatorType<Animator>();
            mComposers.Add(animationTarget, composer);
            return composer;
        }

        static private IAnimation ConstructAnimation(AnimationId id, AnimationData data)
        {
            Dictionary<uint, Vintagestory.API.Common.Animation> animations = data.Shape.AnimationsByCrc32;
            uint crc32 = Utils.ToCrc32(data.Code);
            float totalFrames = animations[crc32].QuantityFrames;
            AnimationKeyFrame[] keyFrames = animations[crc32].KeyFrames;

            List<AnimationFrame> constructedKeyFrames = new();
            List<ushort> keyFramesToFrames = new();
            foreach (AnimationKeyFrame frame in keyFrames)
            {
                constructedKeyFrames.Add(new AnimationFrame(frame.Elements, data, id.Category));
                keyFramesToFrames.Add((ushort)frame.Frame);
                AddPosesNames(frame);
            }

            return new Animation(constructedKeyFrames.ToArray(), keyFramesToFrames.ToArray(), totalFrames, data.Cyclic);
        }

        static private void AddPosesNames(AnimationKeyFrame frame)
        {
            foreach ((string poseName, _) in frame.Elements)
            {
                uint hash = Utils.ToCrc32(poseName);
                if (!AnimationApplier.PosesNames.ContainsKey(hash))
                {
                    AnimationApplier.PosesNames[hash] = poseName;
                }
            }
        }

        private sealed class AnimationRequestWithStatus
        {
            private readonly AnimationRequest[] mRequests;
            private int mNextRequestIndex = 0;

            public bool Synchronize { get; set; }
            public AnimationTarget AnimationTarget { get; set; }

            public AnimationRequestWithStatus(AnimationTarget animationTarget, bool synchronize, AnimationRequest[] requests)
            {
                mRequests = requests;
                Synchronize = synchronize;
                AnimationTarget = animationTarget;
            }

            public AnimationRequest? Next() => mNextRequestIndex < mRequests.Length ? mRequests[mNextRequestIndex++] : null;
            public AnimationRequest? Last() => mNextRequestIndex < mRequests.Length ? mRequests[mNextRequestIndex] : mRequests[^1];
            public bool Finished() => mNextRequestIndex >= mRequests.Length;

        }

        public void SetUpDebugWindow()
        {
#if DEBUG
            ImGuiNET.ImGui.Begin("Current animations");
            ImGuiNET.ImGui.Text(string.Format("Registered animations: {0}", mAnimations.Count));
            ImGuiNET.ImGui.Text(string.Format("Active requests: {0}", mRequests.Count));
            ImGuiNET.ImGui.Text(string.Format("Active composers: {0}", mComposers.Count));
            ImGuiNET.ImGui.End();

            foreach ((_, IComposer composer) in mComposers)
            {
                composer.SetUpDebugWindow();
            }
#endif
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

            AddAnimation(animator, composition);
        }

        public void AddAnimation(Vintagestory.API.Common.IAnimator animator, AnimationFrame composition)
        {
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
}
