using System;
using AnimationManagerLib.API;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace AnimationManagerLib
{
    public class PlayerModelComposer<TAnimationResult> : IComposer<TAnimationResult>
        where TAnimationResult : IAnimationResult
    { 
        private Type mAnimatorType;
        private readonly Dictionary<AnimationId, IAnimation<TAnimationResult>> mAnimations = new();
        private readonly Dictionary<CategoryId, IAnimator<TAnimationResult>> mAnimators = new();
        private readonly Dictionary<CategoryId, IComposer<TAnimationResult>.IfRemoveAnimator> mCallbacks = new();
        private TAnimationResult mDefaultFrame;
        private ICoreAPI mApi;

        void IComposer<TAnimationResult>.Init(ICoreAPI api, TAnimationResult defaultFrame)
        {
            mApi = api;
            mDefaultFrame = defaultFrame;
        }
        void IComposer<TAnimationResult>.SetAnimatorType<TAnimator>() => mAnimatorType = typeof(TAnimator);
        bool IComposer<TAnimationResult>.Register(AnimationId id, IAnimation<TAnimationResult> animation) => mAnimations.TryAdd(id, animation);
        void IComposer<TAnimationResult>.Run(AnimationRequest request, IComposer<TAnimationResult>.IfRemoveAnimator finishCallback) => TryAddAnimator(request, finishCallback).Run(request, mAnimations[request]);
        void IComposer<TAnimationResult>.Stop(AnimationRequest request) => RemoveAnimator(request);

        Composition<TAnimationResult> IComposer<TAnimationResult>.Compose(ComposeRequest request, TimeSpan timeElapsed)
        {
            TAnimationResult sum = mDefaultFrame;
            TAnimationResult averageOnCompose = mDefaultFrame;
            float totalWeightOfTheAverageOnCompose = 0;
            TAnimationResult average = mDefaultFrame;
            float totalWeightOfTheAverage = 0;

            IAnimator<TAnimationResult>.Status animatorStatus;

            foreach ((var category, var animator) in mAnimators)
            {
                switch (category.Blending)
                {
                    case BlendingType.Average:
                        float weight = category.Weight ?? 1;
                        average = (TAnimationResult)average.Average(animator.Calculate(timeElapsed, out animatorStatus), totalWeightOfTheAverage, weight);
                        totalWeightOfTheAverage += weight;
                        break;

                    case BlendingType.AverageOnCompose:
                        float weightOnCompose = category.Weight ?? 1;
                        averageOnCompose = (TAnimationResult)averageOnCompose.Average(animator.Calculate(timeElapsed, out animatorStatus), totalWeightOfTheAverageOnCompose, weightOnCompose);
                        totalWeightOfTheAverageOnCompose += weightOnCompose;
                        break;
                    
                    case BlendingType.Add:
                        sum = (TAnimationResult)sum.Add(animator.Calculate(timeElapsed, out animatorStatus));
                        break;
                    
                    case BlendingType.AddOnCompose:
                        float weightOnComposeToAdd = category.Weight ?? 1;
                        averageOnCompose = (TAnimationResult)averageOnCompose.Add(animator.Calculate(timeElapsed, out animatorStatus));
                        totalWeightOfTheAverageOnCompose += weightOnComposeToAdd;
                        break;

                    default:
                        throw new NotImplementedException();
                }

                ProcessStatus(category, animatorStatus);
            }

            Composition<TAnimationResult> composition = new((TAnimationResult)averageOnCompose.Add(sum), average, totalWeightOfTheAverage);

            return composition;
        }

        void IDisposable.Dispose()
        {
            throw new NotImplementedException();
        }

        private IAnimator<TAnimationResult> TryAddAnimator(AnimationRequest request, IComposer<TAnimationResult>.IfRemoveAnimator finishCallback)
        {
            mCallbacks[request] = finishCallback;
            if (mAnimators.ContainsKey(request)) return mAnimators[request];
            IAnimator<TAnimationResult> animator = Activator.CreateInstance(mAnimatorType) as IAnimator<TAnimationResult>;
            animator.Init(mApi, mDefaultFrame);
            mAnimators.Add(request, animator);
            return animator;
        }

        private void RemoveAnimator(CategoryId category)
        {
            if (!mAnimators.ContainsKey(category)) return;

            IAnimator<TAnimationResult> animator = mAnimators[category];

            mAnimators.Remove(category);

            //animator.Dispose(); // @TODO implement dispose
        }

        private void ProcessStatus(CategoryId category, IAnimator<TAnimationResult>.Status status)
        {
            switch (status)
            {
                case IAnimator<TAnimationResult>.Status.Finished:
                    if (mCallbacks[category]()) RemoveAnimator(category);
                    break;
                
                case IAnimator<TAnimationResult>.Status.Stopped:
                    mCallbacks[category]();
                    break;
                
                default:
                    break;
            }
        }
    }
}
