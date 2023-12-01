﻿using AnimationManagerLib.API;
using System.Diagnostics;
using Vintagestory.API.Common;

namespace AnimationManagerLib
{
    public enum ElementType
    {
        translateX, translateY, translateZ,
        degX, degY, degZ
    }

    public struct ElementId
    {
        public uint ElementNameHash { get; set; }
        public ElementType ElementType { get; set; }

        public ElementId(string name, ElementType elementType)
        {
            ElementNameHash = Utils.ToCrc32(name);
            ElementType = elementType;
        }
        public ElementId(uint nameHash, ElementType elementType)
        {
            ElementNameHash = nameHash;
            ElementType = elementType;
        }

        public override string ToString() => string.Format("({0}){1}", ElementNameHash, ElementType);
    }

    public struct AnimationElement
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

        public void Add(ElementPose pose)
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
        public void Average(ElementPose pose, float poseWeight = 1)
        {
            if (Value == null) return;

            switch (Id.ElementType)
            {
                case ElementType.translateX:
                    pose.translateX = Average(pose.translateX, poseWeight);
                    break;
                case ElementType.translateY:
                    pose.translateY = Average(pose.translateY, poseWeight);
                    break;
                case ElementType.translateZ:
                    pose.translateZ = Average(pose.translateZ, poseWeight);
                    break;
                case ElementType.degX:
                    pose.degX = Average(pose.degX, poseWeight);
                    break;
                case ElementType.degY:
                    pose.degY = Average(pose.degY, poseWeight);
                    break;
                case ElementType.degZ:
                    pose.degZ = Average(pose.degZ, poseWeight);
                    break;
            }
        }
        private float Average(float value, float weight)
        {
            return (Value.Value.Value * Value.Value.Weight + value * weight) / (Value.Value.Weight + weight);
        }

        static public AnimationElement Sum(AnimationElement first, AnimationElement second)
        {
            Debug.Assert(first.Id.ElementNameHash == second.Id.ElementNameHash && second.Id.ElementType == first.Id.ElementType);

            return new()
            {
                Value = WeightedValue.Sum(first.Value, second.Value),
                Id = first.Id
            };
        }
        static public AnimationElement Sum(AnimationElement element, float? value, float defaultWeight = 1)
        {
            if (element.Value == null && value == null)
            {
                return new()
                {
                    Value = null,
                    Id = element.Id
                };
            }

            if (element.Value == null && value != null)
            {
                return new()
                {
                    Value = new(value.Value, defaultWeight),
                    Id = element.Id
                };
            }

            return new()
            {
                Value = new(element.Value.Value.Value + value ?? 0, element.Value.Value.Weight),
                Id = element.Id
            };
        }
        static public AnimationElement Average(AnimationElement first, AnimationElement second)
        {
            Debug.Assert(first.Id.ElementNameHash == second.Id.ElementNameHash && second.Id.ElementType == first.Id.ElementType);

            return new()
            {
                Value = WeightedValue.Average(first.Value, second.Value),
                Id = first.Id
            };
        }
        static public AnimationElement Lerp(AnimationElement from, AnimationElement to, float progress, bool weighted = true)
        {
            Debug.Assert(from.Id.ElementNameHash == to.Id.ElementNameHash && from.Id.ElementType == to.Id.ElementType);

            if (from.ShortestAngularDistance || to.ShortestAngularDistance)
            {
                return new()
                {
                    Value = WeightedValue.CircularLerp(from.Value, to.Value, progress, 360, weighted),
                    Id = from.Id
                };
            }

            return new()
            {
                Value = WeightedValue.Lerp(from.Value, to.Value, progress, weighted),
                Id = from.Id
            };
        }

        public override string ToString() => string.Format("(id: {0}, value: {1})", Value, Value);
    }

    public struct WeightedValue
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
            if (from == null) return new()
            {
                Value = to.Value.Value * progress,
                Weight = to.Value.Weight * (weighted ? progress : 1)
            };

            if (to == null) return new()
            {
                Value = from.Value.Value * (1 - progress),
                Weight = from.Value.Weight * (1 - (weighted ? progress : 0))
            };

            return new()
            {
                Value = from.Value.Value * (1 - progress) + to.Value.Value * progress,
                Weight = from.Value.Weight * (1 - progress) + to.Value.Weight * progress
            };
        }
        static public WeightedValue? CircularLerp(WeightedValue? from, WeightedValue? to, float progress, float max, bool weighted = true)
        {
            if (from == null) return new()
            {
                Value = to.Value.Value * progress,
                Weight = to.Value.Weight * (weighted ? progress : 1)
            };

            if (to == null) return new()
            {
                Value = CalcResultValue(from, to, progress, max),
                Weight = from.Value.Weight * (1 - (weighted ? progress : 0))
            };

            return new()
            {
                Value = CalcResultValue(from, to, progress, max),
                Weight = from.Value.Weight * (1 - progress) + to.Value.Weight * progress
            };
        }

        static private float CalcResultValue(WeightedValue? from, WeightedValue? to, float progress, float max)
        {
            float fromValue = from.Value.Value % max;
            float toValue = (to?.Value ?? 0) % max;
            
            if (fromValue < toValue) return fromValue + (toValue - fromValue) * progress;

            float distance = (toValue + max - fromValue) * progress;

            if (distance < max - fromValue)
            {
                return fromValue + distance;
            }
            else
            {
                return toValue + distance - fromValue;
            }
        }

        public override string ToString() => string.Format("{0} (weight: {1})", Value, Weight);
    }
}