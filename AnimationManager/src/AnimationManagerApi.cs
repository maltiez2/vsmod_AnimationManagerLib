using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib.API
{
    public interface IAnimationManager : IDisposable
    {
        bool Register(AnimationId id, JsonObject definition);
        bool Register(AnimationId id, AnimationMetaData metaData);
        bool Register(AnimationId id, string playerAnimationCode);
        Guid Run(long entityId, params AnimationRequest[] requests);
        Guid Run(long entityId, bool synchronize, params AnimationRequest[] requests);
        Guid Run(long entityId, Guid runId, params AnimationRequest[] requests);
        void Stop(Guid runId);
    }

    public interface IAnimationManagerProvider
    {
        IAnimationManager GetAnimationManager();
    }



    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationRequest
    {
        public AnimationPlayerAction Action { get; set; }
        public CategoryId Category { get; set; }
        public AnimationId Animation { get; set; }
        public TimeSpan Duration { get; set; }
        public ProgressModifierType Modifier { get; set; }
        public float? StartFrame { get; set; }
        public float? EndFrame { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationRunPacket
    {
        public Guid RunId { get; set; }
        public long EntityId { get; set; }
        public AnimationRequest[] Requests { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationStopPacket
    {
        public Guid RunId { get; set; }
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
        SinQuadratic,
        CosShifted
    }

    public enum BlendingType : byte
    {
        Add,
        AddOnCompose,
        Average,
        AverageOnCompose
    }

    public struct AnimationRunMetadata
    {
        public AnimationPlayerAction Action { get; set; }
        public TimeSpan Duration { get; set; }
        public float? StartFrame { get; set; }
        public float? EndFrame { get; set; }
        public ProgressModifierType Modifier { get; set; }

        public AnimationRunMetadata(AnimationRequest request)
        {
            this.Action = request.Action;
            this.Duration = request.Duration;
            this.StartFrame = request.StartFrame;
            this.EndFrame = request.EndFrame;
            this.Modifier = request.Modifier;
        }
        public static implicit operator AnimationRunMetadata(AnimationRequest request) => new AnimationRunMetadata(request);
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationId
    {
        public uint Hash { get; private set; }

        public AnimationId(string name) => Hash = Utils.ToCrc32(name);
        public AnimationId(uint hash) => Hash = hash;

        public static implicit operator AnimationId(AnimationRequest request) => request.Animation;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct CategoryId
    {
        public uint Hash { get; set; }
        public BlendingType Blending { get; set; }
        public float? Weight { get; set; }

        public CategoryId((string name, BlendingType blending, float? weight) parameters)
        {
            Blending = parameters.blending;
            Hash = Utils.ToCrc32(parameters.name);
            Weight = parameters.weight;
        }
        public CategoryId((uint hash, BlendingType blending, float? weight) parameters)
        {
            Blending = parameters.blending;
            Hash = parameters.hash;
            Weight = parameters.weight;
        }
        
        public static implicit operator CategoryId(AnimationRequest request) => request.Category;
    }

    public struct ComposeRequest
    {
        public long EntityId { get; set; }

        public ComposeRequest(long entityId) => EntityId = entityId;
    }

    public interface IAnimationResult : ICloneable
    {
        IAnimationResult Add(IAnimationResult value);
        IAnimationResult Subtract(IAnimationResult value);
        IAnimationResult Average(IAnimationResult value, float weight, float thisWeight = 1);
        IAnimationResult Lerp(IAnimationResult value, float progress);
        IAnimationResult Identity();
    }

    public interface IAnimation<TAnimationResult> : IDisposable
        where TAnimationResult : IAnimationResult
    {
        public TAnimationResult Play(float progress, float? startFrame = null, float? endFrame = null);
        public TAnimationResult Blend(float progress, float? targetFrame, TAnimationResult endFrame);
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

    public class Composition<TAnimationResult>
        where TAnimationResult : IAnimationResult
    {
        public TAnimationResult ToAdd { get; set; }
        public TAnimationResult ToAverage { get; set; }
        public float Weight { get; set; }

        public Composition(TAnimationResult toAdd, TAnimationResult toAverage, float weight)
        {
            ToAdd = toAdd;
            Weight = weight;
            ToAverage = toAverage;
        }
    }

    public interface IComposer<TAnimationResult> : IDisposable
        where TAnimationResult : IAnimationResult
    {
        delegate bool IfRemoveAnimator();
        
        void Init(ICoreAPI api, TAnimationResult defaultFrame);
        void SetAnimatorType<TAnimator>()
            where TAnimator : IAnimator<TAnimationResult>, new();
        bool Register(AnimationId id, IAnimation<TAnimationResult> animation);
        void Run(AnimationRequest request, IfRemoveAnimator finishCallback);
        void Stop(AnimationRequest request);
        Composition<TAnimationResult> Compose(ComposeRequest request, TimeSpan timeElapsed);
    }

    public interface ISynchronizer : IDisposable
    {
        public delegate void AnimationRunHandler(AnimationRunPacket request);
        public delegate void AnimationStopHandler(AnimationStopPacket request);
        void Init(ICoreAPI api, AnimationRunHandler runHandler, AnimationStopHandler stopHandler, string channelName);
        void Sync(AnimationRunPacket request);
        void Sync(AnimationStopPacket request);
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
        };

        public static ProgressModifier Get(ProgressModifierType id) => Modifiers[id];
        public static bool TryAdd(ProgressModifierType id, ProgressModifier modifier) => Modifiers.TryAdd(id, modifier);
    }

    static public class Utils
    {
        public static uint ToCrc32(string value) => GameMath.Crc32(value.ToLowerInvariant()) & int.MaxValue;

        public static AnimationRequest AnimationRequestFromJson(JsonObject definition)
        {
            var animation = new AnimationId(definition["animation"].AsString());


            return new()
            {
                Action = (AnimationPlayerAction)Enum.Parse(typeof(AnimationPlayerAction), definition["action"].AsString("Set")),
                Category = CategoryIdFromJson(definition["category"]),
                Animation = animation,
                Duration = TimeSpan.FromMilliseconds(definition["duration_ms"].AsFloat()),
                Modifier = (ProgressModifierType)Enum.Parse(typeof(ProgressModifierType), definition["dynamic"].AsString("Linear")),
                StartFrame = definition.KeyExists("startFrame") ? definition["startFrame"].AsFloat() : null,
                EndFrame = definition.KeyExists("endFrame") ? definition["endFrame"].AsFloat() : null
            };
        }

        public static CategoryId CategoryIdFromJson(JsonObject definition)
        {
            string code = definition["code"].AsString();
            BlendingType blending = (BlendingType)Enum.Parse(typeof(BlendingType), definition["blending"].AsString("Add"));
            float? weight = definition.KeyExists("weight") ? definition["weight"].AsFloat() : null;

            return new CategoryId() { Blending = blending, Hash = Utils.ToCrc32(code), Weight = weight };
        }
    }
}