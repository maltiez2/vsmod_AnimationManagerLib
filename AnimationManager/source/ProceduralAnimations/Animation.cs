using AnimationManagerLib.API;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

#if DEBUG
using ImGuiNET;
#endif

namespace AnimationManagerLib;

internal class Animation : IAnimation, ISerializable
{
    public AnimationId Id { get; private set; }
    public float TotalFrames => mTotalFrames;


    private readonly AnimationFrame[] mKeyFrames;
    private readonly ushort[] mFrames;
    private readonly bool mCyclic;
    private readonly float mTotalFrames;

    public Animation(AnimationId id, AnimationFrame[] keyFrames, ushort[] keyFramesPosition, float totalFrames, bool cyclic = false)
    {
        Id = id;
        mKeyFrames = keyFrames;
        mFrames = keyFramesPosition;
        mCyclic = cyclic;
        mTotalFrames = totalFrames;

        if (mFrames.Length > 0 && mFrames[0] != 0)
        {
            AnimationFrame firstFrame = new(mKeyFrames[0].DefaultBlendMode, mKeyFrames[0].DefaultElementWeight);
            mFrames = mFrames.Prepend((ushort)0).ToArray();
            mKeyFrames = mKeyFrames.Prepend(firstFrame).ToArray();
        }

        if (mFrames.Length > 0 && MathF.Abs(mFrames[^1] - mTotalFrames + 1) > 1E-3)
        {
            AnimationFrame lastFrame = new(mKeyFrames[^1].DefaultBlendMode, mKeyFrames[^1].DefaultElementWeight);
            mFrames = mFrames.Append((ushort)((ushort)mTotalFrames - 1));
            mKeyFrames = mKeyFrames.Append(lastFrame);
        }

        PreprocessKeyframes();
    }

    public AnimationFrame Blend(float progress, float? targetFrame, AnimationFrame endFrame)
    {
        AnimationFrame targetFrameValue = CalcFrame(progress, targetFrame ?? 0, targetFrame ?? 0);
        AnimationFrame endFrameClone = endFrame.Clone();
        targetFrameValue.LerpInto(endFrameClone, progress);
        return endFrameClone;
    }

    public AnimationFrame Blend(float progress, AnimationFrame startFrame, AnimationFrame endFrame)
    {
        AnimationFrame endFrameClone = endFrame.Clone();
        startFrame.LerpInto(endFrameClone, progress);
        return endFrameClone;
    }

    public AnimationFrame Play(float progress, float? startFrame = null, float? endFrame = null)
    {
        float startFrameIndex = startFrame == null ? 0 : (float)startFrame;
        float endFrameIndex = endFrame == null ? mFrames[^1] : (float)endFrame;

        return CalcFrame(progress, startFrameIndex, endFrameIndex);
    }

    private AnimationFrame CalcFrame(float progress, float startFrame, float endFrame)
    {
        (int prevKeyFrame, int nextKeyFrame, float keyFrameProgress) = ToKeyFrames(progress, startFrame, endFrame);

        if (prevKeyFrame == nextKeyFrame) return mKeyFrames[nextKeyFrame].Clone();

        AnimationFrame resultFrame = mKeyFrames[nextKeyFrame].Clone();
        mKeyFrames[prevKeyFrame].LerpInto(resultFrame, keyFrameProgress);
        return resultFrame;
    }

    private (int prevKeyFrame, int nextKeyFrame, float keyFrameProgress) ToKeyFrames(float progress, float startFrame, float endFrame)
    {
        float currentFrame = CalcCurrentFrame(progress, startFrame, endFrame);

        (int startKeyFrame, int endKeyFrame) = FindKeyFrames(currentFrame);

        if (endKeyFrame == startKeyFrame) return (startKeyFrame, startKeyFrame, 1);

        float firstFrame = mFrames[startKeyFrame];
        float lastFrame = mFrames[endKeyFrame];
        float keyFrameProgress = (currentFrame - firstFrame) / (lastFrame - firstFrame);

        return (startKeyFrame, endKeyFrame, keyFrameProgress);
    }

    private float CalcCurrentFrame(float progress, float startFrame, float endFrame)
    {
        if (!mCyclic || startFrame < endFrame) return startFrame + (endFrame - startFrame) * progress;

        float framesLeft = mTotalFrames - startFrame;
        float frameProgress = progress * (endFrame + framesLeft);
        if (frameProgress < framesLeft)
        {
            return startFrame + frameProgress;
        }
        else
        {
            return frameProgress - framesLeft;
        }
    }

    private (int startKeyFrame, int endKeyFrame) FindKeyFrames(float currentFrame)
    {
        int endKeyFrame;
        int startKeyFrame = 0;

        for (endKeyFrame = 0; endKeyFrame < mKeyFrames.Length && mFrames[endKeyFrame] < currentFrame; endKeyFrame++)
        {
            startKeyFrame = endKeyFrame;
        }

        if (endKeyFrame >= mKeyFrames.Length) endKeyFrame = mKeyFrames.Length - 1;

        if (mCyclic && startKeyFrame == endKeyFrame) endKeyFrame = 0;

        return (startKeyFrame, endKeyFrame);
    }

