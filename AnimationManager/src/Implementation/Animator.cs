using System;
using AnimationManagerLib.API;
using Vintagestory.API.MathTools;
using IAnimator = AnimationManagerLib.API.IAnimator;

namespace AnimationManagerLib
{
    public class Animator : IAnimator
    {
        private AnimationFrame mLastFrame;
        private AnimationFrame mStartFrame;
        private AnimationFrame mDefaultFrame;
        private IAnimation mCurrentAnimation;

        private TimeSpan mCurrentTime;
        private AnimationRunMetadata mCurrentParameters;
        private ProgressModifiers.ProgressModifier mProgressModifier;
        private bool mStopped;
        private float mCurrentProgress = 1;
        private float mPreviousProgress = 1;

        void IAnimator.Init(CategoryId category)
        {
            mDefaultFrame = AnimationFrame.Default(category);
            mStartFrame = AnimationFrame.Default(category);
            mLastFrame = AnimationFrame.Default(category);
        }

        void IAnimator.Run(AnimationRunMetadata parameters, IAnimation animation)
        {
            mCurrentAnimation = animation;
            mCurrentParameters = parameters;
            mStartFrame = mLastFrame.Clone();
            mProgressModifier = ProgressModifiers.Get(parameters.Modifier);
            mStopped = false;
            mCurrentTime = new TimeSpan(0);
            mPreviousProgress = mCurrentProgress;
        }

        AnimationFrame IAnimator.Calculate(TimeSpan timeElapsed, out IAnimator.Status status)
        {
            mCurrentTime += timeElapsed;

            float duration = (float)mCurrentParameters.Duration.TotalSeconds;

            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.EaseOut:
                    duration = duration * mPreviousProgress;
                    break;
                case AnimationPlayerAction.Rewind:
                    duration = duration * mPreviousProgress;
                    break;
                default:
                    break;
            }

            status = mStopped ? IAnimator.Status.Stopped : IAnimator.Status.Running;
            if (mStopped) return mLastFrame;

            mCurrentProgress = GameMath.Clamp((float)mCurrentTime.TotalSeconds / duration, 0, 1);
            if (mCurrentProgress >= 1) mStopped = true;
            

            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.EaseOut:
                    mCurrentProgress = 1 - mProgressModifier(1 - mCurrentProgress);
                    break;
                case AnimationPlayerAction.Rewind:
                    mCurrentProgress = 1 - mProgressModifier(1 - mCurrentProgress);
                    break;
                default:
                    mCurrentProgress = mProgressModifier(mCurrentProgress);
                    break;
            }

            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.EaseOut:
                    if (mStopped) status = IAnimator.Status.Finished;
                    break;
                case AnimationPlayerAction.Clear:
                    if (mStopped) status = IAnimator.Status.Finished;
                    break;
                default:
                    break;
            }

            if (mStopped) return mLastFrame;

            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.Set:
                    mLastFrame = mCurrentAnimation.Play(1, mCurrentParameters.StartFrame, mCurrentParameters.TargetFrame);
                    mStopped = true;
                    break;
                case AnimationPlayerAction.EaseIn:
                    mLastFrame = mCurrentAnimation.Blend(1 - mCurrentProgress, mCurrentParameters.StartFrame, mStartFrame);
                    break;
                case AnimationPlayerAction.EaseOut:
                    mLastFrame = mCurrentAnimation.Blend(mCurrentProgress, mStartFrame, mDefaultFrame);
                    break;
                case AnimationPlayerAction.Start:
                    mLastFrame = mCurrentAnimation.Play(mCurrentProgress, mCurrentParameters.StartFrame, mCurrentParameters.TargetFrame);
                    break;
                case AnimationPlayerAction.Stop:
                    mStopped = true;
                    break;
                case AnimationPlayerAction.Rewind:
                    mLastFrame = mCurrentAnimation.Play(1 - mCurrentProgress * mPreviousProgress, mCurrentParameters.StartFrame, mCurrentParameters.TargetFrame);
                    break;
                case AnimationPlayerAction.Clear:
                    mLastFrame = mDefaultFrame.Clone();
                    mStopped = true;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return mLastFrame;
        }
    }
}
