using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using System.Collections.Generic;
using System.Linq;

namespace AnimationManagerLib.CollectibleBehaviors
{
    public class AnimatableAttachable : Animatable // Based on code from TeacupAngel (https://github.com/TeacupAngel)
    {
        public AnimatableAttachable(CollectibleObject collObj) : base(collObj)
        {
        }

        private readonly Dictionary<string, Attachment> mAttachments = new();
        private readonly Dictionary<string, bool> mActiveAttachments = new();

        public void AddAttachment(string attachmentCode, ItemStack attachmentItem, ModelTransform transform)
        {
            if (mClientApi == null) return;
            mAttachments.Add(attachmentCode, new(mClientApi, attachmentCode, attachmentItem, transform));
            mActiveAttachments.Add(attachmentCode, true);
        }

        public void ToggleAttachment(string attachmentCode, bool toggle)
        {
            mActiveAttachments[attachmentCode] = toggle;
        }

        public void RemoveAttachment(string attachmentCode)
        {
            mAttachments.Remove(attachmentCode);
            mActiveAttachments.Remove(attachmentCode);
        }

        public void ClearAttachments()
        {
            mAttachments.Clear();
            mActiveAttachments.Clear();
        }

        public void RenderHandFp(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, double posX, double posY, double posZ, float size, int color, bool rotate = false, bool showStackSize = true)
        {
            //base.RenderHandFp(inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize);

            if (onlyWhenAnimating && ActiveAnimationsByAnimCode.Count == 0) return;
            if (Animator == null) return;

            foreach ((string code, bool active) in mActiveAttachments.Where(x => x.Value))
            {
                mAttachments[code].Render(renderInfo, Animator, modelMat);
            }
        }
    }

    public class Attachment
    {
        private readonly ICoreClientAPI mApi;
        private readonly ModelTransform mAttachedTransform;
        private readonly ItemStack mItemStack;
        private readonly string mAttachmentPointCode;

        private Matrixf mAttachedMeshMatrix = new();

        public Attachment(ICoreClientAPI api, string attachmentPointCode, ItemStack attachment, ModelTransform transform)
        {
            mApi = api;
            mItemStack = attachment.Clone();
            mAttachedTransform = transform;
            mAttachmentPointCode = attachmentPointCode;
        }

        public void Render(ItemRenderInfo renderInfo, AnimatorBase animator, Matrixf modelMat)
        {
            if (mApi == null) return;

            ItemRenderInfo attachedRenderInfo = GetAttachmentRenderInfo(renderInfo.dt);
            if (attachedRenderInfo == null) return;


            IShaderProgram prog = mApi.Render.CurrentActiveShader;

            AttachmentPointAndPose attachmentPointAndPose = animator.GetAttachmentPointPose(mAttachmentPointCode);
            AttachmentPoint attachmentPoint = attachmentPointAndPose.AttachPoint;
            CalculateMeshMatrix(modelMat, renderInfo, attachedRenderInfo, attachmentPointAndPose, attachmentPoint);
            prog.UniformMatrix("modelViewMatrix", mAttachedMeshMatrix.Values);

            //mApi.Render.RenderMesh(attachedRenderInfo.ModelRef);
        }

        private void CalculateMeshMatrix(Matrixf modelMat, ItemRenderInfo renderInfo, ItemRenderInfo attachedRenderInfo, AttachmentPointAndPose apap, AttachmentPoint ap)
        {
            mAttachedMeshMatrix = modelMat.Clone()
                .Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
                .Mul(apap.AnimModelMatrix)
                .Translate((ap.PosX + attachedRenderInfo.Transform.Translation.X) / 16f, (ap.PosY + attachedRenderInfo.Transform.Translation.Y) / 16f, (ap.PosZ + attachedRenderInfo.Transform.Translation.Z) / 16f)
                .Translate(renderInfo.Transform.Origin.X, renderInfo.Transform.Origin.Y, renderInfo.Transform.Origin.Z)
                .RotateX((float)(ap.RotationX) * GameMath.DEG2RAD)
                .RotateY((float)(ap.RotationY) * GameMath.DEG2RAD)
                .RotateZ((float)(ap.RotationZ) * GameMath.DEG2RAD)
                .Scale(attachedRenderInfo.Transform.ScaleXYZ.X, attachedRenderInfo.Transform.ScaleXYZ.Y, attachedRenderInfo.Transform.ScaleXYZ.Z)
                .Translate(-attachedRenderInfo.Transform.Origin.X / 16f, -attachedRenderInfo.Transform.Origin.Y / 16f, -attachedRenderInfo.Transform.Origin.Z / 16f)
                .Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
            ;
        }

        private ItemRenderInfo GetAttachmentRenderInfo(float dt)
        {
            DummySlot dummySlot = new(mItemStack);
            ItemRenderInfo renderInfo = mApi.Render.GetItemStackRenderInfo(dummySlot, EnumItemRenderTarget.Ground, dt);
            renderInfo.Transform = mAttachedTransform;
            return renderInfo;
        }
    }
}
