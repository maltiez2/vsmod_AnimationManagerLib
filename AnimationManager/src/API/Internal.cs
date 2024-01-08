using ProtoBuf;
using System;

namespace AnimationManagerLib.API;

internal interface IAnimationManager
{
    bool Register(AnimationId id, AnimationData animation);

    Guid Run(AnimationTarget animationTarget, params AnimationRequest[] requests);
    Guid Run(AnimationTarget animationTarget, bool synchronize, params AnimationRequest[] requests);
    Guid Run(AnimationTarget animationTarget, Guid runId, params AnimationRequest[] requests);
    Guid Run(AnimationTarget animationTarget, AnimationId animationId, params RunParameters[] parameters);
    Guid Run(AnimationTarget animationTarget, bool synchronize, AnimationId animationId, params RunParameters[] parameters);

    void Stop(Guid runId);
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
internal struct AnimationRunPacket
{
    public Guid RunId { get; set; }
    public AnimationTarget AnimationTarget { get; set; }
    public AnimationRequest[] Requests { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
internal struct AnimationStopPacket
{
    public Guid RunId { get; set; }

    public AnimationStopPacket(Guid runId) => RunId = runId;
}

internal struct AnimationRunMetadata
{
    public AnimationPlayerAction Action { get; set; }
    public TimeSpan Duration { get; set; }
    public float? StartFrame { get; set; }
    public float? TargetFrame { get; set; }
    public ProgressModifierType Modifier { get; set; }

    public AnimationRunMetadata(AnimationRequest request)
    {
        Action = request.Parameters.Action;
        Duration = request.Parameters.Duration;
        StartFrame = request.Parameters.StartFrame;
        TargetFrame = request.Parameters.TargetFrame;
        Modifier = request.Parameters.Modifier;
    }
    public static implicit operator AnimationRunMetadata(AnimationRequest request) => new(request);
}

internal interface IHasDebugWindow
{
    void SetUpDebugWindow(string id);
}

internal interface IWithGuiEditor
{
    bool Editor(string id);
}

internal interface IAnimation : IWithGuiEditor
{
    public AnimationFrame Play(float progress, float? startFrame = null, float? endFrame = null);
    public AnimationFrame Blend(float progress, float? targetFrame, AnimationFrame endFrame);
    public AnimationFrame Blend(float progress, AnimationFrame startFrame, AnimationFrame endFrame);
}

internal interface IAnimator : IHasDebugWindow
{
    enum Status
    {
        Running,
        Stopped,
        Finished
    }
    
    public void Run(AnimationRunMetadata parameters, IAnimation animation);
    public AnimationFrame Calculate(TimeSpan timeElapsed, out Status status);
}

internal interface IComposer : IHasDebugWindow
{
    delegate bool IfRemoveAnimator(bool complete);

    bool Register(AnimationId id, IAnimation animation);
    void Run(AnimationRequest request, IfRemoveAnimator finishCallback);
    void Stop(Category request);
    AnimationFrame Compose(TimeSpan timeElapsed);
}

internal interface ISynchronizer
{
    public delegate void AnimationRunHandler(AnimationRunPacket request);
    public delegate void AnimationStopHandler(AnimationStopPacket request);
    void Init(Vintagestory.API.Common.ICoreAPI api, AnimationRunHandler? runHandler, AnimationStopHandler? stopHandler, string channelName);
    void Sync(AnimationRunPacket request);
    void Sync(AnimationStopPacket request);
}