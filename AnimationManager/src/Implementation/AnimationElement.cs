using AnimationManagerLib.API;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib
{
    internal enum ElementType
    {
        translateX, translateY, translateZ,
        degX, degY, degZ
    }

    internal struct ElementId
    {
        public uint ElementNameHash { get; set; }
        public ElementType ElementType { get; set; }
        
        private readonly string mDebugName;
        private readonly int mHash;

        public ElementId(string name, ElementType elementType)
        {
            ElementNameHash = Utils.ToCrc32(name);
            mHash = (int)Utils.ToCrc32($"{name}{elementType}");
            mDebugName = name;
            ElementType = elementType;
        }

        public readonly override string ToString() => $"{mDebugName}, type: {ElementType}";
        public readonly override int GetHashCode() => mHash;
        public readonly override bool Equals([NotNullWhen(true)] object? obj) => obj?.GetHashCode() == mHash;
        public static bool operator ==(ElementId left, ElementId right) => left.Equals(right);
        public static bool operator !=(ElementId left, ElementId right) => !(left == right);
    }

    internal struct AnimationElement
    {
        public WeightedValue? Value { get; set; }
        public ElementId Id { get; set; }
        public bool ShortestAngularDistance { get; set; } = false;

        public AnimationElement(ElementId id) => Id = id;
        public AnimationElement(ElementId id, WeightedValue value)
        {
            Id = id;
            Value = value;
        }
        public AnimationElement(ElementId id, WeightedValue value, bool shortestAngularDistance)
        {
            Id = id;
            Value = value;
            ShortestAngularDistance = shortestAngularDistance;
        }

        public readonly void Add(ElementPose pose)
        {
            if (Value == null) return;

            switch (Id.ElementType)
            {
                case ElementType.translateX:
                    pose.translateX += Value.Value.Value;
                    break;
                case ElementType.translateY:
                    pose.translateY += Value.Value.Value;
                    break;
                case ElementType.translateZ:
                    pose.translateZ += Value.Value.Value;
                    break;
                case ElementType.degX:
                    pose.degX += Value.Value.Value;
                    break;
                case ElementType.degY:
                    pose.degY += Value.Value.Value;
                    break;
                case ElementType.degZ:
                    pose.degZ += Value.Value.Value;
                    break;
            }
        }
        public readonly void Average(ElementPose pose, float poseWeight = 1)
        {
            if (Value == null) return;

            switch (Id.ElementType)
            {
                case ElementType.translateX:
                    pose.translateX = Average(Value.Value, pose.translateX, poseWeight);
                    break;
                case ElementType.translateY:
                    pose.translateY = Average(Value.Value, pose.translateY, poseWeight);
                    break;
                case ElementType.translateZ:
                    pose.translateZ = Average(Value.Value, pose.translateZ, poseWeight);
                    break;
                case ElementType.degX:
                    pose.degX = Average(Value.Value, pose.degX, poseWeight);
                    break;
                case ElementType.degY:
                    pose.degY = Average(Value.Value, pose.degY, poseWeight);
                    break;
                case ElementType.degZ:
                    pose.degZ = Average(Value.Value, pose.degZ, poseWeight);
                    break;
            }
        }
        private static float Average(WeightedValue thisValue, float value, float weight)
        {
            return (thisValue.Value * thisValue.Weight + value * weight) / (thisValue.Weight + weight);
        }

        static public AnimationElement Sum(AnimationElement first, AnimationElement second)
        {
            Debug.Assert(first.Id.ElementNameHash == second.Id.ElementNameHash && second.Id.ElementType == first.Id.ElementType);

            return new()
            {
                Value = WeightedValue.Sum(first.Value, second.Value),
                Id = first.Id,
                ShortestAngularDistance = first.ShortestAngularDistance || second.ShortestAngularDistance
            };
        }
        static public AnimationElement Sum(AnimationElement element, float? value, float defaultWeight = 1)
        {
            if (element.Value == null)
            {
                if (value == null)
                {
                    return new()
                    {
                        Value = null,
                        Id = element.Id,
                        ShortestAngularDistance = element.ShortestAngularDistance
                    };
                }
                else
                {
                    return new()
                    {
                        Value = new(value.Value, defaultWeight),
                        Id = element.Id,
                        ShortestAngularDistance = element.ShortestAngularDistance
                    };
                }
            }
            else
            {
                return new()
                {
                    Value = new(element.Value.Value.Value + value ?? 0, element.Value.Value.Weight),
                    Id = element.Id,
                    ShortestAngularDistance = element.ShortestAngularDistance
                };
            }
        }
        static public AnimationElement Average(AnimationElement first, AnimationElement second)
        {
            Debug.Assert(first.Id.ElementNameHash == second.Id.ElementNameHash && second.Id.ElementType == first.Id.ElementType);

            return new()
            {
                Value = WeightedValue.Average(first.Value, second.Value),
                Id = first.Id,
                ShortestAngularDistance = first.ShortestAngularDistance || second.ShortestAngularDistance
            };
        }
        static public AnimationElement Lerp(AnimationElement from, AnimationElement to, float progress, bool weighted = true)
        {
            Debug.Assert(from.Id.ElementNameHash == to.Id.ElementNameHash && from.Id.ElementType == to.Id.ElementType);

            if (from.ShortestAngularDistance || to.ShortestAngularDistance)
            {
                return new()
                {
                    Value = WeightedValue.ShortestLerp(from.Value, to.Value, progress, 360, weighted),
                    Id = from.Id,
                    ShortestAngularDistance = from.ShortestAngularDistance || to.ShortestAngularDistance
                };
            }

            return new()
            {
                Value = WeightedValue.Lerp(from.Value, to.Value, progress, weighted),
                Id = from.Id,
                ShortestAngularDistance = from.ShortestAngularDistance || to.ShortestAngularDistance
            };
        }

        static public AnimationElement CircularLerp(AnimationElement from, AnimationElement to, float progress, bool weighted = true)
        {
            Debug.Assert(from.Id.ElementNameHash == to.Id.ElementNameHash && from.Id.ElementType == to.Id.ElementType);

            return new()
            {
                Value = WeightedValue.CircularLerp(from.Value, to.Value, progress, 360, weighted),
                Id = from.Id,
                ShortestAngularDistance = from.ShortestAngularDistance || to.ShortestAngularDistance
            };
        }

        public readonly override string ToString() => $"id: {Id}, value: {Value}";
    }

    internal struct WeightedValue
    {
        public float Value { get; set; }
        public float Weight { get; set; }

        public WeightedValue(float value, float weight)
        {
            Value = value;
            Weight = weight;
        }

        static public WeightedValue? Sum(WeightedValue? first, WeightedValue? second)
        {
            if (first == null) return second;
            if (second == null) return first;

            return new()
            {
                Value = first.Value.Value + second.Value.Value,
                Weight = first.Value.Weight + second.Value.Weight
            };
        }
        static public WeightedValue? Average(WeightedValue? first, WeightedValue? second)
        {
            if (first == null) return second;
            if (second == null) return first;

            return new()
            {
                Value = (first.Value.Value * first.Value.Weight + second.Value.Value * second.Value.Weight) / (first.Value.Weight + second.Value.Weight),
                Weight = first.Value.Weight + second.Value.Weight
            };
        }
        static public WeightedValue? Lerp(WeightedValue? from, WeightedValue? to, float progress, bool weighted = true)
        {
            return (from, to) switch
            {
                (null, null) => null,
                (null, _) => new()
                {
                    Value = to.Value.Value * progress,
                    Weight = to.Value.Weight * (weighted ? progress : 1)
                },
                (_, null) => new()
                {
                    Value = from.Value.Value * (1 - progress),
                    Weight = from.Value.Weight * (1 - (weighted ? progress : 0))
                },
                _ => new()
                {
                    Value = from.Value.Value * (1 - progress) + to.Value.Value * progress,
                    Weight = from.Value.Weight * (1 - progress) + to.Value.Weight * progress
                }
            };
        }
        static public WeightedValue? ShortestLerp(WeightedValue? from, WeightedValue? to, float progress, float max, bool weighted = true)
        {
            return (from, to) switch
            {
                (null, null) => null,
                (null, _) => new()
                {
                    Value = to.Value.Value * progress,
                    Weight = to.Value.Weight * (weighted ? progress : 1)
                },
                (_, null) => new()
                {
                    Value = CalcResultValue(from, to, progress, max),
                    Weight = from.Value.Weight * (1 - (weighted ? progress : 0))
                },
                _ => new()
                {
                    Value = CalcResultValue(from, to, progress, max),
                    Weight = from.Value.Weight * (1 - progress) + to.Value.Weight * progress
                }
            };
        }

        static private float CalcResultValue(WeightedValue? from, WeightedValue? to, float progress, float max)
        {
            float fromValue = (from?.Value ?? 0) % max;
            float toValue = (to?.Value ?? 0) % max;
            float distance = GameMath.AngleDegDistance(fromValue, toValue) * progress;
            return fromValue + distance;
        }

        static public WeightedValue? CircularLerp(WeightedValue? from, WeightedValue? to, float progress, float max, bool weighted = true)
        {
            return (from, to) switch
            {
                (null, null) => null,
                (null, _) => new()
                {
                    Value = to.Value.Value * progress,
                    Weight = to.Value.Weight * (weighted ? progress : 1)
                },
                (_, null) => new()
                {
                    Value = CircCalcResultValue(from, to, progress, max),
                    Weight = from.Value.Weight * (1 - (weighted ? progress : 0))
                },
                _ => new()
                {
                    Value = CircCalcResultValue(from, to, progress, max),
                    Weight = from.Value.Weight * (1 - progress) + to.Value.Weight * progress
                }
            };
        }

        static private float CircCalcResultValue(WeightedValue? from, WeightedValue? to, float progress, float max)
        {
            float fromValue = (from?.Value ?? 0) % max;
            float toValue = (to?.Value ?? 0) % max;

            if (fromValue <= toValue + 1E-5) return fromValue + (toValue - fromValue) * progress;

            float distance = (toValue + max - fromValue) * progress;

            if (distance > max - fromValue)
            {
                return toValue + distance - fromValue;
            }
            else
            {
                return fromValue + distance;
            }
        }

        public readonly override string ToString() => string.Format("{0}, weight: {1}", Value, Weight);
    }
}