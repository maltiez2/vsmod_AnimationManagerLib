using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#if DEBUG
using VSImGui;
#endif

namespace AnimationManagerLib.Patches;
internal static class PlayerModelMatrixController
{
    public static float PitchModifierFp { get; set; } = 1.0f;
    [Obsolete("Will be removed")]
    public static float YawSpeedMultiplier { get; set; } = 1.0f;
    [Obsolete("Will be removed")]
    public static float IntoxicationEffectIntensity { get; set; } = 1.0f;
    [Obsolete("Will be removed")]
    public static float WalkPitchMultiplier { get; set; } = 1.0f;

    public static void LoadModelMatrixForPlayer(EntityPlayerShapeRenderer __instance, Entity entity, bool isSelf, float dt, bool isShadowPass)
    {
        if (MathF.Abs(PitchModifierFp - 1.0f) > 0.01f) __instance.HeldItemPitchFollowOverride = 0.8f * PitchModifierFp;
    }

    public static bool LoadModelMatrixForPlayer_old(EntityPlayerShapeRenderer __instance, Entity entity, bool isSelf, float dt, bool isShadowPass)
    {
        Field<float, EntityShapeRenderer> bodyYawLerped = new(typeof(EntityShapeRenderer), "bodyYawLerped", __instance);
        Field<EntityAgent, EntityShapeRenderer> eagent = new(typeof(EntityShapeRenderer), "eagent", __instance);
        Field<EntityPlayer, EntityPlayerShapeRenderer> entityPlayer = new(typeof(EntityPlayerShapeRenderer), "entityPlayer", __instance);
        Field<float, EntityShapeRenderer> intoxIntensity = new(typeof(EntityShapeRenderer), "intoxIntensity", __instance);

        ICoreClientAPI capi = (entity.Api as ICoreClientAPI);
        EntityPlayer selfEplr = capi.World.Player.Entity;
        Mat4f.Identity(__instance.ModelMat);

        if (!isSelf)
        {
            // We use special positioning code for mounted entities that are on the same mount as we are.
            // While this should not be necesssary, because the client side physics does set the entity position accordingly, it does same to create 1-frame jitter if we dont specially handle this
            IMountableSupplier? selfMountedOn = selfEplr.MountedOn?.MountSupplier;
            IMountableSupplier? heMountedOn = (entity as EntityAgent).MountedOn?.MountSupplier;
            if (selfMountedOn != null && selfMountedOn == heMountedOn)
            {
                Vec3f selfmountoffset = selfMountedOn.GetMountOffset(selfEplr);
                Vec3f hemountoffset = heMountedOn.GetMountOffset(entity);
                Mat4f.Translate(__instance.ModelMat, __instance.ModelMat, -selfmountoffset.X + hemountoffset.X, -selfmountoffset.Y + hemountoffset.Y, -selfmountoffset.Z + hemountoffset.Z);
            }
            else
            {
                Mat4f.Translate(__instance.ModelMat, __instance.ModelMat, (float)(entity.Pos.X - selfEplr.CameraPos.X), (float)(entity.Pos.Y - selfEplr.CameraPos.Y), (float)(entity.Pos.Z - selfEplr.CameraPos.Z));
            }
        }

        float rotX = entity.Properties.Client.Shape != null ? entity.Properties.Client.Shape.rotateX : 0;
        float rotY = entity.Properties.Client.Shape != null ? entity.Properties.Client.Shape.rotateY : 0;
        float rotZ = entity.Properties.Client.Shape != null ? entity.Properties.Client.Shape.rotateZ : 0;
        float bodyYaw;

        if (!isSelf || capi.World.Player.CameraMode != EnumCameraMode.FirstPerson)
        {
            float yawDist = GameMath.AngleRadDistance(bodyYawLerped.Value, eagent.Value.BodyYaw);
            bodyYawLerped.Value += GameMath.Clamp(yawDist, -dt * 8 * YawSpeedMultiplier, dt * 8 * YawSpeedMultiplier);
            bodyYaw = bodyYawLerped.Value;
        }
        else
        {
            bodyYaw = eagent.Value.BodyYaw;
        }

        float bodyPitch = entityPlayer.Value == null ? 0 : entityPlayer.Value.WalkPitch * WalkPitchMultiplier;
        Mat4f.RotateX(__instance.ModelMat, __instance.ModelMat, entity.Pos.Roll + rotX * GameMath.DEG2RAD);
        Mat4f.RotateY(__instance.ModelMat, __instance.ModelMat, bodyYaw + (180 + rotY) * GameMath.DEG2RAD);
        bool selfSwimming = isSelf && eagent.Value.Swimming && !capi.World.Player.ImmersiveFpMode && capi.World.Player.CameraMode == EnumCameraMode.FirstPerson;

        if (!selfSwimming && (selfEplr?.Controls.Gliding != true || capi.World.Player.ImmersiveFpMode || capi.World.Player.CameraMode != EnumCameraMode.FirstPerson))
        {
            Mat4f.RotateZ(__instance.ModelMat, __instance.ModelMat, bodyPitch + rotZ * GameMath.DEG2RAD);
        }

        Mat4f.RotateX(__instance.ModelMat, __instance.ModelMat, __instance.sidewaysSwivelAngle);

        // Rotate player hands with pitch
        if (isSelf && selfEplr != null && !capi.World.Player.ImmersiveFpMode && capi.World.Player.CameraMode == EnumCameraMode.FirstPerson && !isShadowPass)
        {
            float f = selfEplr?.Controls.IsFlying != true ? PitchModifierFp * 0.8f : 1f;
            Mat4f.Translate(__instance.ModelMat, __instance.ModelMat, 0f, (float)entity.LocalEyePos.Y, 0f);
            Mat4f.RotateZ(__instance.ModelMat, __instance.ModelMat, (float)(entity.Pos.Pitch - GameMath.PI) * f);
            Mat4f.Translate(__instance.ModelMat, __instance.ModelMat, 0, -(float)entity.LocalEyePos.Y, 0f);
        }

        if (isSelf && !capi.World.Player.ImmersiveFpMode && capi.World.Player.CameraMode == EnumCameraMode.FirstPerson && !isShadowPass)
        {
            Mat4f.Translate(__instance.ModelMat, __instance.ModelMat, 0, capi.Settings.Float["fpHandsYOffset"], 0);
        }

        if (selfEplr != null)
        {
            float targetIntensity = entity.WatchedAttributes.GetFloat("intoxication");
            intoxIntensity.Value += (targetIntensity - intoxIntensity.Value) * dt / 3;
            capi.Render.PerceptionEffects.ApplyToTpPlayer(selfEplr, __instance.ModelMat, intoxIntensity.Value * IntoxicationEffectIntensity);
        }

        float scale = entity.Properties.Client.Size;
        Mat4f.Scale(__instance.ModelMat, __instance.ModelMat, new float[] { scale, scale, scale });
        Mat4f.Translate(__instance.ModelMat, __instance.ModelMat, -0.5f, 0, -0.5f);

        return false;
    }
}
