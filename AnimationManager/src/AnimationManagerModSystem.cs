using AnimationManagerLib.Patches;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AnimationManagerLib
{
    public class AnimationManagerModSystem : ModSystem
    {
        public const string HarmonyID = "animationmanagerlib";

        private ICoreAPI mApi;

        public override void Start(ICoreAPI api)
        {
            mApi = api;
            ClientAnimatorPatch.Patch(HarmonyID);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Event.PlayerEntitySpawn += _ => AddPlayer();
        }

        private void AddPlayer()
        {
            EntityPlayer player = (mApi as ICoreClientAPI)?.World.Player.Entity;

            ClientAnimatorPatch.OnFrameCallback += (animator, dt) =>
            {
                if (animator == player?.AnimManager.Animator)
                {
                    mApi.Logger.Notification("OnFrame for: {0}, dt: {1}", player.GetName(), dt);
                }
            };
        }

        public override void Dispose()
        {
            ClientAnimatorPatch.Unpatch(HarmonyID);
            base.Dispose();
        }
    }
}
