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
    }

    public struct AnimationElement
    {
        public WeightedValue? Value { get; set; }
        public ElementId Id { get; set; }

        public AnimationElement(ElementId id) => Id = id;

        public AnimationElement(ElementId id, WeightedValue value)
        {
            Id = id;
            Value = value;
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

            return new()
            {
                Value = WeightedValue.Lerp(from.Value, to.Value, progress, weighted),
                Id = from.Id
            };
        }
    }

    public struct WeightedValue
    {
        public float Value { get; set; }
        public float Weight { get; set; }

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
                Value = to.Value.Value * (weighted ? 1 : progress),
                Weight = to.Value.Weight * (weighted ? progress : 1)
            };

            if (to == null) return new()
            {
                Value = from.Value.Value * (1 - (weighted ? 1 : progress)),
                Weight = from.Value.Weight * (1 - (weighted ? progress : 1))
            };

            return new()
            {
                Value = from.Value.Value * (1 - progress) + to.Value.Value * progress,
                Weight = from.Value.Weight * (1 - progress) + to.Value.Weight * progress
            };
        }
    }
}