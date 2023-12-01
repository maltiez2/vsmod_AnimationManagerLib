using System.Diagnostics;
using AnimationManagerLib.API;
using Vintagestory.API.MathTools;

namespace AnimationManagerLib
{
    public class Animation : IAnimation
    {
        private readonly AnimationFrame[] mKeyFrames;
        private readonly ushort[] mFrames;
        private readonly bool mCyclic;
        private readonly float mTotalFrames;

        public Animation(AnimationFrame[] keyFrames, ushort[] keyFramesPosition, float totalFrames, bool cyclic = false)
        {
            mKeyFrames = keyFrames;
            mFrames = keyFramesPosition;
            mCyclic = cyclic;
            mTotalFrames = totalFrames;
        }

        public AnimationFrame Blend(float progress, float? targetFrame, AnimationFrame endFrame)
        {
            AnimationFrame targetFrameValue = CalcFrame(progress, targetFrame ?? 0, targetFrame ?? 0);
            AnimationFrame endFrameClone = endFrame.Clone();
            targetFrameValue.LerpInto(endFrameClone, progress);
            return endFrameClone;
        }

        public AnimationFrame Blend(float progress, AnimationFrame startFrame, AnimationFrame endFrame)
        {
            AnimationFrame endFrameClone = endFrame.Clone();
            startFrame.LerpInto(endFrameClone, progress);
            return endFrameClone;
        }

        public AnimationFrame Play(float progress, float? startFrame = null, float? endFrame = null)
        {
            float startFrameIndex = startFrame == null ? 0 : (float)startFrame;
            float endFrameIndex = endFrame == null ? mFrames[mFrames.Length - 1] : (float)endFrame;

            return CalcFrame(progress, startFrameIndex, endFrameIndex);
        }

        private AnimationFrame CalcFrame(float progress, float startFrame, float endFrame)
        {
            (int prevKeyFrame, int nextKeyFrame, float keyFrameProgress) = ToKeyFrames(progress, startFrame, endFrame);
            Debug.Assert(mKeyFrames.Length > prevKeyFrame && mKeyFrames.Length > nextKeyFrame);
            
            if (prevKeyFrame == nextKeyFrame) return mKeyFrames[nextKeyFrame].Clone(); // @TODO need more testing
            
            AnimationFrame resultFrame = mKeyFrames[nextKeyFrame].Clone();
            mKeyFrames[prevKeyFrame].LerpInto(resultFrame, keyFrameProgress);
            return resultFrame;
        }

        private (int prevKeyFrame, int nextKeyFrame, float keyFrameProgress) ToKeyFrames(float progress, float startFrame, float endFrame)
        {
            float currentFrame = CalcCurrentFrame(progress, startFrame, endFrame);

            (int startKeyFrame, int endKeyFrame) = FindKeyFrames(currentFrame);

            if (endKeyFrame == startKeyFrame) return (startKeyFrame, startKeyFrame, 1);

            float firstFrame = mFrames[startKeyFrame];
            float lastFrame = mFrames[endKeyFrame];
            float keyFrameProgress = (currentFrame - firstFrame) / (lastFrame - firstFrame);

            return (startKeyFrame, endKeyFrame, keyFrameProgress);
        }

        private float CalcCurrentFrame(float progress, float startFrame, float endFrame)
        {
            if (!mCyclic || startFrame < endFrame) return startFrame + (endFrame - startFrame) * progress;

            float framesLeft = mTotalFrames - startFrame;
            float frameProgress = progress * (endFrame + framesLeft);
            if (frameProgress < framesLeft)
            {
                return startFrame + frameProgress;
            }
            else
            {
                return frameProgress - framesLeft;
            }
        }

        private (int startKeyFrame, int endKeyFrame) FindKeyFrames(float currentFrame)
        {
            int endKeyFrame = 0;
            int startKeyFrame = 0;

            for (endKeyFrame = 0; endKeyFrame < mKeyFrames.Length && mFrames[endKeyFrame] < currentFrame; endKeyFrame++)
            {
                startKeyFrame = endKeyFrame;
            }

            if (endKeyFrame >= mKeyFrames.Length) endKeyFrame = mKeyFrames.Length - 1;

            if (mCyclic && startKeyFrame == endKeyFrame) endKeyFrame = 0;

            return (startKeyFrame, endKeyFrame);
        }
    }
}
