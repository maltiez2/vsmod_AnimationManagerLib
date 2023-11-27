using Vintagestory.API.Common;
using System.Collections.Generic;
using System;

namespace AnimationManagerLib
{
    public class EntityAnimationFrame
    {
        private readonly Dictionary<ElementId, AnimationElement> mElements = new();
        private readonly Dictionary<ElementId, EnumAnimationBlendMode> mBlendModes = new();
        private readonly float mDefaultElementWeight = 1;

        public EntityAnimationFrame() { }

        public EntityAnimationFrame(Dictionary<string, AnimationKeyFrameElement> elements, AnimationMetaData metaData, float weight = 1, float elementWeightMultiplier = 1, float defaultElementWeight = 1)
        {
            mDefaultElementWeight = defaultElementWeight;

            foreach ((string element, AnimationKeyFrameElement keyFrameElement) in elements)
            {
                EnumAnimationBlendMode? blendMode = metaData.ElementBlendMode.ContainsKey(element) ? metaData.ElementBlendMode[element] : null;
                float elementWeight = metaData.ElementWeight.ContainsKey(element) ? metaData.ElementWeight[element] * elementWeightMultiplier : weight;
                ForEachElementType((elementType, value) => ConstructElement(elementType, element, value, elementWeight, blendMode), keyFrameElement);
            }
        }

        private void ConstructElement(ElementType elementType, string name, double? value, float weight, EnumAnimationBlendMode? blendMode)
        {
            if (value == null) return;
            ElementId id = new(name, elementType);
            AnimationElement element = new(id, new((float)value, weight));
            mElements.Add(id, element);
            if (blendMode != null) mBlendModes.Add(id, blendMode.Value);
        }

        public void BlendInto(EntityAnimationFrame frame, EnumAnimationBlendMode blendMode)
        {
            switch (blendMode)
            {
                case EnumAnimationBlendMode.Add:
                    AddTo(frame, mDefaultElementWeight);
                    break;
                case EnumAnimationBlendMode.Average:
                    AverageTo(frame);
                    break;
                case EnumAnimationBlendMode.AddAverage:
                    AddAverageTo(frame, mDefaultElementWeight);
                    break;
            }
        }

        public void LerpInto(EntityAnimationFrame frame, float progress)
        {
            AnimationElement defaultElement = new();
            foreach ((var id, var element) in mElements)
            {
                if (frame.mElements.ContainsKey(id))
                {
                    frame.mElements[id] = AnimationElement.Lerp(element, frame.mElements[id], progress);
                }
                else
                {
                    frame.mElements.Add(id, AnimationElement.Lerp(element, defaultElement, progress));
                }
            }

            foreach ((var id, var element) in frame.mElements)
            {
                if (!mElements.ContainsKey(id))
                {
                    frame.mElements[id] = AnimationElement.Lerp(defaultElement, element, progress);
                }
            }
        }

        private void AddTo(EntityAnimationFrame frame, float defaultWeight = 1)
        {
            foreach ((var id, var element) in mElements)
            {
                if (frame.mElements.ContainsKey(id))
                {
                    frame.mElements[id] = AnimationElement.Sum(frame.mElements[id], element.Value != null ? element.Value.Value.Value : null, defaultWeight);
                }
                else
                {
                    AnimationElement newElement = new()
                    {
                        Value = element.Value == null ? null : new WeightedValue(element.Value.Value.Value, defaultWeight),
                        Id = element.Id
                    };
                    frame.mElements.Add(id, newElement);
                }
            }
        }

        private void AverageTo(EntityAnimationFrame frame)
        {
            foreach ((var id, var element) in mElements)
            {
                if (frame.mElements.ContainsKey(id))
                {
                    if (mBlendModes.ContainsKey(id))
                    {
                        switch (mBlendModes[id])
                        {
                            case EnumAnimationBlendMode.Add:
                                frame.mElements[id] = AnimationElement.Sum(frame.mElements[id], mElements[id]);
                                break;
                            case EnumAnimationBlendMode.Average:
                                frame.mElements[id] = AnimationElement.Average(frame.mElements[id], mElements[id]);
                                break;
                            case EnumAnimationBlendMode.AddAverage:
                                frame.mElements[id] = AnimationElement.Sum(frame.mElements[id], mElements[id]);
                                break;
                        }
                    }
                    else
                    {
                        frame.mElements[id] = AnimationElement.Average(frame.mElements[id], mElements[id]);
                    }
                }
                else
                {
                    frame.mElements.Add(id, element);
                }
            }
        }

        private void AddAverageTo(EntityAnimationFrame frame, float defaultWeight = 1)
        {
            foreach ((var id, var element) in mElements)
            {
                if (frame.mElements.ContainsKey(id))
                {
                    if (mBlendModes.ContainsKey(id))
                    {
                        switch (mBlendModes[id])
                        {
                            case EnumAnimationBlendMode.Add:
                                frame.mElements[id] = AnimationElement.Sum(frame.mElements[id], element.Value != null ? element.Value.Value.Value : null, defaultWeight);
                                break;
                            case EnumAnimationBlendMode.Average:
                                frame.mElements[id] = AnimationElement.Average(frame.mElements[id], mElements[id]);
                                break;
                            case EnumAnimationBlendMode.AddAverage:
                                frame.mElements[id] = AnimationElement.Sum(frame.mElements[id], mElements[id]);
                                break;
                        }
                    }
                    else
                    {
                        frame.mElements[id] = AnimationElement.Sum(frame.mElements[id], mElements[id]);
                    }
                }
                else
                {
                    frame.mElements.Add(id, element);
                }
            }
        }

        private void ForEachElementType(Action<ElementType> action)
        {
            action(ElementType.translateX);
            action(ElementType.translateY);
            action(ElementType.translateZ);
            action(ElementType.degX);
            action(ElementType.degY);
            action(ElementType.degZ);
        }
        private void ForEachElementType(Action<ElementType, double?> action, AnimationKeyFrameElement keyFrameElement)
        {
            action(ElementType.translateX, keyFrameElement.OffsetX);
            action(ElementType.translateY, keyFrameElement.OffsetY);
            action(ElementType.translateZ, keyFrameElement.OffsetZ);
            action(ElementType.degX, keyFrameElement.RotationX);
            action(ElementType.degY, keyFrameElement.RotationY);
            action(ElementType.degZ, keyFrameElement.RotationZ);
        }
        private void ForEachElementType(Action<ElementType, float?> action, ElementPose pose)
        {
            action(ElementType.translateX, pose.translateX);
            action(ElementType.translateY, pose.translateY);
            action(ElementType.translateZ, pose.translateZ);
            action(ElementType.degX, pose.degX);
            action(ElementType.degY, pose.degY);
            action(ElementType.degZ, pose.degZ);
        }
    }
}