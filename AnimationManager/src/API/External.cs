using ProtoBuf;
using System;
using Vintagestory.API.Common;

namespace AnimationManagerLib.API
{
    public interface IAnimationManager : IDisposable
    {
        bool Register(AnimationId id, string animationCode);
        Guid Run(long entityId, params AnimationRequest[] requests);
        Guid Run(long entityId, bool synchronize, params AnimationRequest[] requests);
        Guid Run(long entityId, Guid runId, params AnimationRequest[] requests);
        void Stop(Guid runId);
    }

    public interface IAnimationManagerProvider
    {
        IAnimationManager GetAnimationManager();
        ISynchronizer GetSynchronizer();
    }

    public enum AnimationPlayerAction : byte
    {
        Set, // Set to TargetFrame
        EaseIn, // Lerp from last frame to TargetFrame
        EaseOut, // Lerp from last frame to empty frame
        Start, // Play animation from StartFrame to TargetFrame
        Stop, // Stop animation at last frame and keep it
        Rewind, // Play animation from last frame to TargetFrame
        Clear // Set to empty frame
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationRequest
    {
        public AnimationId Animation { get; set; }
        public RunParameters Parameters { get; set; }

        public override string ToString() => string.Format("Animation ({0}) \t|  {1}", Animation, Parameters);
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct RunParameters
    {
        public AnimationPlayerAction Action { get; set; }
        public TimeSpan Duration { get; set; }
        public ProgressModifierType Modifier { get; set; }
        public float? TargetFrame { get; set; }
        public float? StartFrame { get; set; }

        public static implicit operator RunParameters(AnimationRequest request) => request.Parameters;

        public override string ToString() => string.Format("{0} for '{1} ms' ({2}): {3} -> {4}", Action, Duration.TotalMilliseconds, Modifier, StartFrame != null ? StartFrame : "null", TargetFrame != null ? TargetFrame : "null");
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationId
    {
        public uint Hash { get; private set; }
        public CategoryId Category { get; private set; }

        public AnimationId(CategoryId category, string name)
        {
            Hash = Utils.ToCrc32(name);
            Category = category;
        }
        public AnimationId(CategoryId category, uint hash)
        {
            Hash = hash;
            Category = category;
        }

        public static implicit operator AnimationId(AnimationRequest request) => request.Animation;

        public override string ToString() => string.Format("animation: {0} {1}", Hash, Category);
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct CategoryId
    {
        public uint Hash { get; set; }
        public EnumAnimationBlendMode Blending { get; set; }
        public float? Weight { get; set; }

        public CategoryId(string name, EnumAnimationBlendMode blending, float? weight)
        {
            Blending = blending;
            Hash = Utils.ToCrc32(name);
            Weight = weight;
        }
        public CategoryId(uint hash, EnumAnimationBlendMode blending, float? weight)
        {
            Blending = blending;
            Hash = hash;
            Weight = weight;
        }

        public static implicit operator CategoryId(AnimationRequest request) => request.Animation.Category;

        public override string ToString() => string.Format("category: {0} ({1}: {2})", Hash, Blending, Weight == null ? "null" : Weight);
    }
}
