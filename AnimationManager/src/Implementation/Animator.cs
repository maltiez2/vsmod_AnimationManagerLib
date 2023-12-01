using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnimationManagerLib.API;
using Vintagestory.API.Common;
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

        public void Init(Category category)
        {
            mDefaultFrame = AnimationFrame.Default(category);
            mStartFrame = AnimationFrame.Default(category);
            mLastFrame = AnimationFrame.Default(category);
        }

        public void Run(AnimationRunMetadata parameters, IAnimation animation)
        {
            mCurrentAnimation = animation;
            mCurrentParameters = parameters;
            mStartFrame = mLastFrame.Clone();
            mProgressModifier = ProgressModifiers.Get(parameters.Modifier);
            mStopped = false;
            mCurrentTime = TimeSpan.Zero;
            mPreviousProgress = mCurrentProgress;
        }

        public AnimationFrame Calculate(TimeSpan timeElapsed, out IAnimator.Status status)
        {
            mCurrentTime += timeElapsed;

            float duration = (float)mCurrentParameters.Duration.TotalSeconds;
            bool instant = mCurrentParameters.Duration == TimeSpan.Zero;

            Debug.Assert(!instant ||
                    mCurrentParameters.Action == AnimationPlayerAction.Set ||
                    mCurrentParameters.Action == AnimationPlayerAction.Stop ||
                    mCurrentParameters.Action == AnimationPlayerAction.Clear,
                    "Only 'Set', 'Stop' and 'Clear' actions can have zero duration"
                );

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

            mCurrentProgress = instant ? 1 : GameMath.Clamp((float)mCurrentTime.TotalSeconds / duration, 0, 1);
            if (mCurrentProgress >= 1)
            {
                mStopped = true;

                switch (mCurrentParameters.Action)
                {
                    case AnimationPlayerAction.Set:
                        mLastFrame = mCurrentAnimation.Play(1, null, mCurrentParameters.TargetFrame);
                        mStopped = true;
                        break;
                    case AnimationPlayerAction.Stop:
                        mStopped = true;
                        break;
                    case AnimationPlayerAction.Clear:
                        mLastFrame = mDefaultFrame.Clone();
                        mStopped = true;
                        break;
                }
            }
            

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
                    mLastFrame = mCurrentAnimation.Play(1, null, mCurrentParameters.TargetFrame);
                    mStopped = true;
                    break;
                case AnimationPlayerAction.EaseIn:
                    mLastFrame = mCurrentAnimation.Blend(1 - mCurrentProgress, mCurrentParameters.TargetFrame, mStartFrame);
                    break;
                case AnimationPlayerAction.EaseOut:
                    mLastFrame = mCurrentAnimation.Blend(mCurrentProgress, mStartFrame, mDefaultFrame);
                    break;
                case AnimationPlayerAction.Play:
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

        private sealed class FixedSizedQueue<T>
        {
            public readonly Queue<T> q = new Queue<T>();
            public int Limit { get; set; }
            public void Enqueue(T obj)
            {
                q.Enqueue(obj);
                T overflow;
                while (q.Count > Limit && q.TryDequeue(out overflow)) ;
            }
        }
        private FixedSizedQueue<float> mProgressPlot = new();

        public void SetUpDebugWindow()
        {
#if DEBUG
            mProgressPlot.Limit = 120;
            mProgressPlot.Enqueue(mCurrentProgress);
            ImGuiNET.ImGui.Begin("Animators status");
            ImGuiNET.ImGui.PlotLines(string.Format("{0}", mCurrentParameters.Action), ref mProgressPlot.q.ToArray()[0], mProgressPlot.q.Count, 0, "", 0, 1.1f, new(0, 100f));
            ImGuiNET.ImGui.End();
#endif
        }
    }
}
