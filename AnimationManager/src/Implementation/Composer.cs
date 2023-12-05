using System;
using AnimationManagerLib.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using IAnimator = AnimationManagerLib.API.IAnimator;
using System.Linq;
using Vintagestory.API.Util;
using System.Diagnostics;

namespace AnimationManagerLib
{
    public class Composer : IComposer
    { 
        private Type mAnimatorType;
        private readonly Dictionary<AnimationId, IAnimation> mAnimations = new();
        private readonly Dictionary<Category, IAnimator> mAnimators = new();
        private readonly Dictionary<Category, bool> mCategories = new();
        private readonly Dictionary<Category, IComposer.IfRemoveAnimator> mCallbacks = new();
        private readonly AnimationFrame mDefaultFrame;

        public Composer() => mDefaultFrame = AnimationFrame.Default(new("", EnumAnimationBlendMode.Average, 0));

        public void SetAnimatorType<TAnimator>() where TAnimator : IAnimator => mAnimatorType = typeof(TAnimator);
        public bool Register(AnimationId id, IAnimation animation) => mAnimations.TryAdd(id, animation);
        public void Run(AnimationRequest request, IComposer.IfRemoveAnimator finishCallback) => TryAddAnimator(request, finishCallback).Run(request, mAnimations[request]);
        public void Stop(AnimationRequest request) => RemoveAnimator(request);
        
        public AnimationFrame Compose(TimeSpan timeElapsed)
        {
            AnimationFrame composition = mDefaultFrame.Clone();

            IAnimator.Status animatorStatus;

            foreach ((var category, var animator) in mAnimators.Where((entry, _) => mCategories[entry.Key]))
            {
                animator.Calculate(timeElapsed, out animatorStatus).BlendInto(composition);
                ProcessStatus(category, animatorStatus);
            }

            return composition;
        }

        private IAnimator TryAddAnimator(AnimationRequest request, IComposer.IfRemoveAnimator finishCallback)
        {
            if (mCallbacks.ContainsKey(request.Animation.Category)) mCallbacks[request.Animation.Category](false);
            mCallbacks[request.Animation.Category] = finishCallback;
            if (mAnimators.ContainsKey(request.Animation.Category)) return mAnimators[request.Animation.Category];
            IAnimator? animator = Activator.CreateInstance(mAnimatorType) as IAnimator;
            Debug.Assert(animator != null);
            animator.Init(request.Animation.Category);
            mAnimators.Add(request.Animation.Category, animator);
            mCategories[request.Animation.Category] = true;
            return animator;
        }
        private void RemoveAnimator(Category category)
        {
            if (!mAnimators.ContainsKey(category)) return;
            mAnimators.Remove(category);
        }
        private void ProcessStatus(Category category, IAnimator.Status status)
        {
            switch (status)
            {
                case IAnimator.Status.Finished:
                    if (mCallbacks.ContainsKey(category))
                    {
                        var callback = mCallbacks[category];
                        mCallbacks.Remove(category);
                        if (callback(true)) RemoveAnimator(category);
                    }
                    
                    break;
                
                case IAnimator.Status.Stopped:
                    if (mCallbacks.ContainsKey(category))
                    {
                        var callback = mCallbacks[category];
                        mCallbacks.Remove(category);
                        callback(true);
                    }
                    break;
                
                default:
                    break;
            }
        }

        public void SetUpDebugWindow()
        {
#if DEBUG
            ImGuiNET.ImGui.Text(string.Format("Registered animations: {0}", mAnimations.Count));
            ImGuiNET.ImGui.Text(string.Format("Active animators: {0}", mAnimators.Count));
            ImGuiNET.ImGui.NewLine();
            ImGuiNET.ImGui.SeparatorText("Animators");

            foreach ((var category, IAnimator animator) in mAnimators)
            {
                bool collapsed = !ImGuiNET.ImGui.CollapsingHeader($"{category}");
                ImGuiNET.ImGui.Indent();
                
                if (!collapsed)
                {
                    bool enabled = mCategories[category];
                    ImGuiNET.ImGui.Checkbox("Enable", ref enabled);
                    mCategories[category] = enabled;

                    if (!enabled) ImGuiNET.ImGui.BeginDisabled();
                    animator.SetUpDebugWindow();
                    if (!enabled) ImGuiNET.ImGui.EndDisabled();
                }
                ImGuiNET.ImGui.Unindent();
                
            }

            ImGuiNET.ImGui.NewLine();
#endif
        }
    }
}
