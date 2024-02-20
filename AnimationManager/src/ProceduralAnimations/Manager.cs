using AnimationManagerLib.API;
using AnimationManagerLib.Patches;
using VSImGui;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AnimationManagerLib;

public class AnimationManager : API.IAnimationManager
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

    internal AnimationManager(ICoreClientAPI api, ISynchronizer synchronizer)
    {
        mClientApi = api;
        mSynchronizer = synchronizer;
        mApplier = new(api);
        mProvider = new(api, this);
#if DEBUG
        api.ModLoader.GetModSystem<VSImGui.VSImGuiModSystem>().SetUpImGuiWindows += SetUpDebugWindow;
        Api = api;
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
        IComposer composer = mComposers[animationTarget];
        AnimationRequest? request = mRequests[runId].Last();
        if (request != null) composer.Stop(request.Value);
        mRequests.Remove(runId);
    }

    private Guid Run(Guid id, AnimationTarget animationTarget, bool synchronize, params AnimationRequest[] requests)
    {
        Debug.Assert(requests.Length > 0);

        mRequests.Add(id, new(animationTarget, synchronize, requests, this));
        AnimationRequest? request = mRequests[id].Next();
        if (request == null) return Guid.Empty;

        IComposer composer = TryAddComposer(id, animationTarget);

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

        if (synchronize && animationTarget.TargetType != AnimationTargetType.HeldItem)
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
        AnimationTarget animationTarget = new(entity);

        ValidateEntities();

        if (!mComposers.ContainsKey(animationTarget)) return;

        if (animationTarget.TargetType == AnimationTargetType.EntityFirstPerson || animationTarget.TargetType == AnimationTargetType.EntityImmersiveFirstPerson)
        {
            dt /= 2;
        }

        mApplier.Clear();

        mAnimationFrames.Clear();
        TimeSpan timeSpan = TimeSpan.FromSeconds(dt);
        AnimationFrame composition = mComposers[animationTarget].Compose(timeSpan);

        mApplier.AddAnimation(entity.EntityId, composition);
    }
    public void OnFrameHandler(Vintagestory.API.Common.IAnimator animator, float dt, bool? fp)
    {
        AnimationTarget animationTarget = AnimationTarget.HeldItem(fp);
        
        if (!mComposers.ContainsKey(animationTarget)) return;

        mApplier.Clear();

        mAnimationFrames.Clear();
        TimeSpan timeSpan = TimeSpan.FromSeconds(dt);
        AnimationFrame composition = mComposers[animationTarget].Compose(timeSpan);
        mApplier.AddAnimation(animator, composition);
    }
    public void OnApplyAnimation(ElementPose pose, ref float weight)
    {
        mApplier.ApplyAnimation(pose, ref weight);
    }
    public void OnCalculateWeight(ElementPose pose, ref float weight)
    {
        mApplier.CalculateWeight(pose, ref weight);
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
        foreach (long? entityId in mComposers.Keys.Where((id, _) => id.EntityId != null && mClientApi.World.GetEntityById(id.EntityId.Value) == null).Select(id => id.EntityId))
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
#if DEBUG
            mProvider.Enqueue(mRequests[id]);
#endif
            mRequests.Remove(id);
            return true;
        }

        AnimationRequest? request = mRequests[id].Next();

        if (request == null)
        {
#if DEBUG
            mProvider.Enqueue(mRequests[id]);
#endif
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

    internal sealed class AnimationRequestWithStatus
    {
        private readonly AnimationRequest[] mRequests;
        private int mNextRequestIndex = 0;
        private readonly API.IAnimationManager mManager;

        public bool Synchronize { get; set; }
        public AnimationTarget AnimationTarget { get; set; }

        public AnimationRequestWithStatus(AnimationTarget animationTarget, bool synchronize, AnimationRequest[] requests, API.IAnimationManager manager)
        {
            mRequests = requests;
            Synchronize = synchronize;
            AnimationTarget = animationTarget;
            mManager = manager;
        }

        public bool IsSingleSet() => mRequests.Length == 1 && mRequests[0].Parameters.Action == AnimationPlayerAction.Set;
        public AnimationRequest? Next() => mNextRequestIndex < mRequests.Length ? mRequests[mNextRequestIndex++] : null;
        public AnimationRequest? Last() => mNextRequestIndex < mRequests.Length ? mRequests[mNextRequestIndex] : mRequests[^1];
        public bool Finished() => mNextRequestIndex >= mRequests.Length;
        public void Repeat() => mManager.Run(AnimationTarget, mRequests);
        public override string ToString() => mRequests.Select(request => request.Animation.ToString()).Aggregate((first, second) => $"{first}, {second}");
    }

#if DEBUG
    public static ICoreClientAPI? Api { get; private set; }
#endif

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
            if (!collapsed) composer.SetUpDebugWindow($"{target}");
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

    public bool CalculateWeight(ElementPose pose, ref float weight)
    {
        if (pose == null || !Poses.ContainsKey(pose)) return false;

        (string name, AnimationFrame? composition) = Poses[pose];

        composition.Weight(ref weight, Utils.ToCrc32(name));

        return true;
    }

    public bool ApplyAnimation(ElementPose pose, ref float weight)
    {
        if (pose == null || !Poses.ContainsKey(pose)) return false;

        (string name, AnimationFrame? composition) = Poses[pose];

        composition.Apply(pose, ref weight, Utils.ToCrc32(name));

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
        foreach ((ElementId id, _) in composition.Elements)
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

    public AnimationProvider(ICoreClientAPI api, API.IAnimationManager manager)
    {
        mApi = api;
#if DEBUG
        mManager = manager;
#endif
    }

    public bool Register(AnimationId id, AnimationData data)
    {
        if (data.Shape == null)
        {
            return mAnimationsToConstruct.TryAdd(id, data);
        }
        else
        {
            return mAnimations.TryAdd(id, ConstructAnimation(mApi, id, data, data.Shape));
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
        mConstructedAnimations.Add((id, target), ConstructAnimation(mApi, id, data, data.Shape));
        return mConstructedAnimations[(id, target)];
    }

    private int mConstructedAnimationsCounter = 0;
    private IAnimation ConstructAnimation(ICoreClientAPI api, AnimationId id, AnimationData data, Shape shape)
    {
        Dictionary<uint, Vintagestory.API.Common.Animation> animations = shape.AnimationsByCrc32;
        uint crc32 = Utils.ToCrc32(data.Code);
        if (!animations.ContainsKey(crc32))
        {
            api.Logger.Debug($"[Animation Manager lib] Animation '{data.Code}' was not found in shape. Procedural animation: '{id}'.");
        }

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

        mConstructedAnimationsCounter++;
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
#if DEBUG
    private readonly VSImGui.FixedSizedQueue<AnimationManager.AnimationRequestWithStatus> mLastRequests = new(8);
    private AnimationManager.AnimationRequestWithStatus? mSotredRequest;
    private bool mAnimationEditorToggle = false;
    private int mNewRequestAdded = 0;
    private readonly API.IAnimationManager mManager;
    public void Enqueue(AnimationManager.AnimationRequestWithStatus request)
    {
        if (request.IsSingleSet()) return;
        mLastRequests.Enqueue(request);
        mNewRequestAdded++;
    }
    public void SetUpDebugWindow()
    {
        if (mAnimationEditorToggle) AnimationEditor();

        ImGuiNET.ImGui.Begin("Animation manager");
        if (ImGui.Button($"Show animations editor")) mAnimationEditorToggle = true;
        ImGuiNET.ImGui.Text(string.Format("Registered animations: {0}", mAnimationsToConstruct.Count + mAnimations.Count));
        ImGuiNET.ImGui.Text(string.Format("Registered pre-constructed animations: {0}", mAnimations.Count));
        ImGuiNET.ImGui.Text(string.Format("Registered not pre-constructed animations: {0}", mAnimationsToConstruct.Count));
        ImGuiNET.ImGui.Text(string.Format("Registered constructed animations: {0}", mConstructedAnimations.Count));
        ImGuiNET.ImGui.Text(string.Format($"Constructed animations: {mConstructedAnimationsCounter}"));
        ImGuiNET.ImGui.End();
    }

    private int mCurrentAnimation = 0;
    private int mCurrentRequest = 0;
    private bool mSetCurrentAnimation = false;
    private float mCurrentFrameOverride = 0;
    private bool mOverrideFrame = false;
    private string mAnimationsFilter = "";
    private bool mJsonOutput = false;
    private string mJsonOutputValue = "";
    public void AnimationEditor()
    {
        if (mConstructedAnimations.Count == 0) return;
        if (mCurrentAnimation >= mConstructedAnimations.Count) mCurrentAnimation = mConstructedAnimations.Count - 1;

        string[] animationIds = mConstructedAnimations.Select(value => $"Animation: {value.Key.Item1}, Target: {value.Key.Item2}").ToArray();
        IAnimation[] animations = mConstructedAnimations.Select(value => value.Value).ToArray();
        string[] requests = mLastRequests.Queue.Select(request => request.ToString()).Reverse().ToArray();

        ImGui.Begin($"Animations editor", ref mAnimationEditorToggle);

        if (requests.Length > 0)
        {
            ImGui.SeparatorText("Last requests");
            if (ImGui.Button($"Repeat request##Animations editor")) mLastRequests.Queue.Reverse().ToArray()[mCurrentRequest].Repeat();
            ImGui.SameLine();
            if (ImGui.Button($"Store request##Animations editor")) mSotredRequest = mLastRequests.Queue.Reverse().ToArray()[mCurrentRequest];
            ImGui.SameLine();
            if (mSotredRequest == null) ImGui.BeginDisabled();
            if (ImGui.Button($"Repeat stored request##Animations editor")) mSotredRequest?.Repeat();
            if (mSotredRequest == null) ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button($"Output to JSON##Animations editor"))
            {
                mJsonOutput = true;
                mJsonOutputValue = (animations[mCurrentAnimation] as ISerializable)?.Serialize().ToString() ?? "";
            }
            ImGui.ListBox($"Last requests##Animations editor", ref mCurrentRequest, requests, requests.Length);
        }

        ImGui.SeparatorText("Animations");
        ImGui.Checkbox($"Set current key frame", ref mSetCurrentAnimation);
        ImGui.SameLine();
        ImGui.Checkbox($"Override frame value", ref mOverrideFrame);
        if (!mOverrideFrame) ImGui.BeginDisabled();
        float maxFrame = (animations[mCurrentAnimation] as Animation).TotalFrames - 1;
        bool frameModified = false;
        if (ImGui.SliderFloat($"Frame##Animations editor", ref mCurrentFrameOverride, 0, maxFrame)) frameModified = mOverrideFrame;
        if (!mOverrideFrame) ImGui.EndDisabled();

        ImGui.InputTextWithHint($"Elements filter##Animations editor", "supports wildcards", ref mAnimationsFilter, 100);

        FilterAnimations(StyleEditor.WildCardToRegular(mAnimationsFilter), animationIds, out string[] names, out int[] indexes);
        ImGui.ListBox($"Constructed animations##Animations editor", ref mCurrentAnimation, names, names.Length);
        mCurrentAnimation = indexes.Length <= mCurrentAnimation ? 0 : indexes[mCurrentAnimation];

        ImGui.SeparatorText("Frames");
        bool modified = animations[mCurrentAnimation].Editor($"Animations editor##{animationIds[mCurrentAnimation]}");

        if (modified || mSetCurrentAnimation && mNewRequestAdded > 1 || frameModified) SetAnimationFrame(animations[mCurrentAnimation]);

        ImGui.End();

        if (mJsonOutput)
        {
            ImGui.Begin($"JSON output##Animations editor", ref mJsonOutput, ImGuiWindowFlags.Modal);
            System.Numerics.Vector2 size = ImGui.GetWindowSize();
            size.X -= 8;
            size.Y -= 34;
            ImGui.InputTextMultiline($"##Animations editor", ref mJsonOutputValue, (uint)mJsonOutputValue.Length * 2, size, ImGuiInputTextFlags.ReadOnly);
            ImGui.End();
        }
    }

    public void SetAnimationFrame(IAnimation animation)
    {
        float frame = (animation as Animation).CurrentFrame;
        RunParameters runParams = RunParameters.Set(mOverrideFrame ? mCurrentFrameOverride : frame);
        AnimationTarget target = new(mApi.World.Player.Entity);

        mManager.Run(target, (animation as Animation).Id, runParams);
        mNewRequestAdded = 0;

    }

    private void FilterAnimations(string filter, string[] animationIds, out string[] names, out int[] indexes)
    {

        names = animationIds;
        int count = 0;
        indexes = animationIds.Select(_ => count++).ToArray();

        if (filter == "") return;

        List<string> newNames = new();
        List<int> newIndexes = new();

        for (int index = 0; index < names.Length; index++)
        {
            if (StyleEditor.Match(filter, names[index]))
            {
                if (mCurrentAnimation == index)
                {
                    mCurrentAnimation = newIndexes.Count;
                }
                newIndexes.Add(index);
                newNames.Add(names[index]);
            }
        }

        names = newNames.ToArray();
        indexes = newIndexes.ToArray();
    }

#endif
}
