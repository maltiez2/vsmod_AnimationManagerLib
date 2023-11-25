using System;
using Vintagestory.API.Common;
using AnimationManagerLib.API;

namespace AnimationManagerLib
{
    public class PlayerModelAnimator<TAnimationResult> : IAnimator<TAnimationResult>
        where TAnimationResult : IAnimationResult
    {
        private TimeSpan mCurrentTime;
        private TAnimationResult mLastFrame;
        private TAnimationResult mStartFrame;
        private TAnimationResult mDefaultFrame;
        private IAnimation<TAnimationResult> mCurrentAnimation;
        private AnimationRunMetadata mCurrentParameters;
        private ProgressModifiers.ProgressModifier mProgressModifier;
        private bool mStopped;
        private float mCurrentProgress;
        private float mPreviousProgress;

        void IAnimator<TAnimationResult>.Init(ICoreAPI api, TAnimationResult defaultFrame)
        {
            mDefaultFrame = (TAnimationResult)defaultFrame.Clone();
            mStartFrame = mDefaultFrame;
            mLastFrame = mDefaultFrame;
            mCurrentProgress = 1;
            mPreviousProgress = 1;
        }

        void IAnimator<TAnimationResult>.Run(AnimationRunMetadata parameters, IAnimation<TAnimationResult> animation)
        {
            mCurrentAnimation = animation;
            mCurrentParameters = parameters;
            mStartFrame = (TAnimationResult)mLastFrame.Clone();
            mProgressModifier = ProgressModifiers.Get(parameters.Modifier);
            mStopped = false;
            mCurrentTime = new TimeSpan(0);
            mPreviousProgress = mCurrentProgress;
            Console.WriteLine("*******************************************************************");
            Console.WriteLine("*** Action: {0}, Duration: {1}, Modifier: {2} ***", parameters.Action, parameters.Duration, parameters.Modifier);
            Console.WriteLine("*******************************************************************");
        }

        TAnimationResult IAnimator<TAnimationResult>.Calculate(TimeSpan timeElapsed, out IAnimator<TAnimationResult>.Status status)
        {
            mCurrentTime += timeElapsed;

            status = mStopped ? IAnimator<TAnimationResult>.Status.Stopped : IAnimator<TAnimationResult>.Status.Running;
            
            float progress = (float)mCurrentTime.TotalSeconds / (float)mCurrentParameters.Duration.TotalSeconds * mPreviousProgress + (1 - mPreviousProgress);
            Console.WriteLine("Progress: {0} ({4}), Status: {1}, Action: {2}, Duration: {3}", progress, mStopped ? "Stopped" : "Running", mCurrentParameters.Action, mCurrentParameters.Duration, mProgressModifier(progress));
            if (progress >= 1) mStopped = true;
            mCurrentProgress = mProgressModifier(progress);
            
            if (mStopped) return mLastFrame;

            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.Set:
                    mLastFrame = mCurrentAnimation.Play(1.0f, mCurrentParameters.StartFrame, mCurrentParameters.EndFrame);
                    mStopped = true;
                    break;
                case AnimationPlayerAction.EaseIn:
                    mLastFrame = mCurrentAnimation.Blend(1f - mCurrentProgress, mCurrentParameters.StartFrame, mStartFrame);
                    break;
                case AnimationPlayerAction.EaseOut:
                    mLastFrame = mCurrentAnimation.EaseOut(mCurrentProgress, mDefaultFrame);
                    break;
                case AnimationPlayerAction.Start:
                    mLastFrame = mCurrentAnimation.Play(mCurrentProgress, mCurrentParameters.StartFrame, mCurrentParameters.EndFrame);
                    break;
                case AnimationPlayerAction.Stop:
                    mStopped = true;
                    break;
                case AnimationPlayerAction.Rewind:
                    mLastFrame = mCurrentAnimation.Play(1 - mCurrentProgress, mCurrentParameters.StartFrame, mCurrentParameters.EndFrame);
                    break;
                case AnimationPlayerAction.Clear:
                    mLastFrame = (TAnimationResult)mDefaultFrame.Clone();
                    status = IAnimator<TAnimationResult>.Status.Finished;
                    mStopped = true;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return mLastFrame;
        }

        void IDisposable.Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
