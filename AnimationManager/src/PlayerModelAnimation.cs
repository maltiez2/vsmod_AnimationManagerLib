using System;
using System.Collections.Generic;
using AnimationManagerLib.API;

namespace AnimationManagerLib
{
    public class PlayerModelAnimation<TAnimationResult> : IAnimation<TAnimationResult>
        where TAnimationResult  : IAnimationResult
    {
        private readonly TAnimationResult[] mFrames;
        private TAnimationResult mLastFrame;
        private float mLastProgress;

        public PlayerModelAnimation(TAnimationResult[] frames)
        {
            mFrames = frames;
        }

        TAnimationResult IAnimation<TAnimationResult>.Blend(float progress, ushort? startFrame, TAnimationResult endFrame)
        {
            mLastProgress = progress;
            mLastFrame = (TAnimationResult)mFrames[startFrame == null ? 0 : (ushort)startFrame].Average(endFrame, progress, 1 - progress);
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
            int endFrameIndex = endFrame == null ? mFrames.Length - 1 : (int)endFrame;

            if (startFrameIndex == endFrameIndex) return (TAnimationResult)mFrames[startFrameIndex].Clone();

            int nextFrameIndex = endFrameIndex > startFrameIndex ? startFrameIndex + 1 : startFrameIndex - 1;
            float frameProgress = progress / (endFrameIndex - startFrameIndex);

            mLastFrame = (TAnimationResult)mFrames[startFrameIndex].Average(mFrames[nextFrameIndex], frameProgress, 1 - frameProgress);
            mLastProgress = progress;

            return mLastFrame;
        }

        void IDisposable.Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
