using System;
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
        private TAnimationResult mLastFrame;
        private float mLastProgress;

        public PlayerModelAnimation(TAnimationResult[] keyFrames, ushort[] keyFramesPosition)
        {
            mKeyFrames = keyFrames;
            mFrames = keyFramesPosition;
        }

        TAnimationResult IAnimation<TAnimationResult>.Blend(float progress, float? targetFrame, TAnimationResult endFrame)
        {
            mLastProgress = progress;
            TAnimationResult targetFrameValue = CalcFrame(progress, targetFrame ?? 0, targetFrame ?? 0);
            mLastFrame = (TAnimationResult)targetFrameValue.Average(endFrame, progress, 1 - progress);
            return mLastFrame;
        }

        TAnimationResult IAnimation<TAnimationResult>.EaseOut(float progress, TAnimationResult endFrame)
        {
            float currentProgress = progress / (1 - mLastProgress);
            mLastFrame = (TAnimationResult)mLastFrame.Average(endFrame, currentProgress, 1 - currentProgress);
            return mLastFrame;
        }

        TAnimationResult IAnimation<TAnimationResult>.Play(float progress, float? startFrame, float? endFrame)
        {
            float startFrameIndex = startFrame == null ? 0 : (float)startFrame;
            float endFrameIndex = endFrame == null ? mFrames[mFrames.Length - 1] : (float)endFrame;

            mLastFrame = CalcFrame(progress, startFrameIndex, endFrameIndex);
            mLastProgress = progress;

            return mLastFrame;
        }

        void IDisposable.Dispose()
        {
            throw new NotImplementedException();
        }

        private TAnimationResult CalcFrame(float progress, float startFrame, float endFrame)
        {
            (int prevKeyFrame, int nextKeyFrame, float keyFrameProgress) = ToKeyFrames(progress, startFrame, endFrame);
            Debug.Assert(mKeyFrames.Length > prevKeyFrame && mKeyFrames.Length > nextKeyFrame);
            if (prevKeyFrame == nextKeyFrame) return (TAnimationResult)mKeyFrames[nextKeyFrame].Clone();
            return (TAnimationResult)mKeyFrames[prevKeyFrame].Average(mKeyFrames[nextKeyFrame], keyFrameProgress, 1 - keyFrameProgress);
        }

        private (int prevKeyFrame, int nextKeyFrame, float keyFrameProgress) ToKeyFrames(float progress, float startFrame, float endFrame)
        {
            float currentFrame = startFrame + (endFrame - startFrame) * progress;

            int nextKeyFrame, prevKeyFrame = 0;

            for (nextKeyFrame = 0; nextKeyFrame < mKeyFrames.Length && mFrames[nextKeyFrame] < currentFrame; nextKeyFrame++)
            {
                prevKeyFrame = nextKeyFrame;
            }

            float prevFrame = mFrames[GameMath.Min(prevKeyFrame, mKeyFrames.Length - 1)];
            float nextFrame = mFrames[GameMath.Min(nextKeyFrame, mKeyFrames.Length - 1)];

            float keyFrameProgress = (currentFrame - prevFrame) / (nextFrame - prevFrame);      

            return (prevKeyFrame, nextKeyFrame, keyFrameProgress);
        }
    }
}
