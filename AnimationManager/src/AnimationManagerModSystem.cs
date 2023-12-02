using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using VSImGui;

namespace AnimationManagerLib
{
    public class AnimationManagerLibSystem : ModSystem, API.IAnimationManagerProvider
    {
        public const string HarmonyID = "animationmanagerlib";
        public const string ChannelName = "animationmanagerlib";

        public delegate void OnBeforeRenderCallback(IAnimator animator, float dt);

        public IShaderProgram AnimatedItemShaderProgram { get; private set; }
        public event OnBeforeRenderCallback OnHeldItemBeforeRender;

        private ICoreAPI mApi;
        private API.IAnimationManager mManager;
        private API.ISynchronizer mSynchronizer;

        public API.IAnimationManager GetAnimationManager() => mManager;
        public API.ISynchronizer GetSynchronizer() => mSynchronizer;

        public override void Start(ICoreAPI api)
        {
            mApi = api;
            api.RegisterCollectibleBehaviorClass("ItemAnimationBehavior", typeof(CollectibleBehaviors.PlayerAnimation));
            api.RegisterCollectibleBehaviorClass("Animatable", typeof(CollectibleBehaviors.Animatable));
            api.RegisterCollectibleBehaviorClass("AnimatableAttachable", typeof(CollectibleBehaviors.AnimatableAttachable));
            api.RegisterCollectibleBehaviorClass("AnimatableProcedural", typeof(CollectibleBehaviors.AnimatableProcedural));
            mSynchronizer = new Synchronizer();
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            Patches.AnimatorBasePatch.Patch(HarmonyID);

            api.Event.ReloadShader += LoadAnimatedItemShaders;
            LoadAnimatedItemShaders();

            mManager = new PlayerModelAnimationManager<Composer>(api, mSynchronizer);
            RegisterHandlers(mManager as PlayerModelAnimationManager<Composer>);
            mSynchronizer.Init(
                api,
                (packet) => mManager.Run(packet.AnimationTarget, packet.RunId, packet.Requests),
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
        public bool LoadAnimatedItemShaders()
        {
            AnimatedItemShaderProgram = (mApi as ICoreClientAPI).Shader.NewShaderProgram();
            (AnimatedItemShaderProgram as ShaderProgram).AssetDomain = Mod.Info.ModID;
            (mApi as ICoreClientAPI).Shader.RegisterFileShaderProgram("helditemanimated", AnimatedItemShaderProgram);
            AnimatedItemShaderProgram.Compile();

            return true;
        }
        public void OnBeforeRender(IAnimator animator, float dt)
        {
            OnHeldItemBeforeRender?.Invoke(animator, dt);
        }

        private void RegisterHandlers(PlayerModelAnimationManager<Composer> manager)
        {
            Patches.AnimatorBasePatch.OnElementPoseUsedCallback += manager.OnApplyAnimation;
            Patches.AnimatorBasePatch.OnFrameCallback += manager.OnFrameHandler;
            OnHeldItemBeforeRender += manager.OnFrameHandler;
        }
        private void UnregisterHandlers(PlayerModelAnimationManager<Composer> manager)
        {
            Patches.AnimatorBasePatch.OnElementPoseUsedCallback -= manager.OnApplyAnimation;
            Patches.AnimatorBasePatch.OnFrameCallback -= manager.OnFrameHandler;
            OnHeldItemBeforeRender -= manager.OnFrameHandler;
        }
        public override void Dispose()
        {
            if (mApi.Side == EnumAppSide.Client)
            {
                UnregisterHandlers(mManager as PlayerModelAnimationManager<Composer>);
                Patches.AnimatorBasePatch.Unpatch(HarmonyID);
            }
            base.Dispose();
        }
    }
}
