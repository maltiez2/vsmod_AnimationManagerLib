using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AnimationManagerLib
{
    public class AnimationManagerLibSystem : ModSystem, API.IAnimationManagerProvider
    {
        public const string HarmonyID = "animationmanagerlib";

        private ICoreAPI mApi;
        private API.IAnimationManager mManager;

        public override void Start(ICoreAPI api)
        {
            mApi = api;
            api.RegisterCollectibleBehaviorClass("ItemAnimationBehavior", typeof(Extra.ItemAnimationBehavior));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Patches.AnimatorBasePatch.Patch(HarmonyID);

            mManager = new PlayerModelAnimationManager<PlayerModelComposer<PlayerModelAnimationFrame>>(api, null); // @TODO add synchronizer
        }

        public override void Dispose()
        {
            if (mApi.Side == EnumAppSide.Client) Patches.AnimatorBasePatch.Unpatch(HarmonyID);
            base.Dispose();
        }

        API.IAnimationManager API.IAnimationManagerProvider.GetAnimationManager()
        {
            return mManager;
        }
    }
}
