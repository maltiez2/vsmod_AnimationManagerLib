using System;
using System.Collections.Generic;
using System.Diagnostics;
using AnimationManagerLib.API;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib
{
    public class PlayerModelAnimation<TAnimationResult> : IAnimation<TAnimationResult>
        where TAnimationResult  : IAnimationResult
    {
        private readonly TAnimationResult[] mKeyFrames;
        private readonly ushort[] mFrames;

        public PlayerModelAnimation(TAnimationResult[] keyFrames, ushort[] keyFramesPosition, TAnimationResult startingFrame)
        {
            mKeyFrames = keyFrames;
            mFrames = keyFramesPosition;
        }

        TAnimationResult IAnimation<TAnimationResult>.Blend(float progress, float? targetFrame, TAnimationResult endFrame)
        {
            TAnimationResult targetFrameValue = CalcFrame(progress, targetFrame ?? 0, targetFrame ?? 0);
            return (TAnimationResult)targetFrameValue.Lerp(endFrame, progress);
        }

        TAnimationResult IAnimation<TAnimationResult>.Blend(float progress, TAnimationResult startFrame, TAnimationResult endFrame)
        {
            return (TAnimationResult)startFrame.Lerp(endFrame, progress);
        }

        TAnimationResult IAnimation<TAnimationResult>.Play(float progress, float? startFrame, float? endFrame)
        {
            float startFrameIndex = startFrame == null ? 0 : (float)startFrame;
            float endFrameIndex = endFrame == null ? mFrames[mFrames.Length - 1] : (float)endFrame;

            return CalcFrame(progress, startFrameIndex, endFrameIndex);
        }

        private TAnimationResult CalcFrame(float progress, float startFrame, float endFrame)
        {
            (int prevKeyFrame, int nextKeyFrame, float keyFrameProgress) = ToKeyFrames(progress, startFrame, endFrame);
            Debug.Assert(mKeyFrames.Length > prevKeyFrame && mKeyFrames.Length > nextKeyFrame);
            if (prevKeyFrame == nextKeyFrame) return (TAnimationResult)mKeyFrames[nextKeyFrame].Clone();
            Console.WriteLine("From '{0}' to '{1}': {2} ({3} -> {4}: {5})", prevKeyFrame, nextKeyFrame, keyFrameProgress, startFrame, endFrame, progress);
            return (TAnimationResult)mKeyFrames[prevKeyFrame].Lerp(mKeyFrames[nextKeyFrame], keyFrameProgress);
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

        private bool mDisposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!mDisposedValue)
            {
                if (disposing)
                {
                    foreach (var frame in mKeyFrames)
                    {
                        (frame as IDisposable)?.Dispose();
                    }
                }

                mDisposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
