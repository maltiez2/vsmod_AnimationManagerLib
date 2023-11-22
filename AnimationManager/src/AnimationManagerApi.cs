using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib.API
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationRequest
    {
        public AnimationPlayerAction Action { get; set; }
        public CategoryIdentifier Category { get; set; }
        public AnimationIdentifier AnimationId { get; set; }
        public TimeSpan Duration { get; set; }
        public ProgressModifierType Modifier { get; set; }
        public ushort? StartFrame { get; set; }
        public ushort? EndFrame { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationRunPacket
    {
        public Guid RunId { get; set; }
        public long EntityId { get; set; }
        public AnimationRequest[] Requests { get; set; }
    }

    public interface IAnimationManager : IDisposable
    {
        bool Register(AnimationIdentifier id, JsonObject definition);
        bool Register(AnimationIdentifier id, AnimationMetaData metaData);
        bool Register(AnimationIdentifier id, string playerAnimationCode);
        Guid Run(long entityId, params AnimationRequest[] requests);
        Guid Run(long entityId, bool synchronize, params AnimationRequest[] requests);
        void Stop(Guid runId);
    }



    public enum AnimationPlayerAction : byte
    {
        Set,
        EaseIn,
        EaseOut,
        Start,
        Stop,
        Rewind,
        Clear
    }

    public enum ProgressModifierType : byte
    {
        Linear,
        Quadratic,
        Cubic,
        Sqrt,
        Sin,
        SinQuadratic
    }

    public enum BlendingType : byte
    {
        Add,
        Subtract,
        Average,
    }

    public struct AnimationRunMetadata
    {
        public AnimationPlayerAction Action { get; set; }
        public TimeSpan Duration { get; set; }
        public ushort? StartFrame { get; set; }
        public ushort? EndFrame { get; set; }

        public AnimationRunMetadata(AnimationRequest request)
        {
            this.Action = request.Action;
            this.Duration = request.Duration;
            this.StartFrame = request.StartFrame;
            this.EndFrame = request.EndFrame;
        }
        public static implicit operator AnimationRunMetadata(AnimationRequest request) => new AnimationRunMetadata(request);
    }

    public struct AnimationIdentifier
    {
        public uint Hash { get; private set; }

        public AnimationIdentifier(string name) => new AnimationIdentifier() { Hash = Utils.ToCrc32(name) };
        public AnimationIdentifier(uint hash) => new AnimationIdentifier() { Hash = hash };

        public static implicit operator AnimationIdentifier(AnimationRequest request) => request.AnimationId;
    }

    public struct CategoryIdentifier
    {
        public uint Hash { get; private set; }
        public BlendingType Blending { get; private set; }
        public float? Weight { get; private set; }

        public CategoryIdentifier((string name, BlendingType blending, float? Weight) parameters) => new CategoryIdentifier() { Blending = parameters.blending, Hash = Utils.ToCrc32(parameters.name), Weight = parameters.Weight };
        public CategoryIdentifier((uint hash, BlendingType blending, float? Weight) parameters) => new CategoryIdentifier() { Blending = parameters.blending, Hash = parameters.hash, Weight = parameters.Weight };
        
        public static implicit operator CategoryIdentifier(AnimationRequest request) => request.Category;
    }

    public struct ComposeRequest
    {
        public long EntityId { get; set; }
    }

    public interface IAnimationResult : ICloneable
    {
        IAnimationResult Add(IAnimationResult value);
        IAnimationResult Subtract(IAnimationResult value);
        IAnimationResult Average(IAnimationResult value, float weight, float thisWeight = 1);
        IAnimationResult Identity();
    }

    public interface IAnimation<TAnimationResult> : IDisposable
        where TAnimationResult : IAnimationResult
    {
        public TAnimationResult Play(float progress, ushort? startFrame = null, ushort? endFrame = null);
        public TAnimationResult Blend(float progress, ushort? startFrame, TAnimationResult endFrame);
        public TAnimationResult EaseOut(float progress, TAnimationResult endFrame);
    }

    public interface IAnimator<TAnimationResult> : IDisposable
        where TAnimationResult : IAnimationResult
    {
        enum Status
        {
            Running,
            Stopped,
            Finished
        }
        
        public void Init(ICoreAPI api, TAnimationResult defaultFrame);
        public void Run(AnimationRunMetadata parameters, IAnimation<TAnimationResult> animation);
        public TAnimationResult Calculate(TimeSpan timeElapsed, out Status status);
    }

    public interface IAnimationComposer<TAnimationResult> : IDisposable
        where TAnimationResult : IAnimationResult
    {
        delegate bool IfRemoveAnimator();
        
        void Init(ICoreAPI api, TAnimationResult defaultFrame);
        void SetAnimatorType<TAnimator>()
            where TAnimator : IAnimator<TAnimationResult>, new();
        bool Register(AnimationIdentifier id, IAnimation<TAnimationResult> animation);
        void Run(AnimationRequest request, IfRemoveAnimator finishCallback);
        void Stop(AnimationRequest request);
        TAnimationResult Compose(ComposeRequest request, TimeSpan timeElapsed);
    }

    public interface IAnimationSynchronizer : IDisposable
    {
        public delegate void AnimationRequestHandler(AnimationRequest request);
        void Init(ICoreAPI api, AnimationRequestHandler handler, string channelName);
        void Sync(AnimationRequest request);
    }

    static public class ProgressModifiers
    {
        public delegate float ProgressModifier(float progress);

        private readonly static Dictionary<ProgressModifierType, ProgressModifier> Modifiers = new()
        {
            { ProgressModifierType.Linear,       (float progress) => GameMath.Clamp(progress, 0, 1) },
            { ProgressModifierType.Quadratic,    (float progress) => GameMath.Clamp(progress * progress, 0, 1) },
            { ProgressModifierType.Cubic,        (float progress) => GameMath.Clamp(progress * progress * progress, 0, 1) },
            { ProgressModifierType.Sqrt,         (float progress) => GameMath.Sqrt(GameMath.Clamp(progress, 0, 1)) },
            { ProgressModifierType.Sin,          (float progress) => GameMath.Sin(GameMath.Clamp(progress, 0, 1) * 2 / GameMath.PI) },
            { ProgressModifierType.SinQuadratic, (float progress) => GameMath.Sin(GameMath.Clamp(progress * progress, 0, 1) * 2 / GameMath.PI) }
        };

        public static ProgressModifier Get(ProgressModifierType id) => Modifiers[id];
        public static bool TryAdd(ProgressModifierType id, ProgressModifier modifier) => Modifiers.TryAdd(id, modifier);
    }

    static public class Utils
    {
        public static uint ToCrc32(string value) => GameMath.Crc32(value.ToLowerInvariant()) & int.MaxValue;
    }
}