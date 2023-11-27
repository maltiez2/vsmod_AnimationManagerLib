using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib.API
{
    static public class Utils
    {
        public static uint ToCrc32(string value) => GameMath.Crc32(value.ToLowerInvariant()) & int.MaxValue;

        public static AnimationRequest AnimationRequestFromJson(JsonObject definition)
        {
            float? startFrame = definition.KeyExists("startFrame") ? definition["startFrame"].AsFloat() : null;
            float? endFrame = definition.KeyExists("endFrame") ? definition["endFrame"].AsFloat() : null;
            if (definition.KeyExists("frame"))
            {
                float frame = definition["frame"].AsFloat();
                startFrame = frame;
                endFrame = frame;
            }

            CategoryId category = definition.KeyExists("category") ? CategoryIdFromJson(definition["category"]) : new CategoryId();

            return new()
            {
                Action = (AnimationPlayerAction)Enum.Parse(typeof(AnimationPlayerAction), definition["action"].AsString("Set")),
                Category = category,
                Animation = new AnimationId(category, definition["animation"].AsString()),
                Duration = TimeSpan.FromMilliseconds(definition["duration_ms"].AsFloat()),
                Modifier = (ProgressModifierType)Enum.Parse(typeof(ProgressModifierType), definition["dynamic"].AsString("Linear")),
                StartFrame = startFrame,
                EndFrame = endFrame
            };
        }

        public static AnimationRequest AnimationRequestFromJson(JsonObject definition, bool noCategory)
        {
            float? startFrame = definition.KeyExists("startFrame") ? definition["startFrame"].AsFloat() : null;
            float? endFrame = definition.KeyExists("endFrame") ? definition["endFrame"].AsFloat() : null;
            if (definition.KeyExists("frame"))
            {
                float frame = definition["frame"].AsFloat();
                startFrame = frame;
                endFrame = frame;
            }

            CategoryId category = !noCategory && definition.KeyExists("category") ? CategoryIdFromJson(definition["category"]) : new CategoryId();

            return new()
            {
                Action = (AnimationPlayerAction)Enum.Parse(typeof(AnimationPlayerAction), definition["action"].AsString("Set")),
                Category = category,
                Animation = new AnimationId(category, definition["animation"].AsString()),
                Duration = TimeSpan.FromMilliseconds(definition["duration_ms"].AsFloat()),
                Modifier = (ProgressModifierType)Enum.Parse(typeof(ProgressModifierType), definition["dynamic"].AsString("Linear")),
                StartFrame = startFrame,
                EndFrame = endFrame
            };
        }

        public static CategoryId CategoryIdFromJson(JsonObject definition)
        {
            string code = definition["code"].AsString();
            EnumAnimationBlendMode blending = (EnumAnimationBlendMode)Enum.Parse(typeof(EnumAnimationBlendMode), definition["blending"].AsString("Add"));
            float? weight = definition.KeyExists("weight") ? definition["weight"].AsFloat() : null;

            return new CategoryId() { Blending = blending, Hash = ToCrc32(code), Weight = weight };
        }
    }
}