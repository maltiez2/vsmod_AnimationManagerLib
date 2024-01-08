using AnimationManagerLib.API;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using IAnimator = AnimationManagerLib.API.IAnimator;

namespace AnimationManagerLib;

internal class Composer : IComposer
{
    private readonly Dictionary<AnimationId, IAnimation> mAnimations = new();
    private readonly Dictionary<Category, IAnimator> mAnimators = new();
    private readonly Dictionary<Category, bool> mCategories = new();
    private readonly Dictionary<Category, IComposer.IfRemoveAnimator> mCallbacks = new();
    private readonly AnimationFrame mDefaultFrame;

    public Composer() => mDefaultFrame = AnimationFrame.Default(new("", EnumAnimationBlendMode.Average, 0));

    public bool Register(AnimationId id, IAnimation animation) => mAnimations.TryAdd(id, animation);
    public void Run(AnimationRequest request, IComposer.IfRemoveAnimator finishCallback)
    {
        if (mCallbacks.ContainsKey(request.Animation.Category)) mCallbacks[request.Animation.Category](false);

        mCallbacks[request.Animation.Category] = finishCallback;

        if (mAnimators.ContainsKey(request.Animation.Category))
        {
            mAnimators[request.Animation.Category].Run(request, mAnimations[request]);
            return;
        }

        IAnimator animator = new Animator(request.Animation.Category, request, mAnimations[request]);
        mAnimators.Add(request.Animation.Category, animator);
        mCategories[request.Animation.Category] = true;
    }
    public void Stop(Category request)
    {
        if (!mAnimators.ContainsKey(request)) return;
        mAnimators.Remove(request);
    }
    public AnimationFrame Compose(TimeSpan timeElapsed)
    {
        AnimationFrame composition = mDefaultFrame.Clone();

        foreach ((Category category, IAnimator? animator) in mAnimators.Where((entry, _) => mCategories[entry.Key]))
        {
            animator.Calculate(timeElapsed, out IAnimator.Status animatorStatus).BlendInto(composition);
            ProcessStatus(category, animatorStatus);
        }

        return composition;
    }

    private void ProcessStatus(Category category, IAnimator.Status status)
    {
        switch (status)
        {
            case IAnimator.Status.Finished:
                if (mCallbacks.ContainsKey(category))
                {
                    IComposer.IfRemoveAnimator callback = mCallbacks[category];
                    mCallbacks.Remove(category);
                    if (callback(true)) Stop(category);
                }

                break;

            case IAnimator.Status.Stopped:
                if (mCallbacks.ContainsKey(category))
                {
                    IComposer.IfRemoveAnimator callback = mCallbacks[category];
                    mCallbacks.Remove(category);
                    callback(true);
                }
                break;

            default:
                break;
        }
    }

    public void SetUpDebugWindow(string id)
    {
#if DEBUG
        ImGuiNET.ImGui.Text(string.Format("Registered animations: {0}", mAnimations.Count));
        ImGuiNET.ImGui.Text(string.Format("Active animators: {0}", mAnimators.Count));
        ImGuiNET.ImGui.NewLine();
        ImGuiNET.ImGui.SeparatorText("Animators");

        foreach ((Category category, IAnimator animator) in mAnimators)
        {
            bool collapsed = !ImGuiNET.ImGui.CollapsingHeader($"{category}##{id}");
            ImGuiNET.ImGui.Indent();

            if (!collapsed)
            {
                bool enabled = mCategories[category];
                ImGuiNET.ImGui.Checkbox($"Enable##{id}{category}", ref enabled);
                mCategories[category] = enabled;

                if (!enabled) ImGuiNET.ImGui.BeginDisabled();
                animator.SetUpDebugWindow($"{id}{category}");
                if (!enabled) ImGuiNET.ImGui.EndDisabled();
            }
            ImGuiNET.ImGui.Unindent();

        }

        ImGuiNET.ImGui.NewLine();
#endif
    }
}
