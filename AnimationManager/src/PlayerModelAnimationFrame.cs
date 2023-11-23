using System;
using Vintagestory.API.Common;
using AnimationManagerLib.API;
using System.Collections.Generic;
using Vintagestory.Common;
using System.Linq;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationFrame : IAnimationResult
    {
        private readonly Dictionary<string, PlayerModelAnimationPose> mPoses;

        private PlayerModelAnimationFrame()
        {
            mPoses = new();
        }
        public PlayerModelAnimationFrame(Dictionary<string, PlayerModelAnimationPose> poses)
        {
            mPoses = poses;
        }

        public PlayerModelAnimationFrame(Dictionary<string, PlayerModelAnimationPose> poses, bool shallowCopy = true)
        {
            if (shallowCopy)
            {
                mPoses = poses.ToDictionary(entry => entry.Key,
                                            entry => entry.Value);
            }
            else
            {
                mPoses = poses.ToDictionary(entry => entry.Key,
                                            entry => (PlayerModelAnimationPose) (entry.Value as ICloneable).Clone());
            }
        }

        public void ApplyByAddition(Vintagestory.API.Common.IAnimator animator)
        {
            foreach ((string name, var pose) in mPoses)
            {
                pose.ApplyByAddition(animator.GetPosebyName(name));
            }
        }

        public void ApplyByAverage(Vintagestory.API.Common.IAnimator animator, float poseWeight, float thisWeight)
        {
            foreach ((string name, var pose) in mPoses)
            {
                pose.ApplyByAverage(animator.GetPosebyName(name), poseWeight, thisWeight);
            }
        }

        IAnimationResult IAnimationResult.Add(IAnimationResult value)
        {
            if (!(value is PlayerModelAnimationFrame)) throw new ArgumentException("[PlayerModelAnimationFrame] Argument should be an 'ElementPose'");

            PlayerModelAnimationFrame clone = value.Clone() as PlayerModelAnimationFrame;

            foreach ((string key, var pose) in clone.mPoses)
            {
                if (mPoses.ContainsKey(key))
                {
                    (pose as IAnimationResult).Add(mPoses[key]);
                }
            }

            foreach ((string key, var pose) in mPoses)
            {
                if (!clone.mPoses.ContainsKey(key))
                {
                    clone.mPoses.Add(key, pose);
                }
            }

            return clone;
        }

        IAnimationResult IAnimationResult.Average(IAnimationResult value, float weight, float thisWeight)
        {
            if (!(value is PlayerModelAnimationFrame)) throw new ArgumentException("[PlayerModelAnimationFrame] Argument should be an 'ElementPose'");

            PlayerModelAnimationFrame clone = value.Clone() as PlayerModelAnimationFrame;

            foreach ((string key, var pose) in clone.mPoses)
            {
                if (mPoses.ContainsKey(key))
                {
                    (pose as IAnimationResult).Average(mPoses[key], weight, thisWeight);
                }
            }

            foreach ((string key, var pose) in mPoses)
            {
                if (!clone.mPoses.ContainsKey(key))
                {
                    clone.mPoses.Add(key, pose);
                }
            }

            return clone;
        }

        object ICloneable.Clone()
        {
            PlayerModelAnimationFrame clone = new PlayerModelAnimationFrame(mPoses, shallowCopy: true);

            return clone;
        }

        IAnimationResult IAnimationResult.Subtract(IAnimationResult value)
        {
            throw new NotImplementedException();
        }

        IAnimationResult IAnimationResult.Identity()
        {
            return new PlayerModelAnimationFrame();
        }
    }

    public class PlayerModelAnimationPose : IAnimationResult
    {
        public float? degX = null;
        public float? degY = null;
        public float? degZ = null;

        public float? translateX = null;
        public float? translateY = null;
        public float? translateZ = null;

        public bool RotShortestDistanceX = true;
        public bool RotShortestDistanceY = true;
        public bool RotShortestDistanceZ = true;

        public PlayerModelAnimationPose()
        {

        }

        public PlayerModelAnimationPose(AnimationKeyFrameElement pose)
        {
            degX = (float?)pose.RotationX;
            degY = (float?)pose.RotationY;
            degZ = (float?)pose.RotationZ;
            translateX = (float?)pose.OffsetX;
            translateY = (float?)pose.OffsetY;
            translateZ = (float?)pose.OffsetZ;
            RotShortestDistanceX = pose.RotShortestDistanceX;
            RotShortestDistanceY = pose.RotShortestDistanceY;
            RotShortestDistanceZ = pose.RotShortestDistanceZ;
        }

        public PlayerModelAnimationPose(ElementPose pose)
        {
            degX = pose.degX;
            degY = pose.degY;
            degZ = pose.degZ;
            translateX = pose.translateX;
            translateY = pose.translateY;
            translateZ = pose.translateZ;
            RotShortestDistanceX = pose.RotShortestDistanceX;
            RotShortestDistanceY = pose.RotShortestDistanceY;
            RotShortestDistanceZ = pose.RotShortestDistanceZ;
        }

        public void ApplyByAddition(ElementPose pose)
        {
            pose.translateX += translateX ?? 0;
            pose.translateY += translateY ?? 0;
            pose.translateZ += translateZ ?? 0;
            pose.degX += degX ?? 0;
            pose.degY += degY ?? 0;
            pose.degZ += degZ ?? 0;
        }

        public void ApplyByAverage(ElementPose pose, float poseWeight, float thisWeight)
        {
            if (translateX != null) pose.translateX += Average((float)translateX, pose.translateX, poseWeight, thisWeight);
            if (translateY != null) pose.translateY += Average((float)translateY, pose.translateY, poseWeight, thisWeight);
            if (translateZ != null) pose.translateZ += Average((float)translateZ, pose.translateZ, poseWeight, thisWeight);
            if (degX != null) pose.degX += Average((float)degX, pose.degX, poseWeight, thisWeight);
            if (degY != null) pose.degY += Average((float)degY, pose.degY, poseWeight, thisWeight);
            if (degZ != null) pose.degZ += Average((float)degZ, pose.degZ, poseWeight, thisWeight);
        }

        IAnimationResult IAnimationResult.Add(IAnimationResult value)
        {
            if (!(value is PlayerModelAnimationPose)) throw new ArgumentException(" [PlayerModelAnimationPose] Argument should be an 'PlayerModelAnimationPose'");

            PlayerModelAnimationPose pose = value as PlayerModelAnimationPose;

            translateX = Add(translateX, pose.translateX);
            translateY = Add(translateY, pose.translateY);
            translateZ = Add(translateZ, pose.translateZ);
            degX = Add(degX, pose.degX);
            degY = Add(degY, pose.degY);
            degZ = Add(degZ, pose.degZ);

            return this;
        }

        IAnimationResult IAnimationResult.Average(IAnimationResult value, float weight, float thisWeight)
        {
            if (!(value is PlayerModelAnimationPose)) throw new ArgumentException(" [PlayerModelAnimationPose] Argument should be an 'PlayerModelAnimationPose'");

            PlayerModelAnimationPose pose = value as PlayerModelAnimationPose;

            translateX = Average(translateX, pose.translateX, weight, thisWeight);
            translateY = Average(translateY, pose.translateY, weight, thisWeight);
            translateZ = Average(translateZ, pose.translateZ, weight, thisWeight);
            degX = Average(degX, pose.degX, weight, thisWeight); // @TODO shortest distance
            degY = Average(degY, pose.degY, weight, thisWeight);
            degZ = Average(degZ, pose.degZ, weight, thisWeight);

            return this;
        }

        object ICloneable.Clone()
        {
            PlayerModelAnimationPose clone = (PlayerModelAnimationPose)MemberwiseClone();

            return clone;
        }

        IAnimationResult IAnimationResult.Subtract(IAnimationResult value)
        {
            throw new NotImplementedException();
        }

        private float? Average(float? thisValue, float? givenValue, float weight, float thisWeight = 1) => thisValue == null || givenValue == null ? thisValue ?? givenValue : Average((float)thisValue, (float)givenValue, weight, thisWeight);
        private float Average(float thisValue, float givenValue, float weight, float thisWeight = 1) => (thisValue * thisWeight + givenValue * weight) / (thisWeight + weight);
        private float? Add(float? thisValue, float? valueToAdd) => thisValue != null ? thisValue + valueToAdd ?? 0 : valueToAdd;

        IAnimationResult IAnimationResult.Identity()
        {
            return new PlayerModelAnimationPose();
        }
    }
}