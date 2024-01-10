using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AnimationManagerLib.API;

public interface IAnimationManagerSystem
{
    bool Register(AnimationId id, AnimationData animation);
    Guid Run(AnimationTarget animationTarget, AnimationSequence sequence, bool synchronize = true);
    void Stop(Guid runId);
    void Suppress(string code);
    void Unsuppress(string code);
}

public interface IAnimatableBehavior
{
    int RegisterAnimation(
        string code,
        string category,
        bool cyclic = false,
        EnumAnimationBlendMode categoryBlendMode = EnumAnimationBlendMode.Add,
        float? categoryWeight = null,
        Dictionary<string, EnumAnimationBlendMode>? elementBlendMode = null,
        Dictionary<string, float>? elementWeight = null
        );

    Guid RunAnimation(int id, params RunParameters[] parameters);
    void StopAnimation(Guid runId);
}

public struct AnimationSequence
{
    public AnimationRequest[] Requests { get; private set; }

    public AnimationSequence(params AnimationRequest[] requests)
    {
        if (requests == null) throw new ArgumentNullException(nameof(requests));
        if (requests.Length == 0) throw new ArgumentException("You need to pass at least one 'AnimationRequest'", nameof(requests));
        Requests = requests;
    }

    public AnimationSequence(AnimationId animationId, params RunParameters[] parameters)
    {
        if (parameters == null) throw new ArgumentNullException(nameof(parameters));
        if (parameters.Length == 0) throw new ArgumentException($"You need to pass at least one 'RunParameters'. Animation id: {animationId}.", nameof(parameters));

        List<AnimationRequest> requests = new();
        foreach (RunParameters parametersSet in parameters)
        {
            requests.Add(new AnimationRequest(animationId, parametersSet));
        }

        Requests = requests.ToArray();
    }

    public static implicit operator AnimationSequence(AnimationRequest request) => new(request);
    public static implicit operator AnimationSequence(AnimationRequest[] requests) => new(requests);
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
    /// Entity, specific one is specified by <see cref="Entity.EntityId"/> in <see cref="AnimationTarget.EntityId"/>
    /// </summary>
    EntityThirdPerson,
    /// <summary>
    /// Player in regular first-person camera mode
    /// </summary>
    EntityFirstPerson,
    /// <summary>
    /// Player in immersive first-person camera mode
    /// </summary>
    EntityImmersiveFirstPerson,
    /// <summary>
    /// Item currently held by player
    /// </summary>
    HeldItem
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct AnimationTarget
{
    public AnimationTargetType TargetType { get; set; }
    public long? EntityId { get; set; }

    private AnimationTarget(AnimationTargetType targetType)
    {
        TargetType = targetType;
        EntityId = null;
    }
    private AnimationTarget(long entityId, AnimationTargetType target)
    {
        TargetType = target;
        EntityId = entityId;
    }
    internal AnimationTarget(Entity entity)
    {
        TargetType = GetTargetType(entity);
        EntityId = entity.EntityId;
    }

    static public AnimationTargetType GetTargetType(Entity entity)
    {
        bool owner = (entity.Api as ICoreClientAPI)?.World.Player.Entity.EntityId == entity.EntityId;
        if (!owner) return AnimationTargetType.EntityThirdPerson;

        bool firstPerson = (entity.Api as ICoreClientAPI)?.World.Player.CameraMode == EnumCameraMode.FirstPerson;
        if (!firstPerson) return AnimationTargetType.EntityThirdPerson;

        bool immersive = (entity.Api as ICoreClientAPI)?.Settings.Bool["immersiveFpMode"] ?? false;
        if (!immersive) return AnimationTargetType.EntityFirstPerson;

        return AnimationTargetType.EntityImmersiveFirstPerson;
    }
    static public AnimationTarget HeldItem() => new(AnimationTargetType.HeldItem);
    static public AnimationTarget Entity(long entityId, AnimationTargetType target) => new(entityId, target);

    public readonly override string ToString()
    {
        return TargetType switch
        {
            AnimationTargetType.EntityThirdPerson => $"Entity: {EntityId}",
            AnimationTargetType.EntityFirstPerson => $"PlayerFirstPerson: {EntityId}",
            AnimationTargetType.EntityImmersiveFirstPerson => $"PlayerImmersiveFirstPerson: {EntityId}",
            AnimationTargetType.HeldItem => $"{TargetType}",
            _ => "<AnimationTarget>"
        };
    }
}

public struct AnimationData
{
    public string Code { get; internal set; }
    public bool Cyclic { get; internal set; }
    public Shape? Shape { get; internal set; }
    public Dictionary<string, float>? ElementWeight { get; internal set; }
    public Dictionary<string, EnumAnimationBlendMode>? ElementBlendMode { get; internal set; }

    static public AnimationData Player(string code, bool cyclic = false) => new(code, cyclic);
    static public AnimationData Entity(string code, Entity entity, bool cyclic = false) => new(code, entity, cyclic);
    static public AnimationData HeldItem(
        string code,
        Shape shape,
        bool cyclic = false,
        Dictionary<string, EnumAnimationBlendMode>? elementBlendMode = null,
        Dictionary<string, float>? elementWeight = null
        ) => new(code, shape, cyclic, elementBlendMode, elementWeight);


