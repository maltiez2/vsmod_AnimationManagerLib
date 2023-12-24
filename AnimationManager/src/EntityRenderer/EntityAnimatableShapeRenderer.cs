using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace AnimationManagerLib.EntityRenderers;

public class EntityAnimatableShapeRenderer : EntityShapeRenderer
{
    private float mTimeAccumulation = 0;
    
    public EntityAnimatableShapeRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
    {
        Console.WriteLine($"EntityAnimatableShapeRenderer - created for: {entity.GetName()}");
    }

    protected override void RenderHeldItem(float dt, bool isShadowPass, bool right)
    {
        Console.WriteLine($"EntityAnimatableShapeRenderer - RenderHeldItem - isShadowPass: {isShadowPass}, right: {right}");
        
        ItemSlot? itemSlot = ((!right) ? eagent?.LeftHandItemSlot : eagent?.RightHandItemSlot);
        ItemStack? itemStack = itemSlot?.Itemstack;
        if (itemStack == null)
        {
            return;
        }

        AttachmentPointAndPose? attachmentPointAndPose = entity.AnimManager?.Animator?.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
        if (attachmentPointAndPose == null)
        {
            return;
        }

        IRenderAPI render = capi.Render;
        AttachmentPoint attachPoint = attachmentPointAndPose.AttachPoint;
        ItemRenderInfo itemStackRenderInfo = render.GetItemStackRenderInfo(itemSlot, right ? EnumItemRenderTarget.HandTp : EnumItemRenderTarget.HandTpOff, dt);
        IShaderProgram shaderProgram = null;
        if (itemStackRenderInfo?.Transform == null)
        {
            return;
        }

        ItemModelMat.Set(ModelMat).Mul(attachmentPointAndPose.AnimModelMatrix).Translate(itemStackRenderInfo.Transform.Origin.X, itemStackRenderInfo.Transform.Origin.Y, itemStackRenderInfo.Transform.Origin.Z)
            .Scale(itemStackRenderInfo.Transform.ScaleXYZ.X, itemStackRenderInfo.Transform.ScaleXYZ.Y, itemStackRenderInfo.Transform.ScaleXYZ.Z)
            .Translate(attachPoint.PosX / 16.0 + (double)itemStackRenderInfo.Transform.Translation.X, attachPoint.PosY / 16.0 + (double)itemStackRenderInfo.Transform.Translation.Y, attachPoint.PosZ / 16.0 + (double)itemStackRenderInfo.Transform.Translation.Z)
            .RotateX((float)(attachPoint.RotationX + (double)itemStackRenderInfo.Transform.Rotation.X) * (MathF.PI / 180f))
            .RotateY((float)(attachPoint.RotationY + (double)itemStackRenderInfo.Transform.Rotation.Y) * (MathF.PI / 180f))
            .RotateZ((float)(attachPoint.RotationZ + (double)itemStackRenderInfo.Transform.Rotation.Z) * (MathF.PI / 180f))
            .Translate(0f - itemStackRenderInfo.Transform.Origin.X, 0f - itemStackRenderInfo.Transform.Origin.Y, 0f - itemStackRenderInfo.Transform.Origin.Z);
        string textureSampleName = "tex";
        if (isShadowPass)
        {
            textureSampleName = "tex2d";
            render.CurrentActiveShader.BindTexture2D("tex2d", itemStackRenderInfo.TextureId, 0);
            float[] array = Mat4f.Mul(ItemModelMat.Values, capi.Render.CurrentModelviewMatrix, ItemModelMat.Values);
            Mat4f.Mul(array, capi.Render.CurrentProjectionMatrix, array);
            capi.Render.CurrentActiveShader.UniformMatrix("mvpMatrix", array);
            capi.Render.CurrentActiveShader.Uniform("origin", new Vec3f());
        }
        else
        {
            shaderProgram = getReadyShader();
            shaderProgram.Uniform("dontWarpVertices", 0);
            shaderProgram.Uniform("addRenderFlags", 0);
            shaderProgram.Uniform("normalShaded", 1);
            shaderProgram.Uniform("rgbaTint", ColorUtil.WhiteArgbVec);
            shaderProgram.Uniform("alphaTest", itemStackRenderInfo.AlphaTest);
            shaderProgram.Uniform("damageEffect", itemStackRenderInfo.DamageEffect);
            shaderProgram.Uniform("overlayOpacity", itemStackRenderInfo.OverlayOpacity);
            if (itemStackRenderInfo.OverlayTexture != null && itemStackRenderInfo.OverlayOpacity > 0f)
            {
                shaderProgram.Uniform("tex2dOverlay", itemStackRenderInfo.OverlayTexture.TextureId);
                shaderProgram.Uniform("overlayTextureSize", new Vec2f(itemStackRenderInfo.OverlayTexture.Width, itemStackRenderInfo.OverlayTexture.Height));
                shaderProgram.Uniform("baseTextureSize", new Vec2f(itemStackRenderInfo.TextureSize.Width, itemStackRenderInfo.TextureSize.Height));
                TextureAtlasPosition textureAtlasPosition = render.GetTextureAtlasPosition(itemStack);
                shaderProgram.Uniform("baseUvOrigin", new Vec2f(textureAtlasPosition.x1, textureAtlasPosition.y1));
            }

            int num = (int)itemStack.Collectible.GetTemperature(capi.World, itemStack);
            float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num);
            int num2 = GameMath.Clamp((num - 500) / 3, 0, 255);
            shaderProgram.Uniform("extraGlow", num2);
            shaderProgram.Uniform("rgbaAmbientIn", render.AmbientColor);
            shaderProgram.Uniform("rgbaLightIn", lightrgbs);
            shaderProgram.Uniform("rgbaGlowIn", new Vec4f(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], (float)num2 / 255f));
            shaderProgram.Uniform("rgbaFogIn", render.FogColor);
            shaderProgram.Uniform("fogMinIn", render.FogMin);
            shaderProgram.Uniform("fogDensityIn", render.FogDensity);
            shaderProgram.Uniform("normalShaded", itemStackRenderInfo.NormalShaded ? 1 : 0);
            shaderProgram.UniformMatrix("projectionMatrix", render.CurrentProjectionMatrix);
            shaderProgram.UniformMatrix("viewMatrix", render.CameraMatrixOriginf);
            shaderProgram.UniformMatrix("modelMatrix", ItemModelMat.Values);
        }

        if (!itemStackRenderInfo.CullFaces)
        {
            render.GlDisableCullFace();
        }

        render.RenderMultiTextureMesh(itemStackRenderInfo.ModelRef, textureSampleName);
        if (!itemStackRenderInfo.CullFaces)
        {
            render.GlEnableCullFace();
        }

        if (isShadowPass)
        {
            return;
        }

        shaderProgram?.Uniform("damageEffect", 0f);
        shaderProgram?.Stop();
        float num3 = Math.Max(0f, 1f - (float)capi.World.BlockAccessor.GetDistanceToRainFall(entity.Pos.AsBlockPos) / 5f);
        AdvancedParticleProperties[] array2 = itemStack?.Collectible?.ParticleProperties;
        if (itemStack?.Collectible == null || capi.IsGamePaused)
        {
            return;
        }

        Vec4f vec4f = ItemModelMat.TransformVector(new Vec4f(itemStack.Collectible.TopMiddlePos.X, itemStack.Collectible.TopMiddlePos.Y, itemStack.Collectible.TopMiddlePos.Z, 1f));
        EntityPlayer entityPlayer = capi.World.Player.Entity;
        mTimeAccumulation += dt;
        if (array2 != null && array2.Length != 0 && mTimeAccumulation > 0.05f)
        {
            mTimeAccumulation %= 0.025f;
            foreach (AdvancedParticleProperties advancedParticleProperties in array2)
            {
                advancedParticleProperties.WindAffectednesAtPos = num3;
                advancedParticleProperties.WindAffectednes = num3;
                advancedParticleProperties.basePos.X = (double)vec4f.X + entity.Pos.X + (0.0 - (entity.Pos.X - entityPlayer.CameraPos.X));
                advancedParticleProperties.basePos.Y = (double)vec4f.Y + entity.Pos.Y + (0.0 - (entity.Pos.Y - entityPlayer.CameraPos.Y));
                advancedParticleProperties.basePos.Z = (double)vec4f.Z + entity.Pos.Z + (0.0 - (entity.Pos.Z - entityPlayer.CameraPos.Z));
                eagent?.World.SpawnParticles(advancedParticleProperties);
            }
        }
    }
}
