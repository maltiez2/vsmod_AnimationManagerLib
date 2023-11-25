using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AnimationManagerLib
{
    public class AnimationManagerLibSystem : ModSystem, API.IAnimationManagerProvider
    {
        public const string HarmonyID = "animationmanagerlib";
        public const string ChannelName = "animationmanagerlib";

        private ICoreAPI mApi;
        private API.IAnimationManager mManager;
        private API.ISynchronizer mSynchronizer;

        public override void Start(ICoreAPI api)
        {
            mApi = api;
            api.RegisterCollectibleBehaviorClass("ItemAnimationBehavior", typeof(Extra.ItemAnimationBehavior));
            mSynchronizer = new AnimationSynchronizer();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Patches.AnimatorBasePatch.Patch(HarmonyID);

            mManager = new PlayerModelAnimationManager<PlayerModelComposer<PlayerModelAnimationFrame>>(api, mSynchronizer);
            mSynchronizer.Init(
                api,
                (packet) => mManager.Run(packet.EntityId, packet.RunId, packet.Requests),
                (packet) => mManager.Stop(packet.RunId),
                ChannelName
            );

        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            mSynchronizer.Init(
                api,
                null,
                null,
                ChannelName
            );
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
