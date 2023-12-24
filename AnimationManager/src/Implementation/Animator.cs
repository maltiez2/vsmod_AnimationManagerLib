using System;
using System.Diagnostics;
using AnimationManagerLib.API;
using Vintagestory.API.MathTools;
using VSImGui;
using IAnimator = AnimationManagerLib.API.IAnimator;

namespace AnimationManagerLib
{
    internal class Animator : IAnimator
    {
        private AnimationFrame mLastFrame;
        private AnimationFrame mStartFrame;
        private readonly AnimationFrame mDefaultFrame;
        private IAnimation mCurrentAnimation;
        private TimeSpan mCurrentTime = TimeSpan.Zero;
        private AnimationRunMetadata mCurrentParameters;
        private ProgressModifiers.ProgressModifier mProgressModifier;
        private bool mStopped = false;
        private float mCurrentProgress = 1;
        private float mPreviousProgress = 1;

        public Animator(Category category, AnimationRunMetadata parameters, IAnimation animation)
        {
            mDefaultFrame = AnimationFrame.Default(category);
            mStartFrame = AnimationFrame.Default(category);
            mLastFrame = AnimationFrame.Default(category);
            
            mCurrentAnimation = animation;
            mCurrentParameters = parameters;
            mProgressModifier = ProgressModifiers.Get(parameters.Modifier);
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

            status = mStopped ? IAnimator.Status.Stopped : IAnimator.Status.Running;
            if (mStopped) return mLastFrame;

            float duration = (float)mCurrentParameters.Duration.TotalSeconds;

            AdjustDuration(ref duration);
            CalculateProgress(duration);

            if (mStopped)
            {
                OnStopped(ref status);
                return mLastFrame;
            }

            ModifyProgress();
            CalculateFrame();

            return mLastFrame;
        }

        private void AdjustDuration(ref float duration)
        {
            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.EaseOut:
                    duration *= mPreviousProgress;
                    break;
                case AnimationPlayerAction.Rewind:
                    duration *= mPreviousProgress;
                    break;
                default:
                    break;
            }
        }
        private void CalculateProgress(float duration)
        {
            bool instant = mCurrentParameters.Duration == TimeSpan.Zero;

            /*Debug.Assert(!instant ||
                    mCurrentParameters.Action == AnimationPlayerAction.Set ||
                    mCurrentParameters.Action == AnimationPlayerAction.Stop ||
                    mCurrentParameters.Action == AnimationPlayerAction.Clear,
                    "Only 'Set', 'Stop' and 'Clear' actions can have zero duration"
                );*/

            mCurrentProgress = instant ? 1 : GameMath.Clamp((float)mCurrentTime.TotalSeconds / duration, 0, 1);
            mStopped = mCurrentProgress >= 1;
        }
        private void ModifyProgress()
        {
            mCurrentProgress = mCurrentParameters.Action switch
            {
                AnimationPlayerAction.EaseOut => 1 - mProgressModifier(1 - mCurrentProgress),
                AnimationPlayerAction.Rewind => 1 - mProgressModifier(1 - mCurrentProgress),
                _ => mProgressModifier(mCurrentProgress),
            };
        }
        private void CalculateFrame()
        {
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
        }
        private void OnStopped(ref IAnimator.Status status)
        {
            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.Set:
                    mLastFrame = mCurrentAnimation.Play(1, null, mCurrentParameters.TargetFrame);
                    break;
                case AnimationPlayerAction.Stop:
                    break;
                case AnimationPlayerAction.Clear:
                    mLastFrame = mDefaultFrame.Clone();
                    status = IAnimator.Status.Finished;
                    break;
                case AnimationPlayerAction.EaseOut:
                    status = IAnimator.Status.Finished;
                    break;
            }
        }

#if DEBUG
        private readonly FixedSizedQueue<float> mProgressPlot = new(120);
#endif
        public void SetUpDebugWindow()
        {
#if DEBUG
            mProgressPlot.Enqueue(mCurrentProgress);
            ImGuiNET.ImGui.Text($"{mCurrentAnimation}");
            ImGuiNET.ImGui.Text($"Action: {mCurrentParameters.Action}");
            ImGuiNET.ImGui.Text($"Duration: {mCurrentParameters.Duration}");

            if (!mStopped)
                ImGuiNET.ImGui.Text($"Modifier: {mCurrentParameters.Modifier}");
            else
                ImGuiNET.ImGui.Text($"Modifier: -");
            
            if (mStopped)
                ImGuiNET.ImGui.SliderFloat($"Prev. progress ({mCurrentAnimation.GetHashCode()})", ref mPreviousProgress, 0, 1);
            else
            {
                ImGuiNET.ImGui.BeginDisabled();
                ImGuiNET.ImGui.SliderFloat($"Prev. progress ({mCurrentAnimation.GetHashCode()})", ref mPreviousProgress, 0, 1);
                ImGuiNET.ImGui.EndDisabled();
            }
            
            ImGuiNET.ImGui.PlotLines("Curr. progress", ref mProgressPlot.Queue.ToArray()[0], mProgressPlot.Count, 0, "", 0, 1.1f, new(0, 100f));
            ImGuiNET.ImGui.NewLine();
#endif
        }
    }
}