    private AnimationData(string code, bool cyclic = false)
    {
        Code = code ?? throw new ArgumentException("Animation code cannot be null", nameof(code));
        Shape = null;
        ElementWeight = null;
        ElementBlendMode = null;
        Cyclic = cyclic;
    }
    private AnimationData(string code, Entity entity, bool cyclic = false)
    {
        if (entity == null) throw new ArgumentException("Entity cannot be null", nameof(entity));

        Code = code ?? throw new ArgumentException("Animation code cannot be null", nameof(code));

        entity.Properties.Client.AnimationsByMetaCode.TryGetValue(Code, out AnimationMetaData? metaData);

        Shape = entity.Properties.Client.LoadedShapeForEntity;
        ElementWeight = metaData?.ElementWeight;
        ElementBlendMode = metaData?.ElementBlendMode;
        Cyclic = cyclic;
    }
    private AnimationData(string code, Shape shape, bool cyclic = false, Dictionary<string, EnumAnimationBlendMode>? elementBlendMode = null, Dictionary<string, float>? elementWeight = null)
    {
        Code = code ?? throw new ArgumentException("Animation code cannot be null", nameof(code));
        Shape = shape ?? throw new ArgumentException("Shape cannot be null", nameof(code));
        Cyclic = cyclic;
        ElementBlendMode = elementBlendMode ?? new();
        ElementWeight = elementWeight ?? new();
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

    public readonly override string ToString() => $"animation: {Animation}, parameters: {Parameters}";
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct RunParameters
{
    public AnimationPlayerAction Action { get; internal set; }
    public TimeSpan Duration { get; internal set; }
    public ProgressModifierType Modifier { get; internal set; }
    public float? TargetFrame { get; internal set; }
    public float? StartFrame { get; internal set; }

    internal RunParameters(AnimationPlayerAction action, TimeSpan duration, ProgressModifierType modifier, float? targetFrame, float? startFrame)
    {
        Action = action;
        Duration = duration;
        TargetFrame = targetFrame;
        Modifier = modifier;
        StartFrame = startFrame;
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
    /// Lerps to <paramref name="frame"/> from last played frame
    /// </summary>
    /// <param name="duration_s">Ease-in duration in seconds</param>
    /// <param name="frame">Frame to ease-in to, can be fractional</param>
    /// <param name="modifier">Modifies speed of animation based on its progress</param>
    /// <returns></returns>
    public static RunParameters EaseIn(float duration_s, float frame, ProgressModifierType modifier = ProgressModifierType.Linear)
    {
        return new()
        {
            Action = AnimationPlayerAction.EaseIn,
            Duration = TimeSpan.FromSeconds(duration_s),
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
    /// Lerps from last played frame to an empty frame
    /// </summary>
    /// <param name="duration_s">Total ease-out duration in seconds, will be multiplied by previous animation progress to get actual ease-out duration. It keeps movement speed of model elements more consistent.</param>
    /// <param name="modifier">Modifies speed of animation based on its progress</param>
    /// <returns></returns>
    public static RunParameters EaseOut(float duration_s, ProgressModifierType modifier = ProgressModifierType.Linear)
    {
        return new()
        {
            Action = AnimationPlayerAction.EaseOut,
            Duration = TimeSpan.FromSeconds(duration_s),
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
    /// Plays animation from <paramref name="startFrame"/> to <paramref name="targetFrame"/>
    /// </summary>
    /// <param name="duration_s">Animation duration in seconds</param>
    /// <param name="startFrame">Start frame of an animation, can be fractional</param>
    /// <param name="targetFrame">Last frame of an animation, can be fractional</param>
    /// <param name="modifier">Modifies speed of animation based on its progress</param>
    /// <returns></returns>
    public static RunParameters Play(float duration_s, float startFrame, float targetFrame, ProgressModifierType modifier = ProgressModifierType.Linear)
    {
        return new()
        {
            Action = AnimationPlayerAction.Play,
            Duration = TimeSpan.FromSeconds(duration_s),
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
    /// Plays animation from last played frame to <paramref name="startFrame"/>.<br/>
    /// Meant to be used only after <see cref="AnimationPlayerAction.Play"/> action or it but followed by <see cref="AnimationPlayerAction.Stop"/>.<br/>
    /// Requires both <see cref="RunParameters.StartFrame"/> and <see cref="RunParameters.TargetFrame"/> that were used with <see cref="AnimationPlayerAction.Play"/>
    /// </summary>
    /// <param name="duration_s">Total rewind duration in seconds, will be multiplied by previous animation progress to get actual rewind duration. It keeps movement speed of model elements more consistent.</param>
    /// <param name="startFrame">Animation will be reminded to this frame. Meant to be equal to <see cref="RunParameters.StartFrame"/> of previous <see cref="AnimationPlayerAction.Play"/> animation</param>
    /// <param name="targetFrame">Is used to determine first frame of rewind animation. Meant to be equal to <see cref="RunParameters.TargetFrame"/> of previous  <see cref="AnimationPlayerAction.Play"/> animation. </param>
    /// <param name="modifier">Modifies speed of animation based on its progress</param>
    /// <returns></returns>
    public static RunParameters Rewind(float duration_s, float startFrame, float targetFrame, ProgressModifierType modifier = ProgressModifierType.Linear)
    {
        return new()
        {
            Action = AnimationPlayerAction.Rewind,
            Duration = TimeSpan.FromSeconds(duration_s),
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

    public readonly override string ToString()
    {
        if (Modifier == ProgressModifierType.Linear)
        {
            return $"{Action}{PrintParameters()}";
        }
        else
        {
            return $"{Action}{PrintParameters()}, modifier: {Modifier}";
        }
    }

    private readonly string PrintParameters()
    {
        return Action switch
        {
            AnimationPlayerAction.Set => string.Format(", frame: {0}", TargetFrame == null ? "null" : TargetFrame?.ToString("0.##")),
            AnimationPlayerAction.EaseIn => string.Format(
                    ", frame: {0}, duration: {1}",
                    TargetFrame == null ? "null" : TargetFrame?.ToString("0.##"),
                    Duration.TotalSeconds.ToString("0.000")
                ),
            AnimationPlayerAction.EaseOut => string.Format(", duration: {0}", Duration.TotalSeconds.ToString("0.000")),
            AnimationPlayerAction.Play => string.Format(
                    ", start: {0}, end: {1}, duration: {2}",
                    StartFrame == null ? "null" : TargetFrame?.ToString("0.##"),
                    TargetFrame == null ? "null" : TargetFrame?.ToString("0.##"),
                    Duration.TotalSeconds.ToString("0.000")
                ),
            AnimationPlayerAction.Stop => "",
            AnimationPlayerAction.Clear => "",
            AnimationPlayerAction.Rewind => string.Format(
                    ", start: {0}, end: {1}, duration: {2}",
                    StartFrame == null ? "null" : TargetFrame?.ToString("0.##"),
                    TargetFrame == null ? "null" : TargetFrame?.ToString("0.##"),
                    Duration.TotalSeconds.ToString("0.000")
                ),
            _ => ""
        };
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct AnimationId
{
    public int Hash { get; private set; }
    public Category Category { get; private set; }

    private readonly string DebugName;

    public AnimationId(Category category, string name)
    {
        Category = category;
        DebugName = name ?? throw new ArgumentException("Animation code cannot be null", nameof(name));
        Hash = (int)Utils.ToCrc32(name);
        Hash = (int)Utils.ToCrc32($"{Hash}{Category.Hash}");
    }
    public AnimationId(string category, string animation, EnumAnimationBlendMode blendingType = EnumAnimationBlendMode.Add, float? weight = null)
    {
        Category = new Category(category ?? throw new ArgumentException("Category name cannot be null", nameof(category)), blendingType, weight);
        DebugName = animation ?? throw new ArgumentException("Animation code cannot be null", nameof(animation));
        Hash = (int)Utils.ToCrc32(animation);
        Hash = (int)Utils.ToCrc32($"{Hash}{Category.Hash}");
    }

    public static implicit operator AnimationId(AnimationRequest request) => request.Animation;

    public readonly string GetName() => DebugName;
    public readonly override string ToString() => $"{DebugName}, category: {Category}";
    public readonly override int GetHashCode() => Hash;
    public readonly override bool Equals([NotNullWhen(true)] object? obj) => obj?.GetHashCode() == Hash;
    public static bool operator ==(AnimationId left, AnimationId right) => left.Equals(right);
    public static bool operator !=(AnimationId left, AnimationId right) => !(left == right);
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct Category
{
    public int Hash { get; private set; }
    public EnumAnimationBlendMode Blending { get; private set; }
    public float? Weight { get; private set; }

    private readonly string mDebugName;

    public Category(string name, EnumAnimationBlendMode blending = EnumAnimationBlendMode.Add, float? weight = null)
    {
        if (name == null) throw new ArgumentNullException(nameof(name), "Category name cannot be null");

        Blending = blending;
        Hash = (int)Utils.ToCrc32($"{name}{blending}{weight}");
        Weight = weight;
        mDebugName = name;
    }

    public static implicit operator Category(AnimationRequest request) => request.Animation.Category;

    public override readonly string ToString()
    {
        if (Blending == EnumAnimationBlendMode.Add)
        {
            return $"{mDebugName} ({Blending})";
        }
        else
        {
            return string.Format("{0} ({1}: {2})", mDebugName, Blending, Weight == null ? "null" : Weight.Value.ToString("#.###"));
        }
    }

    public readonly override int GetHashCode() => Hash;
    public readonly override bool Equals([NotNullWhen(true)] object? obj) => obj?.GetHashCode() == Hash;
    public static bool operator ==(Category left, Category right) => left.Equals(right);
    public static bool operator !=(Category left, Category right) => !(left == right);
}
