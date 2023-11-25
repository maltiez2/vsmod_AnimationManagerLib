using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AnimationManagerLib.Extra
{
    public class ItemAnimationBehavior : CollectibleBehavior
    {
        private API.IAnimationManager mAnimationManager;
        private JsonObject mProperties;
        private API.AnimationRequest[] mHeldTpHitAnimation;
        private const string mHeldTpHitAnimationAttrName = "heldInteraction_guid";
        private bool mClientSide;
        private ICoreAPI mApi;

        public ItemAnimationBehavior(CollectibleObject collObj) : base(collObj)
        {
        }
        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            mProperties = properties;
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            mClientSide = api.Side == EnumAppSide.Client;
            if (!mClientSide) return;

            mApi = api;
            mAnimationManager = (api.ModLoader.GetModSystem<AnimationManagerLibSystem>() as API.IAnimationManagerProvider).GetAnimationManager();

            List<API.AnimationRequest> requests = new();
            foreach (JsonObject requestDefinition in mProperties["heldInteraction"].AsArray())
            {
                API.AnimationRequest request = API.Utils.AnimationRequestFromJson(requestDefinition);
                requests.Add(request);
                mAnimationManager.Register(request.Animation, requestDefinition["animation"].AsString());
            }

            mHeldTpHitAnimation = requests.ToArray();
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (mClientSide && slot.Itemstack.TempAttributes.HasAttribute(mHeldTpHitAnimationAttrName)) StopAnimation(slot);

            handHandling = EnumHandHandling.PreventDefaultAnimation;
            handling = EnumHandling.PreventSubsequent;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (mClientSide && !slot.Itemstack.TempAttributes.HasAttribute(mHeldTpHitAnimationAttrName)) StartAnimation(slot, byEntity);
            handling = EnumHandling.PreventSubsequent;
            return true;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefaultAnimation;
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (mClientSide && slot.Itemstack.TempAttributes.HasAttribute(mHeldTpHitAnimationAttrName)) StopAnimation(slot);
            handling = EnumHandling.PreventSubsequent;
        }

        private void StopAnimation(ItemSlot slot)
        {
            Guid runId = new(slot.Itemstack.TempAttributes.GetBytes(mHeldTpHitAnimationAttrName));
            mAnimationManager.Stop(runId);
            slot.Itemstack.TempAttributes.RemoveAttribute(mHeldTpHitAnimationAttrName);
        }
        private void StartAnimation(ItemSlot slot, EntityAgent byEntity)
        {
            Guid runId = mAnimationManager.Run(byEntity.EntityId, mHeldTpHitAnimation);
            slot.Itemstack.TempAttributes.SetBytes(mHeldTpHitAnimationAttrName, runId.ToByteArray());
        }
    }
}