    private void PreprocessKeyframes()
    {
        HashSet<ElementId> elements = new();
        Dictionary<ElementId, (AnimationElement element, EnumAnimationBlendMode blendMode)> singleElements = new();
        foreach (AnimationFrame keyFrame in mKeyFrames)
        {
            foreach ((ElementId id, (AnimationElement element, EnumAnimationBlendMode blendMode) entry) in keyFrame.Elements)
            {
                if (!singleElements.ContainsKey(id))
                {
                    singleElements.Add(id, entry);
                }
                else if (!elements.Contains(id))
                {
                    elements.Add(id);
                }
            }
        }

        foreach ((ElementId element, (AnimationElement element, EnumAnimationBlendMode blendMode) entry) in singleElements.Where((entry, _) => !elements.Contains(entry.Key)))
        {
            foreach (Dictionary<ElementId, (AnimationElement element, EnumAnimationBlendMode blendMode)>? frameElements in mKeyFrames.Select(frame => frame.Elements).Where(frameElements => !frameElements.ContainsKey(element)))
            {
                frameElements.Add(element, entry);
            }
        }

        foreach (ElementId element in elements)
        {
            ProcessElement(element);
        }
    }

    private void ProcessElement(ElementId id)
    {
        List<(int frame, int start, int end, float progress)> tasks = new();

        for (int index = 0; index < mKeyFrames.Length; index++)
        {
            if (mKeyFrames[index].Elements.ContainsKey(id)) continue;

            (int startKeyFrame, int endKeyFrame, float progress) = GetKeyFramesAndProgressToLerp(id, index);

            tasks.Add((index, startKeyFrame, endKeyFrame, progress));
        }

        foreach ((int frame, int start, int end, float progress) in tasks)
        {
            EnumAnimationBlendMode blendMode = mKeyFrames[start].Elements[id].blendMode;
            AnimationElement startElement = mKeyFrames[start].Elements[id].element;
            AnimationElement endElement = mKeyFrames[end].Elements[id].element;

            AnimationElement element = AnimationElement.CircularLerp(startElement, endElement, progress, false);
            mKeyFrames[frame].Elements.Add(id, (element, blendMode));
        }
    }

    private (int startKeyFrame, int endKeyFrame, float progress) GetKeyFramesAndProgressToLerp(ElementId id, int keyFrameIndex)
    {
        (int startKeyFrame, int endKeyFrame) = GetKeyFramesToLerp(id, keyFrameIndex);

        float frame = mFrames[keyFrameIndex];
        float start = mFrames[startKeyFrame];
        float end = mFrames[endKeyFrame];

        float startDistance = frame > start ? frame - start : frame + mTotalFrames - start;
        float endDistance = frame < end ? end - frame : end + mTotalFrames - frame;
        float progress = startDistance / (startDistance + endDistance);

        return (startKeyFrame, endKeyFrame, progress);
    }

    private (int startKeyFrame, int endKeyFrame) GetKeyFramesToLerp(ElementId id, int keyFrameIndex)
    {
        int startKeyFrame = -1;
        int endKeyFrame = mKeyFrames.Length;

        for (int index = 0; index < mKeyFrames.Length; index++)
        {
            if (index == keyFrameIndex || !mKeyFrames[index].Elements.ContainsKey(id)) continue;

            if (index < keyFrameIndex)
            {
                startKeyFrame = index;
            }

            if (index < endKeyFrame && index > keyFrameIndex)
            {
                endKeyFrame = index;
            }
        }

        if (startKeyFrame == -1)
        {
            for (int index = mKeyFrames.Length - 1; index >= 0; index--)
            {
                if (index == keyFrameIndex || !mKeyFrames[index].Elements.ContainsKey(id)) continue;
                startKeyFrame = index;
                break;
            }
        }

        if (endKeyFrame == mKeyFrames.Length)
        {
            for (int index = 0; index < mKeyFrames.Length; index++)
            {
                if (index == keyFrameIndex || !mKeyFrames[index].Elements.ContainsKey(id)) continue;
                endKeyFrame = index;
                break;
            }
        }

        return (startKeyFrame, endKeyFrame);
    }

    public static float ShortestDistance(float start, float end, float max = 360f)
    {
        return ((end - start) % max + max * 1.5f) % max - max * 0.5f;
    }

    public override string ToString() => $"Animation: {Id}";

    private int mCurrentFrame = 0;
    public int CurrentFrame => mFrames[mCurrentFrame];
    public bool Editor(string id)
    {
        bool modified = false;

#if DEBUG
        ImGui.SliderInt($"Key frame##{id}", ref mCurrentFrame, 0, mKeyFrames.Length - 1);

        int keyFrameFrame = mFrames[mCurrentFrame];
        ImGui.SliderInt($"Key frame position##{id}", ref keyFrameFrame, mCurrentFrame == 0 ? 0 : mFrames[mCurrentFrame - 1], mCurrentFrame == mKeyFrames.Length - 1 ? (int)mTotalFrames : mFrames[mCurrentFrame + 1]);
        if (mFrames[mCurrentFrame] != keyFrameFrame) modified = true;
        mFrames[mCurrentFrame] = (ushort)keyFrameFrame;

        if (mKeyFrames[mCurrentFrame].Editor($"{id}KeyFrame{mCurrentFrame}")) modified = true;
#endif

        return modified;
    }

    public JToken Serialize()
    {
        JArray keyFrames = new();

        foreach (JObject frame in mKeyFrames.Select((frame, index) => SerializeFrame(mFrames[index], frame.Serialize())).Where(frame => frame != null))
        {
            keyFrames.Add(frame);
        }

        JObject animation = new()
        {
            ["code"] = Id.GetName(),
            ["keyFrames"] = keyFrames
        };

        return animation;
    }

    private JObject SerializeFrame(int index, JToken elements)
    {
        JObject frame = new()
        {
            ["frame"] = index,
            ["elements"] = elements
        };
        return frame;
    }
}
