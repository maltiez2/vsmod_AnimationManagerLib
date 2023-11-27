using ProtoBuf;
using System;
using Vintagestory.API.Common;

namespace AnimationManagerLib.API
{
    public interface IAnimationManager : IDisposable
    {
        bool Register(AnimationId id, string playerAnimationCode);
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
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct CategoryId
    {
        public uint Hash { get; set; }
        public EnumAnimationBlendMode Blending { get; set; }
        public float? Weight { get; set; }

        public CategoryId((string name, EnumAnimationBlendMode blending, float? weight) parameters)
        {
            Blending = parameters.blending;
            Hash = Utils.ToCrc32(parameters.name);
            Weight = parameters.weight;
        }
        public CategoryId((uint hash, EnumAnimationBlendMode blending, float? weight) parameters)
        {
            Blending = parameters.blending;
            Hash = parameters.hash;
            Weight = parameters.weight;
        }

        public static implicit operator CategoryId(AnimationRequest request) => request.Category;
    }
}
