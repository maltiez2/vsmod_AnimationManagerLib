using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AnimationManagerLib
{
    public class AnimationManagerLib : ModSystem
    {
        public const string HarmonyID = "animationmanagerlib";

        private ICoreAPI mApi;

        public override void Start(ICoreAPI api)
        {
            mApi = api;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Patches.AnimatorBasePatch.Patch(HarmonyID);
            api.Event.PlayerEntitySpawn += _ => AddPlayer();
        }

        private void AddPlayer()
        {
            EntityPlayer player = (mApi as ICoreClientAPI)?.World.Player.Entity;

            Patches.AnimatorBasePatch.OnFrameCallback += (animator, dt) =>
            {
                if (animator == player?.AnimManager.Animator)
                {
                    mApi.Logger.Notification("OnFrame for: {0}, dt: {1}", player.GetName(), dt);
                }
            };
        }

        public override void Dispose()
        {
            if (mApi.Side == EnumAppSide.Client) Patches.AnimatorBasePatch.Unpatch(HarmonyID);
            base.Dispose();
        }
    }
}
