using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using AnimationManagerLib.API;

namespace AnimationManagerLib
{
    static public class Utils
    {
        public static uint ToCrc32(string value) => GameMath.Crc32(value.ToLowerInvariant()) & int.MaxValue;

        public static RunParameters? RunParametersFromJson(JsonObject definition, out string errorMessage)
        {
            AnimationPlayerAction action = (AnimationPlayerAction)Enum.Parse(typeof(AnimationPlayerAction), definition["action"].AsString("Set"));
            TimeSpan? duration = definition.KeyExists("duration_ms") ? TimeSpan.FromMilliseconds(definition["duration_ms"].AsFloat()) : null;
            float? startFrame = definition.KeyExists("startFrame") ? definition["startFrame"].AsFloat() : null;
            float? endFrame = definition.KeyExists("endFrame") ? definition["endFrame"].AsFloat() : null;
            float? frame = definition.KeyExists("frame") ? definition["frame"].AsFloat() : null;
            ProgressModifierType modifier = (ProgressModifierType)Enum.Parse(typeof(ProgressModifierType), definition["dynamic"].AsString("Linear"));
            errorMessage = "";
            
            switch (action)
            {
                case AnimationPlayerAction.Set:
                    if (frame == null)
                    {
                        errorMessage = $"No 'frame' specified for '{action}' action";
                        return null;
                    }
                    return RunParameters.Set(frame.Value);
                case AnimationPlayerAction.EaseIn:
                    if (frame == null)
                    {
                        errorMessage = $"No 'frame' specified for '{action}' action";
                        return null;
                    }
                    if (duration == null)
                    {
                        errorMessage = $"No 'duration_ms' specified for '{action}' action";
                        return null;
                    }
                    return RunParameters.EaseIn(duration.Value, frame.Value, modifier);
                case AnimationPlayerAction.EaseOut:
                    if (duration == null)
                    {
                        errorMessage = $"No 'duration_ms' specified for '{action}' action";
                        return null;
                    }
                    return RunParameters.EaseOut(duration.Value, modifier);
                case AnimationPlayerAction.Play:
                    if (duration == null)
                    {
                        errorMessage = $"No 'duration_ms' specified for '{action}' action";
                        return null;
                    }
                    if (startFrame == null)
                    {
                        errorMessage = $"No 'startFrame' specified for '{action}' action";
                        return null;
                    }
                    if (endFrame == null)
                    {
                        errorMessage = $"No 'endFrame' specified for '{action}' action";
                        return null;
                    }
                    return RunParameters.Play(duration.Value, startFrame.Value, endFrame.Value, modifier);
                case AnimationPlayerAction.Stop:
                    return RunParameters.Stop();
                case AnimationPlayerAction.Rewind:
                    if (duration == null)
                    {
                        errorMessage = $"No 'duration_ms' specified for '{action}' action";
                        return null;
                    }
                    if (startFrame == null)
                    {
                        errorMessage = $"No 'startFrame' specified for '{startFrame}' action";
                        return null;
                    }
                    if (endFrame == null)
                    {
                        errorMessage = $"No 'endFrame' specified for '{endFrame}' action";
                        return null;
                    }
                    return RunParameters.Rewind(duration.Value, startFrame.Value, endFrame.Value, modifier);
                case AnimationPlayerAction.Clear:
                    return RunParameters.Clear();
                default: return null;
            }
        }

        public static Category CategoryFromJson(JsonObject definition)
        {
            string code = definition["code"].AsString();
            EnumAnimationBlendMode blending = (EnumAnimationBlendMode)Enum.Parse(typeof(EnumAnimationBlendMode), definition["blending"].AsString("Add"));
            float? weight = definition.KeyExists("weight") ? definition["weight"].AsFloat() : null;

            return new Category(code, blending, weight);
        }
    }
}