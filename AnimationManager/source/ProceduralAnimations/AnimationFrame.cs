using AnimationManagerLib.API;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

#if DEBUG
using ImGuiNET;
using VSImGui;
#endif

namespace AnimationManagerLib;

internal class AnimationFrame : IWithGuiEditor, ISerializable
{
    public Dictionary<ElementId, (AnimationElement element, EnumAnimationBlendMode blendMode)> Elements { get; set; } = new();
    public EnumAnimationBlendMode DefaultBlendMode { get; set; }
    public float DefaultElementWeight { get; set; } = 1;

    public AnimationFrame(Category category)
    {
        DefaultElementWeight = category.Weight ?? 1;
        DefaultBlendMode = category.Blending;
    }
    public AnimationFrame(EnumAnimationBlendMode blendMode, float weight)
    {
        DefaultBlendMode = blendMode;
        DefaultElementWeight = weight;
    }
    public AnimationFrame(Dictionary<string, AnimationKeyFrameElement> elements, AnimationData metaData, Category category)
    {
        DefaultElementWeight = category.Weight ?? 1;
        DefaultBlendMode = category.Blending;
        foreach ((string element, AnimationKeyFrameElement keyFrameElement) in elements)
        {
            EnumAnimationBlendMode? blendMode = metaData.ElementBlendMode?.ContainsKey(element) == true ? metaData.ElementBlendMode[element] : null;
            float elementWeight = metaData.ElementWeight?.ContainsKey(element) == true ? metaData.ElementWeight[element] * DefaultElementWeight : DefaultElementWeight;

            AddElement(ElementType.translateX, element, keyFrameElement.OffsetX / 16, elementWeight, GetBlendMode(DefaultBlendMode, blendMode));
            AddElement(ElementType.translateY, element, keyFrameElement.OffsetY / 16, elementWeight, GetBlendMode(DefaultBlendMode, blendMode));
            AddElement(ElementType.translateZ, element, keyFrameElement.OffsetZ / 16, elementWeight, GetBlendMode(DefaultBlendMode, blendMode));
            AddElement(ElementType.degX, element, keyFrameElement.RotationX, elementWeight, GetBlendMode(DefaultBlendMode, blendMode), keyFrameElement.RotShortestDistanceX);
            AddElement(ElementType.degY, element, keyFrameElement.RotationY, elementWeight, GetBlendMode(DefaultBlendMode, blendMode), keyFrameElement.RotShortestDistanceY);
            AddElement(ElementType.degZ, element, keyFrameElement.RotationZ, elementWeight, GetBlendMode(DefaultBlendMode, blendMode), keyFrameElement.RotShortestDistanceZ);
        }
    }

