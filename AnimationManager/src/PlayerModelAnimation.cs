using System;
using System.Collections.Generic;
using AnimationManagerLib.API;

namespace AnimationManagerLib
{
    public class PlayerModelAnimation<TAnimationResult> : IAnimation<TAnimationResult>
        where TAnimationResult  : IAnimationResult
    {
        private readonly TAnimationResult[] mKeyFrames;
        private readonly ushort[] mFrames;
        private TAnimationResult mLastFrame;
        private float mLastProgress;

        public PlayerModelAnimation(TAnimationResult[] keyFrames, ushort[] keyFramesPostion)
        {
            mKeyFrames = keyFrames;
            mFrames = keyFramesPostion;
        }

        TAnimationResult IAnimation<TAnimationResult>.Blend(float progress, ushort? startFrame, TAnimationResult endFrame)
        {
            mLastProgress = progress;
            mLastFrame = (TAnimationResult)mKeyFrames[startFrame == null ? 0 : (ushort)startFrame].Average(endFrame, progress, 1 - progress);
            return mLastFrame;
        }

        TAnimationResult IAnimation<TAnimationResult>.EaseOut(float progress, TAnimationResult endFrame)
        {
            float currentProgress = progress / (1 - mLastProgress);
            mLastFrame = (TAnimationResult)mLastFrame.Average(endFrame, currentProgress, 1 - currentProgress);
            return mLastFrame;
        }

        TAnimationResult IAnimation<TAnimationResult>.Play(float progress, ushort? startFrame, ushort? endFrame)
        {
            int startFrameIndex = startFrame == null ? 0 : (int)startFrame;
            int endFrameIndex = endFrame == null ? mKeyFrames.Length - 1 : (int)endFrame;

            if (startFrameIndex == endFrameIndex) return (TAnimationResult)mKeyFrames[startFrameIndex].Clone();

            (int nextKeyFrame, float frameProgress) = CalcNextFrame(progress, startFrameIndex, endFrameIndex);

            mLastFrame = (TAnimationResult)mKeyFrames[startFrameIndex].Average(mKeyFrames[nextKeyFrame], frameProgress, 1 - frameProgress);
            mLastProgress = progress;

            return mLastFrame;
        }

        void IDisposable.Dispose()
        {
            throw new NotImplementedException();
        }

        private (int nextKeyFrame, float frameProgress) CalcNextFrame(float progress, int startKeyFrame, int endKeyFrame)
        {
            int startFrame = mFrames[startKeyFrame];
            int endFrame = mFrames[endKeyFrame];
            int length = endFrame - startFrame;
            float frameIndexProgress = progress * length;

            int nextKeyFrame, prevKeyFrame = startKeyFrame;

            for (nextKeyFrame = startKeyFrame; nextKeyFrame <= endKeyFrame || mFrames[nextKeyFrame] < frameIndexProgress; nextKeyFrame++)
            {
                prevKeyFrame = nextKeyFrame;
            }

            float keyFrameLength = mFrames[nextKeyFrame] - mFrames[prevKeyFrame];
            float frameProgress = (frameIndexProgress - mFrames[prevKeyFrame]) / keyFrameLength;

            return (nextKeyFrame, frameProgress);
        }
    }
}
