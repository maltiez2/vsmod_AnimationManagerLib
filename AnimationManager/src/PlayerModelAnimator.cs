using System;
using Vintagestory.API.Common;
using AnimationManagerLib.API;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib
{
    public class PlayerModelAnimator<TAnimationResult> : IAnimator<TAnimationResult>
        where TAnimationResult : IAnimationResult
    {
        private TAnimationResult mLastFrame;
        private TAnimationResult mStartFrame;
        private TAnimationResult mDefaultFrame;
        private IAnimation<TAnimationResult> mCurrentAnimation;

        private TimeSpan mCurrentTime;
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
            Console.WriteLine("IAnimator<TAnimationResult>.Run, progress: {0} -> {1}", mPreviousProgress, mCurrentProgress);
            mPreviousProgress = mCurrentProgress;
        }

        TAnimationResult IAnimator<TAnimationResult>.Calculate(TimeSpan timeElapsed, out IAnimator<TAnimationResult>.Status status, ref float weight)
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

            status = mStopped ? IAnimator<TAnimationResult>.Status.Stopped : IAnimator<TAnimationResult>.Status.Running;
            if (mStopped) return mLastFrame;

            mCurrentProgress = GameMath.Clamp((float)mCurrentTime.TotalSeconds / duration, 0, 1);
            if (mCurrentProgress >= 1) mStopped = true;
            //Console.WriteLine("Action: {1}, Progress: {0}, Modified: {2}, Previous: {3}", mCurrentProgress, mCurrentParameters.Action, mProgressModifier(mCurrentProgress), mPreviousProgress);
            

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
                    if (mStopped) status = IAnimator<TAnimationResult>.Status.Finished;
                    break;
                case AnimationPlayerAction.Clear:
                    if (mStopped) status = IAnimator<TAnimationResult>.Status.Finished;
                    break;
                default:
                    break;
            }

            if (mStopped) return mLastFrame;

            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.Set:
                    mLastFrame = mCurrentAnimation.Play(1, mCurrentParameters.StartFrame, mCurrentParameters.EndFrame);
                    mStopped = true;
                    break;
                case AnimationPlayerAction.EaseIn:
                    //weight *= mCurrentProgress;
                    mLastFrame = mCurrentAnimation.Blend(1 - mCurrentProgress, mCurrentParameters.StartFrame, mStartFrame);
                    break;
                case AnimationPlayerAction.EaseOut:
                    //weight *= (1 - mCurrentProgress);
                    mLastFrame = mCurrentAnimation.Blend(mCurrentProgress, mStartFrame, mDefaultFrame);
                    break;
                case AnimationPlayerAction.Start:
                    mLastFrame = mCurrentAnimation.Play(mCurrentProgress, mCurrentParameters.StartFrame, mCurrentParameters.EndFrame);
                    break;
                case AnimationPlayerAction.Stop:
                    mStopped = true;
                    break;
                case AnimationPlayerAction.Rewind:
                    mLastFrame = mCurrentAnimation.Play(1 - mCurrentProgress * mPreviousProgress, mCurrentParameters.StartFrame, mCurrentParameters.EndFrame);
                    break;
                case AnimationPlayerAction.Clear:
                    mLastFrame = (TAnimationResult)mDefaultFrame.Clone();
                    mStopped = true;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return mLastFrame;
        }

        private bool mDisposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!mDisposedValue)
            {
                if (disposing)
                {
                    (mLastFrame as IDisposable)?.Dispose();
                    (mStartFrame as IDisposable)?.Dispose();
                    (mDefaultFrame as IDisposable)?.Dispose();
                    mCurrentAnimation?.Dispose();
                }

                mDisposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        float IAnimator<TAnimationResult>.CalculateProgress(TimeSpan timeElapsed) => CalculateProgress(timeElapsed) ?? 1;

        private float? CalculateProgress(TimeSpan timeElapsed)
        {
            TimeSpan totalTime = mCurrentTime + timeElapsed;
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

            float progress = GameMath.Clamp((float)totalTime.TotalSeconds / duration, 0, 1);
            if (progress >= 1) return null;


            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.EaseOut:
                    progress = 1 - mProgressModifier(1 - progress);
                    break;
                case AnimationPlayerAction.Rewind:
                    progress = 1 - mProgressModifier(1 - progress);
                    break;
                default:
                    progress = mProgressModifier(progress);
                    break;
            }

            return progress;
        }
    }
}
