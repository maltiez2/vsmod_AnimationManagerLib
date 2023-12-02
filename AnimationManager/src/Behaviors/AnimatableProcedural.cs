using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using AnimationManagerLib.API;

namespace AnimationManagerLib.CollectibleBehaviors
{
    public class AnimatableProcedural : AnimatableAttachable, API.IAnimatableBehavior
    {
        private API.IAnimationManager mAnimationManager;
        private readonly List<AnimationId> mRegisteredAnimations = new();

        public AnimatableProcedural(CollectibleObject collObj) : base(collObj)
        {
            if (mApi.Side == EnumAppSide.Client)  RenderAnyway = true;
        }

        public int RegisterAnimation(string code, string category, bool cyclic = false, EnumAnimationBlendMode categoryBlendMode = EnumAnimationBlendMode.Add, float? categoryWeight = null, Dictionary<string, EnumAnimationBlendMode> elementBlendMode = null, Dictionary<string, float> elementWeight = null)
        {
            if (mApi.Side != EnumAppSide.Client)
            {
                mApi.Logger.Warning("Trying to register animation '{0}' in category '{1}' on server side. Animations can be registered only on client side, skipping", code, category);
                return -1;
            }
            AnimationId id = new(code, category, categoryBlendMode, categoryWeight);
            mAnimationManager = capi.ModLoader.GetModSystem<AnimationManagerLibSystem>().GetAnimationManager();
            AnimationData animation = AnimationData.HeldItem(code, CurrentShape);
            mAnimationManager.Register(id, animation);
            mRegisteredAnimations.Add(id);
            return mRegisteredAnimations.Count - 1;
        }

        public Guid RunAnimation(int id, params RunParameters[] parameters)
        {
            if (mApi.Side != EnumAppSide.Client)
            {
                mApi.Logger.Warning("Trying to run animation with id '{0}' on server side. Animations can be run only from client side, skipping", id);
                return Guid.Empty;
            }
            if (mRegisteredAnimations.Count <= id)
            {
                capi.Logger.Error("Animation with id '{0}' is not registered. Number of registered animations: {1}", id, mRegisteredAnimations.Count);
                return Guid.Empty;
            }
            
            AnimationRequest[] requests = new AnimationRequest[parameters.Length];

            for (int index = 0; index < parameters.Length; index++)
            {
                requests[index] = new AnimationRequest(mRegisteredAnimations[id], parameters[index]);
            }

            return mAnimationManager.Run(new(AnimationTargetType.HeldItemFp), requests);
        }

        public void StopAnimation(Guid runId)
        {
            if (mApi.Side != EnumAppSide.Client)
            {
                mApi.Logger.Warning("Trying to stop animation with run id '{0}' on server side. Animations can be stopped only from client side, skipping", runId);
                return;
            }
            mAnimationManager.Stop(runId);
        }
    }
}
