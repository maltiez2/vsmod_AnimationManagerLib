using ProtoBuf;
using System;
using System.Diagnostics;
using System.Xml.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AnimationManagerLib.API
{
    public interface IAnimationManager
    {
        /// <summary>
        /// Registers animation of player model, should be called on all clients in order to synchronize animations
        /// </summary>
        /// <param name="id">Used in <see cref="Run"/> in <see cref="AnimationRequest"/> to specify animation to play</param>
        /// <param name="animationCode">Animation code ( example: <c>"axechop"</c> )</param>
        /// <returns><c>false</c> if animation already registered</returns>
        bool Register(AnimationId id, string animationCode);

        /// <summary>
        /// Registers animation of an entity model, should be called on all clients in order to synchronize animations
        /// </summary>
        /// <param name="id">Used in <see cref="Run"/> in <see cref="AnimationRequest"/> to specify animation to play</param>
        /// <param name="animationCode">Animation code ( example: <c>"axechop"</c> )</param>
        /// <param name="entity">Entity that has specified animation</param>
        /// <returns><c>false</c> if animation already registered</returns>
        bool Register(AnimationId id, string animationCode, Entity entity);

        /// <summary>
        /// Registers animation for an item, should be called on all clients in order to synchronize animations
        /// </summary>
        /// <param name="id">>Used in <see cref="Run"/> in <see cref="AnimationRequest"/> to specify animation to play</param>
        /// <param name="animationCode">Animation code ( example: <c>"axechop"</c> )</param>
        /// <param name="shape">Item shape, can be acquired from <see cref="CollectibleBehaviors.Animatable"/></param>
        /// <param name="metaData">Currently only <see cref="AnimationMetaData.ElementBlendMode"/> and <see cref="AnimationMetaData.ElementWeight"/> fields are used from meta data, all other are igonred</param>
        /// <returns><c>false</c> if animation already registered</returns>
        bool Register(AnimationId id, string animationCode, Shape shape, AnimationMetaData metaData);

        /// <summary>
        /// Starts the animation sequence, synchronized between clients, unless <see cref="AnimationTarget.TargetType"/> is <see cref="AnimationTargetType.HeldItemFp"/>
        /// </summary>
        /// <param name="animationTarget">Specifies what would be animated</param>
        /// <param name="requests">Sequence of animations bonded to run parameters. Will be played one after another.</param>
        /// <returns>Unique identifier that is used to stop animation sequence with <see cref="Stop"/></returns>
        Guid Run(AnimationTarget animationTarget, params AnimationRequest[] requests);

        /// <summary>
        /// Starts the animation sequence
        /// </summary>
        /// <param name="animationTarget">Specifies what would be animated</param>
        /// <param name="synchronize">If <c>true</c> animations will be synchronized between clients, unless <see cref="AnimationTarget.TargetType"/> is <see cref="AnimationTargetType.HeldItemFp"/> </param>
        /// <param name="requests">Sequence of animations bonded to run parameters. Will be played one after another.</param>
        /// <returns>Unique identifier that is used to stop animation sequence with <see cref="Stop"/></returns>
        Guid Run(AnimationTarget animationTarget, bool synchronize, params AnimationRequest[] requests);

        /// <summary>
        /// Used by synchronizer, not synchronized.
        /// </summary>
        /// <param name="animationTarget">Specifies what would be animated</param>
        /// <param name="runId">Unique identifier, should be the same between clients</param>
        /// <param name="requests">Sequence of animations bonded to run parameters. Will be played one after another.</param>
        /// <returns>Unique identifier that is used to stop animation sequence with <see cref="Stop"/></returns>
        Guid Run(AnimationTarget animationTarget, Guid runId, params AnimationRequest[] requests);

        /// <summary>
        /// Stops animation sequence with specified id provided by <see cref="Run"/>. Synchronized.
        /// </summary>
        /// <param name="runId">Animation sequence id provided by <see cref="Run"/></param>
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
        Rewind, // Play animation from last frame to TargetFrame (needs StartFrame to calculate last frame)
        Clear // Set to empty frame
    }

    public enum AnimationTargetType
    {
        Entity,
        HeldItemFp
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationTarget
    {
        public AnimationTargetType TargetType { get; set; }
        public long? EntityId { get; set; }

        public AnimationTarget(AnimationTargetType targetType)
        {
            Debug.Assert(targetType != AnimationTargetType.Entity, "'Entity' target requires entity id");

            TargetType = targetType;
            EntityId = null;
        }
        public AnimationTarget(AnimationTargetType targetType, long? entityId)
        {
            Debug.Assert(targetType != AnimationTargetType.Entity || entityId != null, "'Entity' target requires not null entity id");
            
            TargetType = targetType;
            EntityId = entityId;
        }
        public AnimationTarget(long entityId)
        {
            TargetType = AnimationTargetType.Entity;
            EntityId = entityId;
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct AnimationRequest
    {
        public AnimationId Animation { get; set; }
        public RunParameters Parameters { get; set; }

        public AnimationRequest(AnimationId animationId, RunParameters parameters)
        {
            Animation = animationId;
            Parameters = parameters;
        }

        public override string ToString() => string.Format("AnimationRequest: ({0}) \t|  {1}", Animation, Parameters);
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct RunParameters
    {
        public AnimationPlayerAction Action { get; set; }
        public TimeSpan Duration { get; set; }
        public ProgressModifierType Modifier { get; set; }
        public float? TargetFrame { get; set; }
        public float? StartFrame { get; set; }

        public RunParameters(AnimationPlayerAction action, TimeSpan duration, ProgressModifierType modifier, float? targetFrame, float? startFrame)
        {
            Action = action;
            Duration = duration;
            TargetFrame = targetFrame;
            Modifier = modifier;
            TargetFrame = startFrame;
        }

        public RunParameters(AnimationPlayerAction action, TimeSpan duration, float startFrame, float targetFrame, ProgressModifierType modifier = ProgressModifierType.Linear)
        {
            Debug.Assert(
                action == AnimationPlayerAction.Rewind ||
                action == AnimationPlayerAction.Start,
                "Only 'Start' and 'Rewind' actions need both 'StartFrame' and 'TargetFrame'"
                );

            Action = action;
            Duration = duration;
            TargetFrame = targetFrame;
            Modifier = modifier;
            TargetFrame = startFrame;
        }

        public RunParameters(AnimationPlayerAction action, TimeSpan duration, float frame, ProgressModifierType modifier = ProgressModifierType.Linear)
        {
            Debug.Assert(
                action == AnimationPlayerAction.Set ||
                action == AnimationPlayerAction.EaseIn,
                "Only 'Set' and 'EaseIn' actions need only single frame to be specified"
                );

            Action = action;
            Duration = duration;
            TargetFrame = frame;
            Modifier = modifier;
            TargetFrame = frame;
        }

        public RunParameters(AnimationPlayerAction action, TimeSpan duration, ProgressModifierType modifier = ProgressModifierType.Linear)
        {
            Debug.Assert(
                action == AnimationPlayerAction.EaseOut ||
                action == AnimationPlayerAction.Stop ||
                action == AnimationPlayerAction.Clear,
                "Only 'EaseOut', 'Stop' and 'Clear' actions do not need frame to be specified"
                );

            Action = action;
            Duration = duration;
            TargetFrame = null;
            Modifier = modifier;
            TargetFrame = null;
        }

        public static implicit operator RunParameters(AnimationRequest request) => request.Parameters;

        public override string ToString() => string.Format("RunParameters: {0} for '{1} ms' ({2}): {3} -> {4}", Action, Duration.TotalMilliseconds, Modifier, StartFrame != null ? StartFrame : "null", TargetFrame != null ? TargetFrame : "null");
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
        public AnimationId(string category, string animation, EnumAnimationBlendMode blendingType = EnumAnimationBlendMode.Add, float? weight = null)
        {
            Hash = Utils.ToCrc32(animation);
            Category = new CategoryId(category, blendingType, weight);
        }

        public static implicit operator AnimationId(AnimationRequest request) => request.Animation;

        public override string ToString() => string.Format("AnimationId: {0} {1}", Hash, Category);
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct CategoryId
    {
        public uint Hash { get; set; }
        public EnumAnimationBlendMode Blending { get; set; }
        public float? Weight { get; set; }

        public CategoryId(string name, EnumAnimationBlendMode blending = EnumAnimationBlendMode.Add, float? weight = null)
        {
            Blending = blending;
            Hash = Utils.ToCrc32(name);
            Weight = weight;
        }
        public CategoryId(uint hash, EnumAnimationBlendMode blending = EnumAnimationBlendMode.Add, float? weight = null)
        {
            Blending = blending;
            Hash = hash;
            Weight = weight;
        }

        public static implicit operator CategoryId(AnimationRequest request) => request.Animation.Category;

        public override string ToString() => string.Format("CategoryId: {0} ({1}: {2})", Hash, Blending, Weight == null ? "null" : Weight);
    }
}
