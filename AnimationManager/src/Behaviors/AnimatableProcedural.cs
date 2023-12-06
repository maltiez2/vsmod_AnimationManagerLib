using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using AnimationManagerLib.API;
using Vintagestory.API.Client;

namespace AnimationManagerLib.CollectibleBehaviors
{
    public class AnimatableProcedural : AnimatableAttachable, API.IAnimatableBehavior
    {
        private readonly List<AnimationId> mRegisteredAnimations = new();
        private readonly HashSet<Guid> mRunningAnimations = new();
        protected ICoreAPI? mApi;
        protected AnimationManagerLibSystem? mModSystem;

        public AnimatableProcedural(CollectibleObject collObj) : base(collObj)
        {

        }

        public override void OnLoaded(ICoreAPI api)
        {
            mModSystem = api.ModLoader.GetModSystem<AnimationManagerLibSystem>();
            mApi = api;

            base.OnLoaded(api);
        }

        public int RegisterAnimation(string code, string category, bool cyclic = false, EnumAnimationBlendMode categoryBlendMode = EnumAnimationBlendMode.Add, float? categoryWeight = null, Dictionary<string, EnumAnimationBlendMode>? elementBlendMode = null, Dictionary<string, float>? elementWeight = null)
        {
            if (mApi?.Side != EnumAppSide.Client)
            {
                mApi?.Logger.Warning("Trying to register animation '{0}' in category '{1}' on server side. Animations can be registered only on client side, skipping", code, category);
                return -1;
            }
            AnimationId id = new(category, code, categoryBlendMode, categoryWeight);

            if (CurrentShape == null)
            {
                mApi.Logger.Warning("Trying to register animation '{0}' in category '{1}'. 'CurrentShape' is null, skipping.", code, category);
                return -1;
            }

            AnimationData animation = AnimationData.HeldItem(code, CurrentShape);
            mModSystem?.Register(id, animation);
            mRegisteredAnimations.Add(id);
            return mRegisteredAnimations.Count - 1;
        }

        public Guid RunAnimation(int id, params RunParameters[] parameters)
        {
            if (mApi?.Side != EnumAppSide.Client)
            {
                mApi?.Logger.Warning("Trying to run animation with id '{0}' on server side. Animations can be run only from client side, skipping", id);
                return Guid.Empty;
            }
            if (mRegisteredAnimations.Count <= id)
            {
                mClientApi?.Logger.Error("Animation with id '{0}' is not registered. Number of registered animations: {1}", id, mRegisteredAnimations.Count);
                return Guid.Empty;
            }
            
            AnimationRequest[] requests = new AnimationRequest[parameters.Length];

            for (int index = 0; index < parameters.Length; index++)
            {
                requests[index] = new AnimationRequest(mRegisteredAnimations[id], parameters[index]);
            }

            Guid? runId = mModSystem?.Run(AnimationTarget.HeldItem(), new(requests));
            if (runId == null) return Guid.Empty;
            mRunningAnimations.Add(runId.Value);
            return runId.Value;
        }

        public void StopAnimation(Guid runId)
        {
            if (mApi?.Side != EnumAppSide.Client)
            {
                mApi?.Logger.Warning("Trying to stop animation with run id '{0}' on server side. Animations can be stopped only from client side, skipping", runId);
                return;
            }
            if (mRunningAnimations.Contains(runId)) mRunningAnimations.Remove(runId);
            mModSystem?.Stop(runId);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            RenderProceduralAnimations = mRunningAnimations.Count > 0 || !onlyWhenAnimating;

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }
    }
}
