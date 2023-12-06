using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationManager : API.IAnimationManager
    {
        private readonly ICoreClientAPI mClientApi;
        private readonly ISynchronizer mSynchronizer;
        private readonly AnimationApplier mApplier;
        private readonly AnimationProvider mProvider;

        private readonly Dictionary<AnimationTarget, IComposer> mComposers = new();
        
        private readonly Dictionary<Guid, AnimationTarget> mEntitiesByRuns = new();
        private readonly Dictionary<Guid, AnimationRequestWithStatus> mRequests = new();
        private readonly Dictionary<AnimationTarget, AnimationFrame> mAnimationFrames = new();
        private readonly HashSet<Guid> mSynchronizedPackets = new();

        internal PlayerModelAnimationManager(ICoreClientAPI api, ISynchronizer synchronizer)
        {
            mClientApi = api;
            mSynchronizer = synchronizer;
            mApplier = new(api);
            mProvider = new(api);
#if DEBUG
            api.ModLoader.GetModSystem<VSImGui.VSImGuiModSystem>().SetUpImGuiWindows += SetUpDebugWindow;
#endif
        }

        public bool Register(AnimationId id, AnimationData animation) => mProvider.Register(id, animation);
        public Guid Run(AnimationTarget animationTarget, params AnimationRequest[] requests) => Run(Guid.NewGuid(), animationTarget, true, requests);
        public Guid Run(AnimationTarget animationTarget, bool synchronize, params AnimationRequest[] requests) => Run(Guid.NewGuid(), animationTarget, synchronize, requests);
        public Guid Run(AnimationTarget animationTarget, Guid runId, params AnimationRequest[] requests) => Run(runId, animationTarget, false, requests);
        public Guid Run(AnimationTarget animationTarget, AnimationId animationId, params RunParameters[] parameters) => Run(Guid.NewGuid(), animationTarget, true, ToRequests(animationId, parameters));
        public Guid Run(AnimationTarget animationTarget, bool synchronize, AnimationId animationId, params RunParameters[] parameters) => Run(Guid.NewGuid(), animationTarget, synchronize, ToRequests(animationId, parameters));
        public void Stop(Guid runId)
        {
            if (!mRequests.ContainsKey(runId)) return;

            if (mSynchronizedPackets.Contains(runId))
            {
                mSynchronizedPackets.Remove(runId);
                mSynchronizer.Sync(new AnimationStopPacket(runId));
            }

            AnimationTarget animationTarget = mEntitiesByRuns[runId];
            var composer = mComposers[animationTarget];
            AnimationRequest? request = mRequests[runId].Last();
            if (request != null) composer.Stop((AnimationRequest)request);
            mRequests.Remove(runId);
        }

        private Guid Run(Guid id, AnimationTarget animationTarget, bool synchronize, params AnimationRequest[] requests)
        {
            Debug.Assert(requests.Length > 0);

            mRequests.Add(id, new(animationTarget, synchronize, requests));
            AnimationRequest? request = mRequests[id].Next();
            if (request == null) return Guid.Empty;

            var composer = TryAddComposer(id, animationTarget);

            foreach (AnimationId animationId in requests.Select(request => request.Animation))
            {
                IAnimation? animation = mProvider.Get(animationId, animationTarget);
                if (animation == null)
                {
                    mClientApi.Logger.Error("Failed to get animation '{0}' for '{1}' while trying to run request, will skip it", animationId, animationTarget);
                    return Guid.Empty;
                }
                composer.Register(animationId, animation);
            }

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
            
            composer.Run(request.Value, (complete) => ComposerCallback(id, complete));

            return id;
        }

        public void OnFrameHandler(Entity entity, float dt)
        {
            AnimationTarget animationTarget = AnimationTarget.Entity(entity.EntityId);

            ValidateEntities();

            if (!mComposers.ContainsKey(animationTarget)) return;

            mApplier.Clear();

            mAnimationFrames.Clear();
            TimeSpan timeSpan = TimeSpan.FromSeconds(dt);
            AnimationFrame composition = mComposers[animationTarget].Compose(timeSpan);

            mApplier.AddAnimation(entity.EntityId, composition);
        }
        public void OnFrameHandler(Vintagestory.API.Common.IAnimator animator, float dt)
        {
            AnimationTarget animationTarget = AnimationTarget.HeldItem();

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

        private static AnimationRequest[] ToRequests(AnimationId animationId, params RunParameters[] parameters)
        {
            List<AnimationRequest> requests = new(parameters.Length);
            foreach (RunParameters item in parameters)
            {
                requests.Add(new(animationId, item));
            }
            return requests.ToArray();
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
            foreach (long? entityId in mComposers.Keys.Where( (id, _) => id.EntityId != null && mClientApi.World.GetEntityById(id.EntityId.Value) == null).Select(id => id.EntityId))
            {
                if (entityId != null && mClientApi.World.GetEntityById(entityId.Value) == null) OnNullEntity(entityId.Value);
            }
        }

        private bool ComposerCallback(Guid id, bool complete)
        {
            if (!mRequests.ContainsKey(id)) return true;
            if (!complete)
            {
                mRequests.Remove(id);
                return true;
            }
            if (mRequests[id].Finished())
            {
                mRequests.Remove(id);
                return true;
            }

            AnimationRequest? request = mRequests[id].Next();
            
            if (request == null)
            {
                mRequests.Remove(id);
                return true;
            }

            mComposers[mEntitiesByRuns[id]].Run((AnimationRequest)request, (complete) => ComposerCallback(id, complete));

            return false;
        }

        private IComposer TryAddComposer(Guid id, AnimationTarget animationTarget)
        {
            if (mEntitiesByRuns.ContainsKey(id)) return mComposers[mEntitiesByRuns[id]];

            mEntitiesByRuns.Add(id, animationTarget);

            if (mComposers.ContainsKey(animationTarget)) return mComposers[animationTarget];

            Composer composer = new();
            mComposers.Add(animationTarget, composer);
            return composer;
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
            ImGuiNET.ImGui.Begin("Animation manager");
            mProvider.SetUpDebugWindow();
            ImGuiNET.ImGui.Text(string.Format("Active requests: {0}", mRequests.Count));
            ImGuiNET.ImGui.Text(string.Format("Active composers: {0}", mComposers.Count));
            ImGuiNET.ImGui.NewLine();
            ImGuiNET.ImGui.SeparatorText("Composers:");

            foreach ((AnimationTarget target, IComposer composer) in mComposers)
            {
                bool collapsed = !ImGuiNET.ImGui.CollapsingHeader($"{target}");
                ImGuiNET.ImGui.Indent();
                if (!collapsed) composer.SetUpDebugWindow();
                ImGuiNET.ImGui.Unindent();
            }

            ImGuiNET.ImGui.End();
#endif
        }
    }

    internal class AnimationApplier
    {
        public Dictionary<ElementPose, (string name, AnimationFrame composition)> Poses { get; private set; } = new();
        static public Dictionary<uint, string> PosesNames { get; set; } = new();

        private readonly ICoreAPI mApi;

        public AnimationApplier(ICoreAPI api) => mApi = api;

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
                ElementPose pose = animator.GetPosebyName(name);
                if (pose != null) Poses[pose] = (name, composition);
            }
        }

        public void Clear()
        {
            Poses.Clear();
        }
    }

    internal class AnimationProvider
    {
        private readonly Dictionary<AnimationId, AnimationData> mAnimationsToConstruct = new();
        private readonly Dictionary<(AnimationId, AnimationTarget), IAnimation> mConstructedAnimations = new();
        private readonly Dictionary<AnimationId, IAnimation> mAnimations = new();
        private readonly ICoreClientAPI mApi;

        public AnimationProvider(ICoreClientAPI api)
        {
            mApi = api;
        }

        public bool Register(AnimationId id, AnimationData data)
        {
            if (data.Shape == null)
            {
                return mAnimationsToConstruct.TryAdd(id, data);
            }
            else
            {
                return mAnimations.TryAdd(id, ConstructAnimation(id, data, data.Shape));
            }
        }

        public IAnimation? Get(AnimationId id, AnimationTarget target)
        {
            if (mAnimations.ContainsKey(id)) return mAnimations[id];
            if (mConstructedAnimations.ContainsKey((id, target))) return mConstructedAnimations[(id, target)];
            if (!mAnimationsToConstruct.ContainsKey(id)) return null;

            if (target.EntityId == null) return null;
            Entity entity = mApi.World.GetEntityById(target.EntityId.Value);
            if (entity == null) return null;
            AnimationData data = AnimationData.Entity(mAnimationsToConstruct[id].Code, entity, mAnimationsToConstruct[id].Cyclic);
            if (data.Shape == null) return null;
            mConstructedAnimations.Add((id, target), ConstructAnimation(id, data, data.Shape));
            return mConstructedAnimations[(id, target)];
        }

        static private IAnimation ConstructAnimation(AnimationId id, AnimationData data, Shape shape)
        {
            Dictionary<uint, Vintagestory.API.Common.Animation> animations = shape.AnimationsByCrc32;
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

            return new Animation(id, constructedKeyFrames.ToArray(), keyFramesToFrames.ToArray(), totalFrames, data.Cyclic);
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

        public void SetUpDebugWindow()
        {
#if DEBUG
            ImGuiNET.ImGui.Begin("Animation manager");
            ImGuiNET.ImGui.Text(string.Format("Registered animations: {0}", mAnimationsToConstruct.Count + mAnimations.Count));
            ImGuiNET.ImGui.Text(string.Format("Registered pre-constructed animations: {0}", mAnimations.Count));
            ImGuiNET.ImGui.Text(string.Format("Registered not pre-constructed animations: {0}", mAnimationsToConstruct.Count));
            ImGuiNET.ImGui.Text(string.Format("Registered constructed animations: {0}", mConstructedAnimations.Count));
            ImGuiNET.ImGui.End();
#endif
        }
    }
}
