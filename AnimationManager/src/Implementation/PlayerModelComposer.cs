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
        private readonly Dictionary<uint, IAnimator<TAnimationResult>> mAnimators = new();
        private readonly Dictionary<uint, IComposer<TAnimationResult>.IfRemoveAnimator> mCallbacks = new();
        private readonly Dictionary<uint, CategoryId> mCategories = new();
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

        Composition<TAnimationResult> IComposer<TAnimationResult>.Compose(ComposeRequest request, TimeSpan timeElapsed) // @TODO Why request is not used?
        {
            TAnimationResult sum = (TAnimationResult)mDefaultFrame.Clone();
            TAnimationResult averageOnCompose = (TAnimationResult)mDefaultFrame.Clone();
            float totalWeightOfTheAverageOnCompose = 0;
            TAnimationResult average = (TAnimationResult)mDefaultFrame.Clone();
            float totalWeightOfTheAverage = 0;
            (float totalPreCalcWeight, float totalPreCalcWeightOnCompose) = GetTotalWeights(timeElapsed);

            IAnimator<TAnimationResult>.Status animatorStatus;

            Console.WriteLine("******************************* Compose start ({0}) ***************************", timeElapsed.Milliseconds);
            float dummyWeight = 0;
            foreach ((var category, var animator) in mAnimators)
            {
                switch (mCategories[category].Blending)
                {
                    case BlendingType.Average:
                        float weight = (mCategories[category].Weight ?? 1) / totalPreCalcWeight;
                        Console.WriteLine("Compose - Average - weight: {0}, totalWeightOfTheAverage: {1}", weight, totalWeightOfTheAverage);
                        average = (TAnimationResult)average.Average(animator.Calculate(timeElapsed, out animatorStatus, ref weight), weight, totalWeightOfTheAverage);
                        totalWeightOfTheAverage += weight;
                        break;

                    case BlendingType.AverageOnCompose:
                        float weightOnCompose = (mCategories[category].Weight ?? 1) / totalPreCalcWeightOnCompose;
                        Console.WriteLine("Compose - AverageOnCompose - weight: {0}, totalWeightOfTheAverage: {1}", weightOnCompose, totalWeightOfTheAverageOnCompose);
                        averageOnCompose = (TAnimationResult)averageOnCompose.Average(animator.Calculate(timeElapsed, out animatorStatus, ref weightOnCompose), weightOnCompose, totalWeightOfTheAverageOnCompose);
                        totalWeightOfTheAverageOnCompose += weightOnCompose;
                        break;
                    
                    case BlendingType.Add:
                        Console.WriteLine("Compose - Add");
                        sum = (TAnimationResult)sum.Add(animator.Calculate(timeElapsed, out animatorStatus, ref dummyWeight));
                        break;
                    
                    case BlendingType.AddOnCompose:
                        float weightOnComposeToAdd = (mCategories[category].Weight ?? 1) / totalPreCalcWeight;
                        Console.WriteLine("Compose - AddOnCompose - weight: {0}, totalWeightOfTheAverage: {1}", weightOnComposeToAdd, totalWeightOfTheAverageOnCompose);
                        averageOnCompose = (TAnimationResult)averageOnCompose.Add(animator.Calculate(timeElapsed, out animatorStatus, ref weightOnComposeToAdd));
                        totalWeightOfTheAverageOnCompose += weightOnComposeToAdd;
                        break;

                    default:
                        throw new NotImplementedException();
                }

                ProcessStatus(mCategories[category], animatorStatus);
            }
            Composition<TAnimationResult> composition = new((TAnimationResult)averageOnCompose.Add(sum), average, totalPreCalcWeight);
            Console.WriteLine("Compose - composition - totalWeightOfTheAverage: {0} ({1})", totalWeightOfTheAverage, totalPreCalcWeight);
            Console.WriteLine("******************************* Compose stop ({0}) ***************************", timeElapsed.Milliseconds);

            return composition;
        }

        private IAnimator<TAnimationResult> TryAddAnimator(AnimationRequest request, IComposer<TAnimationResult>.IfRemoveAnimator finishCallback)
        {
            mCallbacks[request.Category.Hash] = finishCallback;
            mCategories[request.Category.Hash] = request;
            if (mAnimators.ContainsKey(request.Category.Hash)) return mAnimators[request.Category.Hash];
            IAnimator<TAnimationResult> animator = Activator.CreateInstance(mAnimatorType) as IAnimator<TAnimationResult>;
            animator.Init(mApi, mDefaultFrame);
            mAnimators.Add(request.Category.Hash, animator);
            Console.WriteLine("**************** new animator {0} *************", mAnimators.Count);
            return animator;
        }

        private void RemoveAnimator(CategoryId category)
        {
            if (!mAnimators.ContainsKey(category.Hash)) return;

            Console.WriteLine("**************** remove animator {0} *************", mAnimators.Count);

            IAnimator<TAnimationResult> animator = mAnimators[category.Hash];

            mAnimators.Remove(category.Hash);

            animator.Dispose();
        }

        private void ProcessStatus(CategoryId category, IAnimator<TAnimationResult>.Status status)
        {
            switch (status)
            {
                case IAnimator<TAnimationResult>.Status.Finished:
                    Console.WriteLine("**************** ProcessStatus {0} *************", status);
                    if (mCallbacks[category.Hash]()) RemoveAnimator(category);
                    break;
                
                case IAnimator<TAnimationResult>.Status.Stopped:
                    mCallbacks[category.Hash]();
                    break;
                
                default:
                    break;
            }
        }

        private (float weight, float weightOnCompose) GetTotalWeights(TimeSpan timeElapsed)
        {
            float totalWeightOfTheAverageOnCompose = 0;
            float totalWeightOfTheAverage = 0;

            foreach ((var category, var animator) in mAnimators)
            {
                switch (mCategories[category].Blending)
                {
                    case BlendingType.Average:
                        float weight = mCategories[category].Weight ?? 1;
                        totalWeightOfTheAverage += weight;
                        break;

                    case BlendingType.AverageOnCompose:
                        float weightOnCompose = mCategories[category].Weight ?? 1;
                        totalWeightOfTheAverageOnCompose += weightOnCompose;
                        break;

                    case BlendingType.AddOnCompose:
                        float weightOnComposeToAdd = mCategories[category].Weight ?? 1;
                        totalWeightOfTheAverageOnCompose += weightOnComposeToAdd;
                        break;

                    default:
                        break;
                }
            }

            return (totalWeightOfTheAverage, totalWeightOfTheAverageOnCompose);
        }

        private bool mDisposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!mDisposedValue)
            {
                if (disposing)
                {
                    foreach ((_, var animator) in mAnimations)
                    {
                        animator.Dispose();
                    }
                }

                mDisposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
