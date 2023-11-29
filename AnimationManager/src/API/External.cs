using ProtoBuf;
using System;
using System.Diagnostics;
using Vintagestory.API.Common;

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
        bool Register(AnimationId id, string animationCode, Vintagestory.API.Common.Entities.Entity entity);

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
        /// <summary>
        /// Sets animation to <see cref="RunParameters.TargetFrame"/>
        /// </summary>
        Set,
        /// <summary>
        /// Lerp to <see cref="RunParameters.TargetFrame"/> from last played frame
        /// </summary>
        EaseIn,
        /// <summary>
        /// Lerps from last played frame to empty frame
        /// </summary>
        EaseOut,
        /// <summary>
        /// Plays animation from <see cref="RunParameters.StartFrame"/> to <see cref="RunParameters.TargetFrame"/>
        /// </summary>
        Play,
        /// <summary>
        /// Stops animation at last played frame and keeps this frame
        /// </summary>
        Stop,
        /// <summary>
        /// Plays animation from last played frame to <see cref="RunParameters.StartFrame"/>.<br/>
        /// Meant to be used only after <see cref="AnimationPlayerAction.Play"/> action or it but followed by <see cref="AnimationPlayerAction.Stop"/>.<br/>
        /// Requires both <see cref="RunParameters.StartFrame"/> and <see cref="RunParameters.TargetFrame"/> that were used with <see cref="AnimationPlayerAction.Play"/>
        /// </summary>
        Rewind,
        /// <summary>
        /// Sets animation to empty frame
        /// </summary>
        Clear
    }

    public enum AnimationTargetType
    {
        /// <summary>
        /// Entity model, entity is specified by <see cref="Vintagestory.API.Common.Entities.Entity.EntityId"/> in <see cref="AnimationTarget.EntityId"/>
        /// </summary>
        Entity,
        /// <summary>
        /// Item currently held by player in first person view
        /// </summary>
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
            StartFrame = startFrame;
        }

        public RunParameters(AnimationPlayerAction action, TimeSpan duration, float startFrame, float targetFrame, ProgressModifierType modifier = ProgressModifierType.Linear)
        {
            Debug.Assert(
                action == AnimationPlayerAction.Rewind ||
                action == AnimationPlayerAction.Play,
                "Only 'Play' and 'Rewind' actions need both 'StartFrame' and 'TargetFrame'"
                );

            Action = action;
            Duration = duration;
            TargetFrame = targetFrame;
            Modifier = modifier;
            StartFrame = startFrame;
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
            StartFrame = frame;
        }

        public RunParameters(AnimationPlayerAction action, float frame)
        {
            Debug.Assert(
                action == AnimationPlayerAction.Set,
                "Only 'Set' action can be defined only by frame"
                );

            Action = action;
            Duration = TimeSpan.Zero;
            TargetFrame = frame;
            Modifier = ProgressModifierType.Linear;
            StartFrame = frame;
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
            StartFrame = null;
        }

        public RunParameters(AnimationPlayerAction action)
        {
            Debug.Assert(
                action == AnimationPlayerAction.Stop ||
                action == AnimationPlayerAction.Clear,
                "Only 'Stop' and 'Clear' do not need any parameters"
                );

            Action = action;
            Duration = TimeSpan.Zero;
            TargetFrame = null;
            Modifier = ProgressModifierType.Linear;
            StartFrame = null;
        }

        /// <summary>
        /// Sets animation to <paramref name="frame"/>
        /// </summary>
        /// <param name="frame">Frame to set animation to, can be fractional</param>
        /// <returns></returns>
        public static RunParameters Set(float frame)
        {
            return new()
            {
                Action = AnimationPlayerAction.Set,
                Duration = TimeSpan.Zero,
                TargetFrame = frame,
                Modifier = ProgressModifierType.Linear,
                StartFrame = frame
            };
        }

        /// <summary>
        /// Lerps to <paramref name="frame"/> from last played frame
        /// </summary>
        /// <param name="duration">Ease-in duration</param>
        /// <param name="frame">Frame to ease-in to, can be fractional</param>
        /// <param name="modifier">Modifies speed of animation based on its progress</param>
        /// <returns></returns>
        public static RunParameters EaseIn(TimeSpan duration, float frame, ProgressModifierType modifier = ProgressModifierType.Linear)
        {
            return new()
            {
                Action = AnimationPlayerAction.EaseIn,
                Duration = duration,
                TargetFrame = frame,
                Modifier = modifier,
                StartFrame = frame
            };
        }

        /// <summary>
        /// Lerps from last played frame to an empty frame
        /// </summary>
        /// <param name="duration">Total ease-out duration, will be multiplied by previous animation progress to get actual ease-out duration. It keeps movement speed of model elements more consistent.</param>
        /// <param name="modifier">Modifies speed of animation based on its progress</param>
        /// <returns></returns>
        public static RunParameters EaseOut(TimeSpan duration, ProgressModifierType modifier = ProgressModifierType.Linear)
        {
            return new()
            {
                Action = AnimationPlayerAction.EaseOut,
                Duration = duration,
                TargetFrame = null,
                Modifier = modifier,
                StartFrame = null
            };
        }

        /// <summary>
        /// Plays animation from <paramref name="startFrame"/> to <paramref name="targetFrame"/>
        /// </summary>
        /// <param name="duration">Animation duration</param>
        /// <param name="startFrame">Start frame of an animation, can be fractional</param>
        /// <param name="targetFrame">Last frame of an animation, can be fractional</param>
        /// <param name="modifier">Modifies speed of animation based on its progress</param>
        /// <returns></returns>
        public static RunParameters Play(TimeSpan duration, float startFrame, float targetFrame, ProgressModifierType modifier = ProgressModifierType.Linear)
        {
            return new()
            {
                Action = AnimationPlayerAction.Play,
                Duration = duration,
                TargetFrame = targetFrame,
                Modifier = modifier,
                StartFrame = startFrame
            };
        }

        /// <summary>
        /// Stops animation at last played frame and keeps this frame
        /// </summary>
        /// <returns></returns>
        public static RunParameters Stop()
        {
            return new()
            {
                Action = AnimationPlayerAction.Stop,
                Duration = TimeSpan.Zero,
                TargetFrame = null,
                Modifier = ProgressModifierType.Linear,
                StartFrame = null
            };
        }

        /// <summary>
        /// Plays animation from last played frame to <paramref name="startFrame"/>.<br/>
        /// Meant to be used only after <see cref="AnimationPlayerAction.Play"/> action or it but followed by <see cref="AnimationPlayerAction.Stop"/>.<br/>
        /// Requires both <see cref="RunParameters.StartFrame"/> and <see cref="RunParameters.TargetFrame"/> that were used with <see cref="AnimationPlayerAction.Play"/>
        /// </summary>
        /// <param name="duration">Total rewind duration, will be multiplied by previous animation progress to get actual rewind duration. It keeps movement speed of model elements more consistent.</param>
        /// <param name="startFrame">Animation will be reminded to this frame. Meant to be equal to <see cref="RunParameters.StartFrame"/> of previous <see cref="AnimationPlayerAction.Play"/> animation</param>
        /// <param name="targetFrame">Is used to determine first frame of rewind animation. Meant to be equal to <see cref="RunParameters.TargetFrame"/> of previous  <see cref="AnimationPlayerAction.Play"/> animation. </param>
        /// <param name="modifier">Modifies speed of animation based on its progress</param>
        /// <returns></returns>
        public static RunParameters Rewind(TimeSpan duration, float startFrame, float targetFrame, ProgressModifierType modifier = ProgressModifierType.Linear)
        {
            return new()
            {
                Action = AnimationPlayerAction.Rewind,
                Duration = duration,
                TargetFrame = targetFrame,
                Modifier = modifier,
                StartFrame = startFrame
            };
        }

        /// <summary>
        /// Sets animation to empty frame
        /// </summary>
        /// <returns></returns>
        public static RunParameters Clear()
        {
            return new()
            {
                Action = AnimationPlayerAction.Clear,
                Duration = TimeSpan.Zero,
                TargetFrame = null,
                Modifier = ProgressModifierType.Linear,
                StartFrame = null
            };
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