    public virtual void BlendInto(AnimationFrame frame)
    {
        foreach ((ElementId id, (AnimationElement element, EnumAnimationBlendMode blendMode)) in Elements)
        {
            if (frame.Elements.ContainsKey(id))
            {
                frame.Elements[id] = (CombineElements(element, frame.Elements[id].element, blendMode, DefaultElementWeight), DefaultBlendMode);
            }
            else
            {
                frame.Elements[id] = (CombineElements(element, new(id), blendMode, DefaultElementWeight), DefaultBlendMode);
            }
        }
    }
    public virtual void LerpInto(AnimationFrame frame, float progress)
    {
        foreach ((ElementId id, (AnimationElement element, EnumAnimationBlendMode blendMode)) in Elements)
        {
            if (frame.Elements.ContainsKey(id))
            {
                frame.Elements[id] = (AnimationElement.Lerp(element, frame.Elements[id].element, progress, blendMode != EnumAnimationBlendMode.Add), blendMode);
            }
            else
            {
                frame.Elements.Add(id, (AnimationElement.Lerp(element, new(id), progress, blendMode != EnumAnimationBlendMode.Add), blendMode));
            }
        }

        foreach ((ElementId id, (AnimationElement element, EnumAnimationBlendMode blendMode)) in frame.Elements)
        {
            if (!Elements.ContainsKey(id))
            {
                frame.Elements[id] = (AnimationElement.Lerp(new(id), element, progress, blendMode != EnumAnimationBlendMode.Add), blendMode);
            }
        }
    }
    public virtual void Apply(ElementPose pose, ref float poseWeight, uint nameHash)
    {
        foreach ((ElementId id, (AnimationElement element, EnumAnimationBlendMode blendMode)) in Elements)
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
    public virtual void Weight(ref float poseWeight, uint nameHash)
    {
        float frameWeight = 0;
        int count = 0;

        foreach ((ElementId id, (AnimationElement element, EnumAnimationBlendMode blendMode)) in Elements)
        {
            if (id.ElementNameHash != nameHash) continue;
            switch (blendMode)
            {
                case EnumAnimationBlendMode.Add:
                    break;
                case EnumAnimationBlendMode.Average:
                    if (element.Value?.Weight != null)
                    {
                        frameWeight += element.Value?.Weight ?? 0;
                        count++;
                    }
                    break;
                case EnumAnimationBlendMode.AddAverage:
                    if (element.Value?.Weight != null)
                    {
                        frameWeight += element.Value?.Weight ?? 0;
                        count++;
                    }
                    break;
            }
        }

        poseWeight += frameWeight / count;
    }
    static public AnimationFrame Default(Category category)
    {
        return new AnimationFrame(category);
    }

    public virtual AnimationFrame Clone()
    {
        AnimationFrame clone = new(DefaultBlendMode, DefaultElementWeight);
        foreach ((ElementId key, (AnimationElement element, EnumAnimationBlendMode blendMode) value) in Elements)
        {
            clone.Elements[key] = value;
        }
        return clone;
    }
    public override string ToString()
    {
        string toString = "";
        int index = 1;
        foreach ((_, (AnimationElement element, _)) in Elements)
        {
            toString += string.Format("\n{0}: {1}", index++, element);
        }
        return toString;
    }

    protected void AddElement(ElementType elementType, string name, double? value, float weight, EnumAnimationBlendMode blendMode, bool shortestAngularDistance = false)
    {
        if (value == null) return;
        ElementId id = new(name, elementType);
        AnimationElement element = new(id, new((float)value, weight), shortestAngularDistance);
        Elements.Add(id, (element, blendMode));
    }
    static protected EnumAnimationBlendMode GetBlendMode(EnumAnimationBlendMode categoryMode, EnumAnimationBlendMode? elementMode)
    {
        if (elementMode == null) return categoryMode;

        return categoryMode switch // @TODO refactor or come out with more sensical mapping
        {
            EnumAnimationBlendMode.Add => elementMode switch
            {
                EnumAnimationBlendMode.Add => EnumAnimationBlendMode.Add,
                EnumAnimationBlendMode.Average => EnumAnimationBlendMode.Average,
                EnumAnimationBlendMode.AddAverage => EnumAnimationBlendMode.AddAverage,
                _ => throw new NotImplementedException()
            },
            EnumAnimationBlendMode.Average => elementMode switch
            {
                EnumAnimationBlendMode.Add => EnumAnimationBlendMode.Add,
                EnumAnimationBlendMode.Average => EnumAnimationBlendMode.Average,
                EnumAnimationBlendMode.AddAverage => EnumAnimationBlendMode.AddAverage,
                _ => throw new NotImplementedException()
            },
            EnumAnimationBlendMode.AddAverage => elementMode switch
            {
                EnumAnimationBlendMode.Add => EnumAnimationBlendMode.Add,
                EnumAnimationBlendMode.Average => EnumAnimationBlendMode.Average,
                EnumAnimationBlendMode.AddAverage => EnumAnimationBlendMode.AddAverage,
                _ => throw new NotImplementedException()
            },
            _ => throw new NotImplementedException(),
        };
    }
    static protected AnimationElement CombineElements(AnimationElement from, AnimationElement to, EnumAnimationBlendMode blendMode, float defaultWeight = 1)
    {
        return blendMode switch
        {
            EnumAnimationBlendMode.Add => AnimationElement.Sum(to, from.Value?.Value, defaultWeight),
            EnumAnimationBlendMode.Average => AnimationElement.Average(from, to),
            EnumAnimationBlendMode.AddAverage => AnimationElement.Sum(from, to),
            _ => throw new NotImplementedException()
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

    private readonly HashSet<ElementId> mModifiedElements = new();
#if DEBUG
    private readonly EnumEditor<EnumAnimationBlendMode> mBlendModeEditor = new();
    private const float cEpsilon = 1e-6f;
    private string mElementFilter = "";
    private int mCurrentElement = 0;
    private ElementId? mLastElement = null;
#endif
    public bool Editor(string id)
    {
        bool modified = false;
#if DEBUG
        DefaultBlendMode = mBlendModeEditor.Editor($"Default blend mode##{id}", DefaultBlendMode, ref modified);

        float defaultElementWeight = DefaultElementWeight;
        ImGui.DragFloat($"Default element weight##{id}", ref defaultElementWeight);
        if (Math.Abs(DefaultElementWeight - defaultElementWeight) > cEpsilon) modified = true;
        DefaultElementWeight = defaultElementWeight;

        ImGui.InputTextWithHint($"Elements filter##{id}", "supports wildcards", ref mElementFilter, 100);
        FilterElements(StyleEditor.WildCardToRegular(mElementFilter), out string[] elementNames, out ElementId[] ids);

        ImGui.ListBox($"Elements##{id}", ref mCurrentElement, elementNames, elementNames.Length);

        mLastElement = mCurrentElement >= ids.Length ? mLastElement : ids[mCurrentElement];

        if (mLastElement == null) return false;

        (AnimationElement element, EnumAnimationBlendMode blendMode) = Elements[mLastElement.Value];

        ImGui.SeparatorText("Element");
        blendMode = mBlendModeEditor.Editor($"Element blend mode##{id}", blendMode, ref modified);

        if (element.Editor($"{id}Element")) modified = true;

        if (modified)
        {
            Elements[mLastElement.Value] = new(element, blendMode);
            if (!mModifiedElements.Contains(mLastElement.Value)) mModifiedElements.Add(mLastElement.Value);
        }
#endif

        return modified;
    }

    private string ModifiedPrefix(ElementId id) => mModifiedElements.Contains(id) ? "* " : "";

    private void FilterElements(string filter, out string[] names, out ElementId[] ids)
    {

        names = Elements.Select((id, _) => $"{ModifiedPrefix(id.Key)}{id.Key}").ToArray();
        ids = Elements.Select((id, _) => id.Key).ToArray();
#if DEBUG
        if (filter == "") return;

        List<string> newNames = new();
        List<ElementId> newIds = new();

        for (int index = 0; index < ids.Length; index++)
        {
            if (StyleEditor.Match(filter, names[index]))
            {
                newIds.Add(ids[index]);
                newNames.Add(names[index]);
            }
        }

        if (mLastElement != null && newIds.Contains(mLastElement.Value))
        {
            mCurrentElement = newIds.FindIndex((value) => mLastElement.Value == value);
        }
        else if (mCurrentElement >= newIds.Count)
        {
            mLastElement = null;
            mCurrentElement = 0;
        }

        names = newNames.ToArray();
        ids = newIds.ToArray();
#endif
    }

    public JToken Serialize()
    {
        JObject elements = new();

        foreach ((string code, JObject element) in Elements.Select(entry => (entry.Key.Name, SerializeCube(entry.Key.Name))).Where(entry => entry.Item2 != null))
        {
            elements.TryAdd(code, element);
        }

        return elements;
    }

    private JObject SerializeCube(string name)
    {
        JObject cube = new();
        float? rotationX = GetElementValue(name, ElementType.degX);
        float? rotationY = GetElementValue(name, ElementType.degY);
        float? rotationZ = GetElementValue(name, ElementType.degZ);
        float? offsetX = GetElementValue(name, ElementType.translateX);
        float? offsetY = GetElementValue(name, ElementType.translateY);
        float? offsetZ = GetElementValue(name, ElementType.translateZ);
        bool hasRotation = (rotationX ?? rotationY ?? rotationZ) != null;
        bool hasOffset = (offsetX ?? offsetY ?? offsetZ) != null;

        if (hasRotation)
        {
            cube.Add("rotationX", new JValue(rotationX ?? 0));
            cube.Add("rotationY", new JValue(rotationY ?? 0));
            cube.Add("rotationZ", new JValue(rotationZ ?? 0));
        }

        if (hasOffset)
        {
            cube.Add("offsetX", new JValue((offsetX ?? 0) * 16));
            cube.Add("offsetY", new JValue((offsetY ?? 0) * 16));
            cube.Add("offsetZ", new JValue((offsetZ ?? 0) * 16));
        }

        return cube;
    }

    private float? GetElementValue(string name, ElementType elementType)
    {
        return Elements.Where(entry => entry.Key.Name == name && entry.Key.ElementType == elementType).Select(entry => entry.Value.element.Value?.Value).FirstOrDefault();
    }
}

internal class Composition
{
    public Dictionary<EnumAnimationBlendMode, AnimationFrame> Frames { get; set; } = new();
}

#if DEBUG
internal readonly struct EnumEditor<TEnum>
    where TEnum : struct, Enum
{
    private readonly string[] mNames;
    private readonly TEnum[] mValues;
    private readonly Dictionary<TEnum, int> mKeys = new();

    public EnumEditor()
    {
        mValues = Enum.GetValues<TEnum>();
        mNames = Enum.GetNames<TEnum>();

        for (int index = 0; index < mValues.Length; index++)
        {
            mKeys.Add(mValues[index], index);
        }
    }

    public TEnum Editor(string title, TEnum value, ref bool modified)
    {
        int index = mKeys[value];
        ImGui.Combo(title, ref index, mNames, mNames.Length);
        TEnum result = mValues[index];
        modified = index != mKeys[result];
        return result;
    }

    public bool Editor(string title, ref TEnum value)
    {
        int index = mKeys[value];
        ImGui.Combo(title, ref index, mNames, mNames.Length);
        TEnum result = mValues[index];
        bool modified = index != mKeys[result];
        value = result;
        return modified;
    }
}
#endif