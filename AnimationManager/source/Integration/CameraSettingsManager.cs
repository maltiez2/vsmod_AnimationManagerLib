using AnimationManagerLib.Patches;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using VSImGui;

namespace AnimationManagerLib;

public enum CameraSettingsType
{
    FirstPersonHandsPitch,
    FirstPersonHandsYawSpeed,
    IntoxicationEffectIntensity,
    WalkPitchMultiplier,
    WalkBobbingAmplitude,
    WalkBobbingOffset,
    WalkBobbingSprint
}

internal sealed class CameraSettingsManager : IDisposable
{
    private readonly Dictionary<CameraSettingsType, CameraSetting> mSettings = new();
    private readonly long mListener;
    private readonly ICoreClientAPI mApi;
    private bool mDisposed = false;

    public CameraSettingsManager(ICoreClientAPI api)
    {
        mListener = api.World.RegisterGameTickListener(Update, 0);
        mApi = api;
    }

    public void Set(string domain, CameraSettingsType setting, float value, float blendingSpeed)
    {
        if (!mSettings.ContainsKey(setting))
        {
            mSettings.Add(setting, new());
        }

        mSettings[setting].Set(domain, value, blendingSpeed);
    }
    private void Update(float dt)
    {
        foreach ((CameraSettingsType setting, CameraSetting value) in mSettings)
        {
            SetValue(setting, value.Get(dt));
        }
    }

    private static void SetValue(CameraSettingsType setting, float value)
    {
        switch (setting)
        {
            case CameraSettingsType.FirstPersonHandsPitch:
                PlayerModelMatrixController.PitchModifierFp = value;
                break;
            case CameraSettingsType.FirstPersonHandsYawSpeed:
                PlayerModelMatrixController.YawSpeedMultiplier = value;
                break;
            case CameraSettingsType.IntoxicationEffectIntensity:
                PlayerModelMatrixController.IntoxicationEffectIntensity = value;
                break;
            case CameraSettingsType.WalkPitchMultiplier:
                PlayerModelMatrixController.WalkPitchMultiplier = value;
                break;
            case CameraSettingsType.WalkBobbingAmplitude:
                EyeHightController.Amplitude = value;
                break;
            case CameraSettingsType.WalkBobbingOffset:
                EyeHightController.Offset = value;
                break;
            case CameraSettingsType.WalkBobbingSprint:
                EyeHightController.SprintAmplitudeEffect = value;
                break;
        }
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mApi.World.UnregisterGameTickListener(mListener);
    }
}

internal sealed class CameraSetting
{
    private readonly Dictionary<string, CameraSettingValue> mValues = new();

    public void Set(string domain, float value, float speed)
    {
        if (!mValues.ContainsKey(domain))
        {
            mValues[domain] = new(1.0f);
        }

        mValues[domain].Set(value, speed);
    }

    public float Get(float dt)
    {
        float result = 1.0f;

        foreach ((_, CameraSettingValue value) in mValues)
        {
            result *= value.Get(dt);
        }

        return result;
    }
}

internal sealed class CameraSettingValue
{
    private const float cEpsilon = 1e-3f;
    private const float cSpeedMultiplier = 10.0f;

    private float mValue;
    private float mTarget;
    private float mBlendSpeed = 0;
    private bool mUpdated = true;

    public CameraSettingValue(float value)
    {
        mValue = value;
        mTarget = value;
    }

    public void Set(float target, float speed)
    {
        mTarget = target;
        mBlendSpeed = speed;
        mUpdated = false;
    }

    public float Get(float dt)
    {
        Update(dt);
        return mValue;
    }

    private void Update(float dt)
    {
        if (mUpdated) return;
        float diff = mTarget - mValue;
        float change = Math.Clamp(diff * dt * mBlendSpeed * cSpeedMultiplier, -Math.Abs(diff), Math.Abs(diff));
        mValue += change;
        mUpdated = Math.Abs(mValue - mTarget) < cEpsilon;
    }
}
