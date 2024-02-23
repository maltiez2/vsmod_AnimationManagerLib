using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace AnimationManagerLib.Patches;
/*public static class CameraAnglesController
{
    private static void UpdateCameraYawPitch(ClientMain __instance, float dt)
    {
        if (player.worlddata.CurrentGameMode == EnumGameMode.Survival && api.renderapi.ShaderUniforms.GlitchStrength > 0.75f && Platform.GetWindowState() == WindowState.Normal && rand.Value.NextDouble() < 0.01)
        {
            Size2i screenSize = Platform.ScreenSize;
            Size2i windowSize = Platform.WindowSize;
            if (windowSize.Width < screenSize.Width && windowSize.Height < screenSize.Height)
            {
                int num = screenSize.Width - windowSize.Width;
                int num2 = screenSize.Height - windowSize.Height;
                Vector2i clientSize = ((ClientPlatformWindows)Platform).window.ClientSize;
                int x = clientSize.X;
                int y = clientSize.Y;
                if (x > 0 && x < num)
                {
                    clientSize.X = GameMath.Clamp(x + rand.Value.Next(10) - 5, 0, num);
                }

                if (y > 0 && y < num2)
                {
                    clientSize.Y = GameMath.Clamp(y + rand.Value.Next(10) - 5, 0, num2);
                }

                ((ClientPlatformWindows)Platform).window.ClientSize = clientSize;
            }
        }

        double num3 = GameMath.Clamp(dt / 0.0133333337f, 0f, 3f);
        double num4 = GameMath.Clamp((double)(ClientSettings.MouseSmoothing / 100f) * num3, 0.0099999997764825821, 1.0);
        float num5 = 0.5f * (float)num3;
        double num6 = num4 * (MouseDeltaX - DelayedMouseDeltaX);
        double num7 = num4 * (MouseDeltaY - DelayedMouseDeltaY);
        DelayedMouseDeltaX += num6;
        DelayedMouseDeltaY += num7;
        if (!AllowCameraControl || !Platform.IsFocused || !MouseGrabbed)
        {
            return;
        }

        EnumMountAngleMode enumMountAngleMode = EnumMountAngleMode.Unaffected;
        float? num8 = null;
        IMountable mountedOn = EntityPlayer.MountedOn;
        if (EntityPlayer.MountedOn != null)
        {
            enumMountAngleMode = mountedOn.AngleMode;
            num8 = mountedOn.MountPosition.Yaw;
        }

        if (player.CameraMode == EnumCameraMode.Overhead)
        {
            EntityPlayer.Pos.Yaw = EntityPlayer.WalkYaw;
            mouseYaw -= (float)(num6 * (double)rotationspeed * 1.0 / 75.0);
            mouseYaw = GameMath.Mod(mouseYaw, MathF.PI * 2f);
        }
        else
        {
            mouseYaw -= (float)(num6 * (double)rotationspeed * 1.0 / 75.0);
            EntityPlayer.Pos.Yaw = mouseYaw;
        }

        if (enumMountAngleMode == EnumMountAngleMode.PushYaw || enumMountAngleMode == EnumMountAngleMode.Push)
        {
            float num9 = (0f - num5) * GameMath.AngleRadDistance(mountedOn.MountPosition.Yaw, prevMountAngles.Y);
            prevMountAngles.Y += num9;
            if (enumMountAngleMode == EnumMountAngleMode.Push)
            {
                EntityPlayer.Pos.Roll -= GameMath.AngleRadDistance(mountedOn.MountPosition.Roll, prevMountAngles.X);
                EntityPlayer.Pos.Pitch -= GameMath.AngleRadDistance(mountedOn.MountPosition.Pitch, prevMountAngles.Z);
            }

            if (player.CameraMode == EnumCameraMode.Overhead)
            {
                EntityPlayer.WalkYaw += num9;
            }
            else
            {
                mouseYaw += num9;
                EntityPlayer.Pos.Yaw += num9;
                EntityPlayer.BodyYaw += num9;
            }
        }

        if (enumMountAngleMode == EnumMountAngleMode.Fixate || enumMountAngleMode == EnumMountAngleMode.FixateYaw)
        {
            EntityPlayer.Pos.Yaw = num8.Value;
        }

        if (enumMountAngleMode == EnumMountAngleMode.Fixate)
        {
            EntityPlayer.Pos.Pitch = EntityPlayer.MountedOn.MountPosition.Pitch;
        }
        else
        {
            EntityPlayer.Pos.Pitch += (float)(num7 * (double)rotationspeed * 1.0 / 75.0 * (double)((!ClientSettings.InvertMouseYAxis) ? 1 : (-1)));
        }

        if (mountedOn != null)
        {
            prevMountAngles.Set(mountedOn.MountPosition.Roll, prevMountAngles.Y, mountedOn.MountPosition.Pitch);
        }

        EntityPlayer.Pos.Pitch = GameMath.Clamp(EntityPlayer.Pos.Pitch, 1.58579636f, 4.697389f);
        EntityPlayer.Pos.Yaw = GameMath.Mod(EntityPlayer.Pos.Yaw, MathF.PI * 2f);
        mousePitch = EntityPlayer.Pos.Pitch;
    }
}*/
