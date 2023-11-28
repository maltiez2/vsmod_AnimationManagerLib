using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib.API
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationRunPacket
    {
        public Guid RunId { get; set; }
        public AnimationTarget AnimationTarget { get; set; }
        public AnimationRequest[] Requests { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationStopPacket
    {
        public Guid RunId { get; set; }
    }

    public struct AnimationRunMetadata
    {
        public AnimationPlayerAction Action { get; set; }
        public TimeSpan Duration { get; set; }
        public float? StartFrame { get; set; }
        public float? TargetFrame { get; set; }
        public ProgressModifierType Modifier { get; set; }

        public AnimationRunMetadata(AnimationRequest request)
        {
            this.Action = request.Parameters.Action;
            this.Duration = request.Parameters.Duration;
            this.StartFrame = request.Parameters.StartFrame;
            this.TargetFrame = request.Parameters.TargetFrame;
            this.Modifier = request.Parameters.Modifier;
        }
        public static implicit operator AnimationRunMetadata(AnimationRequest request) => new AnimationRunMetadata(request);
    }

    public interface IAnimation
    {
        public AnimationFrame Play(float progress, float? startFrame = null, float? endFrame = null);
        public AnimationFrame Blend(float progress, float? targetFrame, AnimationFrame endFrame);
        public AnimationFrame Blend(float progress, AnimationFrame startFrame, AnimationFrame endFrame);
    }

    public interface IAnimator
    {
        enum Status
        {
            Running,
            Stopped,
            Finished
        }
        
        public void Init(CategoryId category);
        public void Run(AnimationRunMetadata parameters, IAnimation animation);
        public AnimationFrame Calculate(TimeSpan timeElapsed, out Status status);
    }

    public interface IComposer
    {
        delegate bool IfRemoveAnimator();

        void SetAnimatorType<TAnimator>()
            where TAnimator : IAnimator;
        bool Register(AnimationId id, IAnimation animation);
        void Run(AnimationRequest request, IfRemoveAnimator finishCallback);
        void Stop(AnimationRequest request);
        AnimationFrame Compose(TimeSpan timeElapsed);
    }

    public interface ISynchronizer
    {
        public delegate void AnimationRunHandler(AnimationRunPacket request);
        public delegate void AnimationStopHandler(AnimationStopPacket request);
        void Init(ICoreAPI api, AnimationRunHandler runHandler, AnimationStopHandler stopHandler, string channelName);
        void Sync(AnimationRunPacket request);
        void Sync(AnimationStopPacket request);
    }

    public enum ProgressModifierType : byte
    {
        Linear,
        Quadratic,
        Cubic,
        Sqrt,
        Sin,
        SinQuadratic,
        CosShifted,
        SqrtSqrt,
        Bounce
    }

    static public class ProgressModifiers
    {
        public delegate float ProgressModifier(float progress);

        private readonly static Dictionary<ProgressModifierType, ProgressModifier> Modifiers = new()
        {
            { ProgressModifierType.Linear,       (float progress) => progress },
            { ProgressModifierType.Quadratic,    (float progress) => progress * progress },
            { ProgressModifierType.Cubic,        (float progress) => progress * progress * progress },
            { ProgressModifierType.Sqrt,         (float progress) => GameMath.Sqrt(progress) },
            { ProgressModifierType.Sin,          (float progress) => GameMath.Sin(progress / 2 * GameMath.PI) },
            { ProgressModifierType.SinQuadratic, (float progress) => GameMath.Sin(progress * progress / 2 * GameMath.PI) },
            { ProgressModifierType.CosShifted,   (float progress) => 0.5f - GameMath.Cos(progress * GameMath.PI) / 2 },
            { ProgressModifierType.SqrtSqrt,     (float progress) => GameMath.Sqrt(GameMath.Sqrt(progress)) },
            { ProgressModifierType.Bounce,       (float progress) => 0.5f - GameMath.Cos(progress * GameMath.PI) / 2 + MathF.Pow(GameMath.Sin(progress * GameMath.PI), 2) * 0.35f }
        };

        public static ProgressModifier Get(ProgressModifierType id) => Modifiers[id];
        public static bool TryAdd(ProgressModifierType id, ProgressModifier modifier) => Modifiers.TryAdd(id, modifier);
    }
}