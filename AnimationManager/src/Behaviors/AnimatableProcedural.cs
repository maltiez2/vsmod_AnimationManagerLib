using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using AnimationManagerLib.API;
using Vintagestory.API.Client;
using HarmonyLib;
using System.Linq;

namespace AnimationManagerLib.CollectibleBehaviors
{
    public class AnimatableProcedural : AnimatableAttachable, API.IAnimatableBehavior
    {
        private readonly List<AnimationId> mRegisteredAnimationsTp = new();
        private readonly List<AnimationId> mRegisteredAnimationsFp = new();
        private readonly List<AnimationId> mRegisteredAnimationsIfp = new();
        private readonly HashSet<Guid> mRunningAnimations = new();
        private readonly Dictionary<Guid, (Guid fp, Guid ifp)> mRunningAnimationsFp = new();
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
            AnimationId fp = new(category, $"{code}-fp", categoryBlendMode, categoryWeight);
            AnimationId ifp = new(category, $"{code}-ifp", categoryBlendMode, categoryWeight);

            if (mShape?.Shape == null)
            {
                mApi.Logger.Warning("Trying to register animation '{0}' in category '{1}'. 'CurrentShape' is null, skipping.", code, category);
                return -1;
            }

            AnimationData animationTp = AnimationData.HeldItem(code, mShape?.Shape);
            mModSystem?.Register(id, animationTp);
            mRegisteredAnimationsTp.Add(id);

            AnimationData animationFp = AnimationData.HeldItem(code, mShape?.Shape);
            mModSystem?.Register(fp, animationFp);
            mRegisteredAnimationsFp.Add(fp);

            AnimationData animationIfp = AnimationData.HeldItem(code, mShape?.Shape);
            mModSystem?.Register(ifp, animationIfp);
            mRegisteredAnimationsIfp.Add(ifp);

            return mRegisteredAnimationsTp.Count - 1;
        }

        public Guid RunAnimation(int id, params RunParameters[] parameters)
        {
            if (mApi?.Side != EnumAppSide.Client)
            {
                mApi?.Logger.Warning("Trying to run animation with id '{0}' on server side. Animations can be run only from client side, skipping", id);
                return Guid.Empty;
            }
            if (mRegisteredAnimationsTp.Count <= id)
            {
                mClientApi?.Logger.Error("Animation with id '{0}' is not registered. Number of registered animations: {1}", id, mRegisteredAnimationsTp.Count);
                return Guid.Empty;
            }

            Guid tp = RunAnimation(mRegisteredAnimationsTp[id], parameters);
            Guid fp = RunAnimation(mRegisteredAnimationsFp[id], parameters);
            Guid ifp = RunAnimation(mRegisteredAnimationsIfp[id], parameters);

            if (tp != Guid.Empty)
            {
                mRunningAnimationsFp.Add(tp, (fp, ifp));
            }
            
            return tp;
        }

        private Guid RunAnimation(AnimationId id, params RunParameters[] parameters)
        {
            AnimationRequest[] requests = new AnimationRequest[parameters.Length];

            for (int index = 0; index < parameters.Length; index++)
            {
                requests[index] = new AnimationRequest(id, parameters[index]);
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
            if (mRunningAnimationsFp.ContainsKey(runId))
            {
                mModSystem?.Stop(mRunningAnimationsFp[runId].fp);
                mModSystem?.Stop(mRunningAnimationsFp[runId].ifp);
                mRunningAnimationsFp.Remove(runId);
            }
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            RenderProceduralAnimations = mRunningAnimations.Count > 0 || !mOnlyWhenAnimating;

            base.BeforeRender(capi, itemstack, target, ref renderinfo);
        }
    }
}
