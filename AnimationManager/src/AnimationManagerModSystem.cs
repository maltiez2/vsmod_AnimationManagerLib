using AnimationManagerLib.API;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace AnimationManagerLib
{
    public class AnimationManagerLibSystem : ModSystem, API.IAnimationManagerSystem
    {
        public const string HarmonyID = "animationmanagerlib";
        public const string ChannelName = "animationmanagerlib";

        internal delegate void OnBeforeRenderCallback(Vintagestory.API.Common.IAnimator animator, float dt);
        internal IShaderProgram? AnimatedItemShaderProgram => mShaderProgram;
        internal event OnBeforeRenderCallback? OnHeldItemBeforeRender;

        private ICoreAPI? mApi;
        private AnimationManager? mManager;
        private ShaderProgram? mShaderProgram;

        public bool Register(API.AnimationId id, API.AnimationData animation) => mManager?.Register(id, animation) ?? false;
        public Guid Run(API.AnimationTarget animationTarget, API.AnimationSequence sequence, bool synchronize = true) => mManager?.Run(animationTarget, synchronize, sequence.Requests) ?? Guid.Empty;
        public void Stop(Guid runId) => mManager?.Stop(runId);

        public override void Start(ICoreAPI api)
        {
            mApi = api;
            api.RegisterCollectibleBehaviorClass("Animatable", typeof(CollectibleBehaviors.Animatable));
            api.RegisterCollectibleBehaviorClass("AnimatableAttachable", typeof(CollectibleBehaviors.AnimatableAttachable));
            api.RegisterCollectibleBehaviorClass("AnimatableProcedural", typeof(CollectibleBehaviors.AnimatableProcedural));
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            Patches.AnimatorBasePatch.Patch(HarmonyID);

            api.Event.ReloadShader += LoadAnimatedItemShaders;
            LoadAnimatedItemShaders();

            Synchronizer synchronizer = new();
            mManager = new AnimationManager(api, synchronizer);
            RegisterHandlers(mManager);
            synchronizer.Init(
                api,
                (packet) => mManager.Run(packet.AnimationTarget, packet.RunId, packet.Requests),
                (packet) => mManager.Stop(packet.RunId),
                ChannelName
            );
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            Synchronizer synchronizer = new();
            synchronizer.Init(
                api,
                null,
                null,
                ChannelName
            );
        }
        public bool LoadAnimatedItemShaders()
        {
            if (mApi is not ICoreClientAPI clientApi) return false;
            
            mShaderProgram = clientApi.Shader.NewShaderProgram() as ShaderProgram;
            
            if (mShaderProgram == null) return false;
            
            mShaderProgram.AssetDomain = Mod.Info.ModID;
            clientApi.Shader.RegisterFileShaderProgram("helditemanimated", AnimatedItemShaderProgram);
            mShaderProgram.Compile();

            return true;
        }
        public void OnBeforeRender(Vintagestory.API.Common.IAnimator animator, float dt)
        {
            OnHeldItemBeforeRender?.Invoke(animator, dt);
        }

        private void RegisterHandlers(AnimationManager manager)
        {
            Patches.AnimatorBasePatch.OnElementPoseUsedCallback += manager.OnApplyAnimation;
            Patches.AnimatorBasePatch.OnFrameCallback += manager.OnFrameHandler;
            OnHeldItemBeforeRender += manager.OnFrameHandler;
        }
        private void UnregisterHandlers(AnimationManager? manager)
        {
            if (manager == null) return;
            Patches.AnimatorBasePatch.OnElementPoseUsedCallback -= manager.OnApplyAnimation;
            Patches.AnimatorBasePatch.OnFrameCallback -= manager.OnFrameHandler;
            OnHeldItemBeforeRender -= manager.OnFrameHandler;
        }
        public override void Dispose()
        {
            if (mApi is ICoreClientAPI clientApi)
            {
                clientApi.Event.ReloadShader -= LoadAnimatedItemShaders;
                UnregisterHandlers(mManager);
                Patches.AnimatorBasePatch.Unpatch(HarmonyID);
            }
            base.Dispose();
        }

        
    }
}
