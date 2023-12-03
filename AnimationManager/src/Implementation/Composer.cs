using System;
using AnimationManagerLib.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using IAnimator = AnimationManagerLib.API.IAnimator;

namespace AnimationManagerLib
{
    public class Composer : IComposer
    { 
        private Type mAnimatorType;
        private readonly Dictionary<AnimationId, IAnimation> mAnimations = new();
        private readonly Dictionary<uint, IAnimator> mAnimators = new();
        private readonly Dictionary<uint, IComposer.IfRemoveAnimator> mCallbacks = new();
        private readonly Dictionary<uint, Category> mCategories = new();
        private readonly AnimationFrame mDefaultFrame;

        private Guid mId;

        public Composer()
        {
            mDefaultFrame = AnimationFrame.Default(new(0, EnumAnimationBlendMode.Average, 0));
            mId = Guid.NewGuid();
        }

        public void SetAnimatorType<TAnimator>()
            where TAnimator : IAnimator
        {
            mAnimatorType = typeof(TAnimator);
        }
        public bool Register(AnimationId id, IAnimation animation) => mAnimations.TryAdd(id, animation);
        public void Run(AnimationRequest request, IComposer.IfRemoveAnimator finishCallback) => TryAddAnimator(request, finishCallback).Run(request, mAnimations[request]);
        public void Stop(AnimationRequest request) => RemoveAnimator(request);
        public AnimationFrame Compose(TimeSpan timeElapsed)
        {
            AnimationFrame composition = mDefaultFrame.Clone();

            IAnimator.Status animatorStatus;

            foreach ((var category, var animator) in mAnimators)
            {
                animator.Calculate(timeElapsed, out animatorStatus).BlendInto(composition);
                ProcessStatus(mCategories[category], animatorStatus);
            }

            return composition;
        }

        private IAnimator TryAddAnimator(AnimationRequest request, IComposer.IfRemoveAnimator finishCallback)
        {
            //if (mCallbacks.ContainsKey(request.Animation.Category.Hash)) mCallbacks[request.Animation.Category.Hash]();
            mCallbacks[request.Animation.Category.Hash] = finishCallback;
            mCategories[request.Animation.Category.Hash] = request;
            if (mAnimators.ContainsKey(request.Animation.Category.Hash)) return mAnimators[request.Animation.Category.Hash];
            IAnimator animator = Activator.CreateInstance(mAnimatorType) as IAnimator;
            animator.Init(request.Animation.Category);
            mAnimators.Add(request.Animation.Category.Hash, animator);
            return animator;
        }

        private void RemoveAnimator(Category category)
        {
            if (!mAnimators.ContainsKey(category.Hash)) return;
            mAnimators.Remove(category.Hash);
        }

        private void ProcessStatus(Category category, IAnimator.Status status)
        {
            switch (status)
            {
                case IAnimator.Status.Finished:
                    if (mCallbacks[category.Hash]()) RemoveAnimator(category);
                    break;
                
                case IAnimator.Status.Stopped:
                    mCallbacks[category.Hash]();
                    break;
                
                default:
                    break;
            }
        }

        public void SetUpDebugWindow()
        {
#if DEBUG
            ImGuiNET.ImGui.Begin("Composer status");
            ImGuiNET.ImGui.Text(string.Format("Composer id: {0}", mId));
            ImGuiNET.ImGui.Text(string.Format("Registered animations: {0}", mAnimations.Count));
            ImGuiNET.ImGui.Text(string.Format("Active categories: {0}", mCategories.Count));
            ImGuiNET.ImGui.Text(string.Format("Active animators: {0}", mAnimators.Count));
            ImGuiNET.ImGui.NewLine();
            ImGuiNET.ImGui.End();

            foreach ((_, IAnimator animator) in mAnimators)
            {
                animator.SetUpDebugWindow();
            }
#endif
        }
    }
}
