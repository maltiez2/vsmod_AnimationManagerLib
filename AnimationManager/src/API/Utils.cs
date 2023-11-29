using System;
using System.Collections;
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
            CategoryId category = definition.KeyExists("category") ? CategoryIdFromJson(definition["category"]) : new CategoryId();
            return AnimationRequestFromJson(definition, category);
        }

        public static AnimationRequest AnimationRequestFromJson(JsonObject definition, bool noCategory)
        {
            CategoryId category = !noCategory && definition.KeyExists("category") ? CategoryIdFromJson(definition["category"]) : new CategoryId();
            return AnimationRequestFromJson(definition, category);
        }

        public static AnimationRequest AnimationRequestFromJson(JsonObject definition, CategoryId category)
        {
            float? startFrame = definition.KeyExists("startFrame") ? definition["startFrame"].AsFloat() : null;
            float? endFrame = definition.KeyExists("endFrame") ? definition["endFrame"].AsFloat() : null;
            if (definition.KeyExists("frame"))
            {
                float frame = definition["frame"].AsFloat();
                startFrame = frame;
                endFrame = frame;
            }

            RunParameters runParameters = new RunParameters()
            {
                Action = (AnimationPlayerAction)Enum.Parse(typeof(AnimationPlayerAction), definition["action"].AsString("Set")),
                Duration = TimeSpan.FromMilliseconds(definition["duration_ms"].AsFloat()),
                Modifier = (ProgressModifierType)Enum.Parse(typeof(ProgressModifierType), definition["dynamic"].AsString("Linear")),
                StartFrame = startFrame,
                TargetFrame = endFrame
            };

            return new()
            {
                Animation = new AnimationId(category, definition["animation"].AsString()),
                Parameters = runParameters
            };
        }

        public static CategoryId CategoryIdFromJson(JsonObject definition)
        {
            string code = definition["code"].AsString();
            EnumAnimationBlendMode blending = (EnumAnimationBlendMode)Enum.Parse(typeof(EnumAnimationBlendMode), definition["blending"].AsString("Add"));
            float? weight = definition.KeyExists("weight") ? definition["weight"].AsFloat() : null;

            return new CategoryId() { Blending = blending, Hash = ToCrc32(code), Weight = weight };
        }

        public static AnimationMetaData GenerateMetaData(Dictionary<string, EnumAnimationBlendMode> elementsBlendModes = null, Dictionary<string, float> elementsWeights = null)
        {
            return new AnimationMetaData()
            {
                ElementWeight = elementsWeights,
                ElementBlendMode = elementsBlendModes,
            };
        }
    }
}