using System;
using AnimationManagerLib.API;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace AnimationManagerLib
{
    public class PlayerModelComposer<TAnimationResult> : IAnimationComposer<TAnimationResult>
        where TAnimationResult : IAnimationResult
    { 
        private Type mAnimatorType;
        private readonly Dictionary<AnimationId, IAnimation<TAnimationResult>> mAnimations = new();
        private readonly Dictionary<CategoryId, IAnimator<TAnimationResult>> mAnimators = new();
        private readonly Dictionary<CategoryId, IAnimationComposer<TAnimationResult>.IfRemoveAnimator> mCallbacks = new();
        private TAnimationResult mDefaultFrame;
        private ICoreAPI mApi;

        void IAnimationComposer<TAnimationResult>.Init(ICoreAPI api, TAnimationResult defaultFrame)
        {
            mApi = api;
            mDefaultFrame = defaultFrame;
        }
        void IAnimationComposer<TAnimationResult>.SetAnimatorType<TAnimator>() => mAnimatorType = typeof(TAnimator);
        bool IAnimationComposer<TAnimationResult>.Register(AnimationId id, IAnimation<TAnimationResult> animation) => mAnimations.TryAdd(id, animation);
        void IAnimationComposer<TAnimationResult>.Run(AnimationRequest request, IAnimationComposer<TAnimationResult>.IfRemoveAnimator finishCallback) => TryAddAnimator(request, finishCallback).Run(request, mAnimations[request]);
        void IAnimationComposer<TAnimationResult>.Stop(AnimationRequest request) => RemoveAnimator(request);

        Composition<TAnimationResult> IAnimationComposer<TAnimationResult>.Compose(ComposeRequest request, TimeSpan timeElapsed)
        {
            TAnimationResult sum = mDefaultFrame;
            TAnimationResult averageOnCompose = mDefaultFrame;
            float totalWeightOfTheAverageOnCompose = 1;
            TAnimationResult average = mDefaultFrame;
            float totalWeightOfTheAverage = 1;

            IAnimator<TAnimationResult>.Status animatorStatus;

            foreach ((var category, var animator) in mAnimators)
            {
                switch (category.Blending)
                {
                    case BlendingType.Average:
                        float weight = category.Weight ?? 1;
                        average.Average(animator.Calculate(timeElapsed, out animatorStatus), totalWeightOfTheAverage, weight);
                        totalWeightOfTheAverage += weight;
                        break;

                    case BlendingType.AverageOnCompose:
                        float weightOnCompose = category.Weight ?? 1;
                        averageOnCompose.Average(animator.Calculate(timeElapsed, out animatorStatus), totalWeightOfTheAverageOnCompose, weightOnCompose);
                        totalWeightOfTheAverageOnCompose += weightOnCompose;
                        break;
                    
                    case BlendingType.Add:
                        sum.Add(animator.Calculate(timeElapsed, out animatorStatus));
                        break;
                    
                    case BlendingType.Subtract:
                        sum.Subtract(animator.Calculate(timeElapsed, out animatorStatus));
                        break;

                    default:
                        throw new NotImplementedException();
                }

                ProcessStatus(category, animatorStatus);
            }

            return new( ((TAnimationResult)averageOnCompose.Add(sum), average, totalWeightOfTheAverage) );
        }

        void IDisposable.Dispose()
        {
            throw new NotImplementedException();
        }

        private IAnimator<TAnimationResult> TryAddAnimator(AnimationRequest request, IAnimationComposer<TAnimationResult>.IfRemoveAnimator finishCallback)
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
