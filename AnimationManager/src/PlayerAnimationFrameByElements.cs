using Vintagestory.API.Common;
using AnimationManagerLib.API;
using System.Collections.Generic;
using System;

namespace AnimationManagerLib
{
    public class EntityAnimationFrame
    {
        private readonly Dictionary<ElementId, (AnimationElement element, EnumAnimationBlendMode blendMode)> mElements = new();
        private readonly EnumAnimationBlendMode mDefaultBlendMode = EnumAnimationBlendMode.Average;
        private readonly float mDefaultElementWeight = 1;

        public EntityAnimationFrame() { }

        public EntityAnimationFrame(CategoryId category)
        {
            mDefaultElementWeight = category.Weight ?? 1;
            mDefaultBlendMode = category.Blending;
        }

        public EntityAnimationFrame(Dictionary<string, AnimationKeyFrameElement> elements, AnimationMetaData metaData, CategoryId category)
        {
            mDefaultElementWeight = category.Weight ?? 1;
            mDefaultBlendMode = category.Blending;
            foreach ((string element, AnimationKeyFrameElement keyFrameElement) in elements)
            {
                EnumAnimationBlendMode? blendMode = metaData.ElementBlendMode.ContainsKey(element) ? metaData.ElementBlendMode[element] : null;
                float elementWeight = metaData.ElementWeight.ContainsKey(element) ? metaData.ElementWeight[element] * mDefaultElementWeight : mDefaultElementWeight;
                ForEachElementType((elementType, value) => AddElement(elementType, element, value, elementWeight, GetBlendMode(mDefaultBlendMode, blendMode.Value)), keyFrameElement);
            }
        }

        public virtual void BlendInto(EntityAnimationFrame frame)
        {
            foreach ((var id, (var element, var blendMode)) in mElements)
            {
                if (frame.mElements.ContainsKey(id))
                {
                    frame.mElements[id] = (CombineElements(element, frame.mElements[id].element, blendMode, mDefaultElementWeight), mDefaultBlendMode);
                }
                else
                {
                    frame.mElements[id] = (CombineElements(element, new(id), blendMode, mDefaultElementWeight), mDefaultBlendMode);
                }
            }
        }
        public virtual void LerpInto(EntityAnimationFrame frame, float progress)
        {
            AnimationElement defaultElement = new();
            foreach ((var id, (var element, var blendMode)) in mElements)
            {
                if (frame.mElements.ContainsKey(id))
                {
                    frame.mElements[id] = (AnimationElement.Lerp(element, frame.mElements[id].element, progress), blendMode);
                }
                else
                {
                    frame.mElements.Add(id, (AnimationElement.Lerp(element, defaultElement, progress), blendMode));
                }
            }

            foreach ((var id, (var element, var blendMode)) in frame.mElements)
            {
                if (!mElements.ContainsKey(id))
                {
                    frame.mElements[id] = (AnimationElement.Lerp(defaultElement, element, progress), blendMode);
                }
            }
        }
        public virtual void Apply(ElementPose pose, float poseWeight, uint nameHash)
        {
            foreach ((var id, (var element, var blendMode)) in mElements)
            {
                if (id.ElementNameHash != nameHash) continue;
                switch (blendMode)
                {
                    case EnumAnimationBlendMode.Add:
                        element.Add(pose);
                        break;
                    case EnumAnimationBlendMode.Average:
                        element.Average(pose, poseWeight);
                        break;
                    case EnumAnimationBlendMode.AddAverage:
                        element.Add(pose);
                        break;
                }
            }
        }
        static public EntityAnimationFrame Default(CategoryId category)
        {
            return new EntityAnimationFrame(category);
        }

        protected void AddElement(ElementType elementType, string name, double? value, float weight, EnumAnimationBlendMode blendMode)
        {
            if (value == null) return;
            ElementId id = new(name, elementType);
            AnimationElement element = new(id, new((float)value, weight));
            mElements.Add(id, (element, blendMode));
        }
        static protected EnumAnimationBlendMode GetBlendMode(EnumAnimationBlendMode categoryMode, EnumAnimationBlendMode? elementMode)
        {
            if (elementMode == null) return categoryMode;

            return categoryMode switch
            {
                EnumAnimationBlendMode.Add => elementMode switch
                {
                    EnumAnimationBlendMode.Add => EnumAnimationBlendMode.Add,
                    EnumAnimationBlendMode.Average => EnumAnimationBlendMode.Add,
                    EnumAnimationBlendMode.AddAverage => EnumAnimationBlendMode.Add
                },
                EnumAnimationBlendMode.Average => elementMode switch
                {
                    EnumAnimationBlendMode.Add => EnumAnimationBlendMode.Add,
                    EnumAnimationBlendMode.Average => EnumAnimationBlendMode.Average,
                    EnumAnimationBlendMode.AddAverage => EnumAnimationBlendMode.AddAverage
                },
                EnumAnimationBlendMode.AddAverage => elementMode switch
                {
                    EnumAnimationBlendMode.Add => EnumAnimationBlendMode.Add,
                    EnumAnimationBlendMode.Average => EnumAnimationBlendMode.Average,
                    EnumAnimationBlendMode.AddAverage => EnumAnimationBlendMode.AddAverage
                },
            };
        }
        static protected AnimationElement CombineElements(AnimationElement from, AnimationElement to, EnumAnimationBlendMode blendMode, float defaultWeight = 1)
        {
            return blendMode switch
            {
                EnumAnimationBlendMode.Add => AnimationElement.Sum(to, from.Value != null ? from.Value.Value.Value : null, defaultWeight),
                EnumAnimationBlendMode.Average => AnimationElement.Average(from, to),
                EnumAnimationBlendMode.AddAverage => AnimationElement.Sum(from, to)
            };
        }

        static public void ForEachElementType(Action<ElementType> action)
        {
            action(ElementType.translateX);
            action(ElementType.translateY);
            action(ElementType.translateZ);
            action(ElementType.degX);
            action(ElementType.degY);
            action(ElementType.degZ);
        }
        static public void ForEachElementType(Action<ElementType, double?> action, AnimationKeyFrameElement keyFrameElement)
        {
            action(ElementType.translateX, keyFrameElement.OffsetX);
            action(ElementType.translateY, keyFrameElement.OffsetY);
            action(ElementType.translateZ, keyFrameElement.OffsetZ);
            action(ElementType.degX, keyFrameElement.RotationX);
            action(ElementType.degY, keyFrameElement.RotationY);
            action(ElementType.degZ, keyFrameElement.RotationZ);
        }
        static public void ForEachElementType(Action<ElementType, float?> action, ElementPose pose)
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