using System;
using System.Collections.Generic;
using System.Diagnostics;
using AnimationManagerLib.API;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib
{
    public class Animation : IAnimation
    {
        private readonly AnimationFrame[] mKeyFrames;
        private readonly ushort[] mFrames;

        public Animation(AnimationFrame[] keyFrames, ushort[] keyFramesPosition)
        {
            mKeyFrames = keyFrames;
            mFrames = keyFramesPosition;
        }

        AnimationFrame IAnimation.Blend(float progress, float? targetFrame, AnimationFrame endFrame)
        {
            AnimationFrame targetFrameValue = CalcFrame(progress, targetFrame ?? 0, targetFrame ?? 0);
            AnimationFrame endFrameClone = endFrame.Clone();
            targetFrameValue.LerpInto(endFrameClone, progress);
            return endFrameClone;
        }

        AnimationFrame IAnimation.Blend(float progress, AnimationFrame startFrame, AnimationFrame endFrame)
        {
            AnimationFrame endFrameClone = endFrame.Clone();
            startFrame.LerpInto(endFrameClone, progress);
            return endFrameClone;
        }

        AnimationFrame IAnimation.Play(float progress, float? startFrame, float? endFrame)
        {
            float startFrameIndex = startFrame == null ? 0 : (float)startFrame;
            float endFrameIndex = endFrame == null ? mFrames[mFrames.Length - 1] : (float)endFrame;

            return CalcFrame(progress, startFrameIndex, endFrameIndex);
        }

        private AnimationFrame CalcFrame(float progress, float startFrame, float endFrame)
        {
            (int prevKeyFrame, int nextKeyFrame, float keyFrameProgress) = ToKeyFrames(progress, startFrame, endFrame);
            Debug.Assert(mKeyFrames.Length > prevKeyFrame && mKeyFrames.Length > nextKeyFrame);
            if (prevKeyFrame == nextKeyFrame) return mKeyFrames[nextKeyFrame].Clone();
            AnimationFrame resultFrame = mKeyFrames[nextKeyFrame].Clone();
            mKeyFrames[prevKeyFrame].LerpInto(resultFrame, keyFrameProgress);
            return resultFrame;
        }

        private (int prevKeyFrame, int nextKeyFrame, float keyFrameProgress) ToKeyFrames(float progress, float startFrame, float endFrame)
        {
            float currentFrame = startFrame + (endFrame - startFrame) * progress;

            int nextKeyFrame, prevKeyFrame = 0;

            for (nextKeyFrame = 0; nextKeyFrame < mKeyFrames.Length && mFrames[nextKeyFrame] < currentFrame; nextKeyFrame++)
            {
                prevKeyFrame = nextKeyFrame;
            }
            prevKeyFrame = GameMath.Min(prevKeyFrame, mKeyFrames.Length - 1);
            nextKeyFrame = GameMath.Min(nextKeyFrame, mKeyFrames.Length - 1);

            float prevFrame = mFrames[prevKeyFrame];
            float nextFrame = mFrames[nextKeyFrame];

            if (startFrame == endFrame) return (nextKeyFrame, nextKeyFrame, 1);

            Console.WriteLine("prevKeyFrame: {3}, nextKeyFrame{4}, prevFrame: {0}, currentFrame: {1}, nextFrame: {2}", prevFrame, currentFrame, nextFrame, prevKeyFrame, nextKeyFrame);

            float keyFrameProgress = nextFrame == prevFrame ? 1 : (currentFrame - prevFrame) / (nextFrame - prevFrame);      

            return (prevKeyFrame, nextKeyFrame, keyFrameProgress);
        }
    }
}
