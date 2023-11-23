using AnimationManagerLib.API;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AnimationManagerLib.Extra
{
    public class ItemAnimationBehavior : CollectibleBehavior
    {
        private API.IAnimationManager mAnimationManager;
        private JsonObject mProperties;
        private API.AnimationRequest mHeldTpHitAnimation;

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

            string heldTpHitAnimation = mProperties["heldTpHitAnimation"]["animationCode"].AsString();

            mAnimationManager = (api.ModLoader.GetModSystem<AnimationManagerLibSystem>() as API.IAnimationManagerProvider).GetAnimationManager();
            mAnimationManager.Register(new("heldTpHitAnimation"), heldTpHitAnimation);

            mHeldTpHitAnimation = Utils.AnimationRequestFromJson(mProperties["heldTpHitAnimation"]);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandHandling handling)
        {
            if (slot.Itemstack.TempAttributes.HasAttribute("mHeldTpHitAnimation_played")) StopAnimation(slot);
        }
        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (!slot.Itemstack.TempAttributes.HasAttribute("mHeldTpHitAnimation_played"))  StartAnimation(slot, byEntity);

            return true;
        }
        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandHandling handling)
        {
            if (slot.Itemstack.TempAttributes.HasAttribute("mHeldTpHitAnimation_played"))  StopAnimation(slot);

            return false;
        }
        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (slot.Itemstack.TempAttributes.HasAttribute("mHeldTpHitAnimation_played")) StopAnimation(slot);
        }

        private void StopAnimation(ItemSlot slot)
        {
            Guid runId = new(slot.Itemstack.TempAttributes.GetBytes("mHeldTpHitAnimation_guid"));
            mAnimationManager.Stop(runId);
            slot.Itemstack.TempAttributes.RemoveAttribute("mHeldTpHitAnimation_guid");
        }
        private void StartAnimation(ItemSlot slot, EntityAgent byEntity)
        {
            slot.Itemstack.TempAttributes.SetInt("mHeldTpHitAnimation_played", 1);
            Guid runId = mAnimationManager.Run(byEntity.EntityId, mHeldTpHitAnimation);
            slot.Itemstack.TempAttributes.SetBytes("mHeldTpHitAnimation_guid", runId.ToByteArray());
        }
    }
}
