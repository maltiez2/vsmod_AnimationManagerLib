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

        public PlayerModelAnimation(TAnimationResult[] keyFrames, ushort[] keyFramesPosition, TAnimationResult startingFrame)
        {
            mKeyFrames = keyFrames;
            mFrames = keyFramesPosition;
            mLastFrame = (TAnimationResult)startingFrame.Clone();
        }

        TAnimationResult IAnimation<TAnimationResult>.Blend(float progress, float? targetFrame, TAnimationResult endFrame)
        {
            mLastProgress = progress;
            TAnimationResult targetFrameValue = CalcFrame(progress, targetFrame ?? 0, targetFrame ?? 0);
            mLastFrame = (TAnimationResult)targetFrameValue.Lerp(endFrame, progress);
            return mLastFrame;
        }

        TAnimationResult IAnimation<TAnimationResult>.EaseOut(float progress, TAnimationResult endFrame)
        {
            float currentProgress = progress / (1 - mLastProgress);
            mLastFrame = (TAnimationResult)mLastFrame.Lerp(endFrame, currentProgress);
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

            float keyFrameProgress = nextFrame == prevFrame ? 1 : (currentFrame - prevFrame) / (nextFrame - prevFrame);      

            return (prevKeyFrame, nextKeyFrame, keyFrameProgress);
        }
    }
}
