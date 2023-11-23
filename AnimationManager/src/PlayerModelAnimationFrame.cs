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
        public float degX = 0;
        public float degY = 0;
        public float degZ = 0;

        public float scaleX = 1f;
        public float scaleY = 1f;
        public float scaleZ = 1f;

        public float translateX = 0;
        public float translateY = 0;
        public float translateZ = 0;

        public bool RotShortestDistanceX = true;
        public bool RotShortestDistanceY = true;
        public bool RotShortestDistanceZ = true;

        public PlayerModelAnimationPose()
        {

        }

        public PlayerModelAnimationPose(ElementPose pose)
        {
            degX = pose.degX;
            degY = pose.degY;
            degZ = pose.degZ;
            scaleX = pose.scaleX;
            scaleY = pose.scaleY;
            scaleZ = pose.scaleZ;
            translateX = pose.translateX;
            translateY = pose.translateY;
            translateZ = pose.translateZ;
            RotShortestDistanceX = pose.RotShortestDistanceX;
            RotShortestDistanceY = pose.RotShortestDistanceY;
            RotShortestDistanceZ = pose.RotShortestDistanceZ;
        }

        public void ApplyByAddition(ElementPose pose)
        {
            pose.translateX += translateX;
            pose.translateY += translateY;
            pose.translateZ += translateZ;
            pose.degX += degX;
            pose.degY += degY;
            pose.degZ += degZ;
            pose.scaleX += scaleX;
            pose.scaleY += scaleY;
            pose.scaleZ += scaleZ;
        }

        public void ApplyByAverage(ElementPose pose, float poseWeight, float thisWeight)
        {
            pose.translateX += Average(translateX, pose.translateX, poseWeight, thisWeight);
            pose.translateY += Average(translateY, pose.translateY, poseWeight, thisWeight);
            pose.translateZ += Average(translateZ, pose.translateZ, poseWeight, thisWeight);
            pose.degX += Average(degX, pose.degX, poseWeight, thisWeight);
            pose.degY += Average(degY, pose.degY, poseWeight, thisWeight);
            pose.degZ += Average(degZ, pose.degZ, poseWeight, thisWeight);
            pose.scaleX += Average(scaleX, pose.scaleX, poseWeight, thisWeight);
            pose.scaleY += Average(scaleY, pose.scaleY, poseWeight, thisWeight);
            pose.scaleZ += Average(scaleZ, pose.scaleZ, poseWeight, thisWeight);
        }

        IAnimationResult IAnimationResult.Add(IAnimationResult value)
        {
            if (!(value is PlayerModelAnimationPose)) throw new ArgumentException(" [PlayerModelAnimationPose] Argument should be an 'PlayerModelAnimationPose'");

            PlayerModelAnimationPose pose = value as PlayerModelAnimationPose;

            translateX += pose.translateX;
            translateY += pose.translateY;
            translateZ += pose.translateZ;
            degX += pose.degX;
            degY += pose.degY;
            degZ += pose.degZ;
            scaleX += pose.scaleX;
            scaleY += pose.scaleY;
            scaleZ += pose.scaleZ;

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
            scaleX = Average(scaleX, pose.scaleX, weight, thisWeight);
            scaleY = Average(scaleY, pose.scaleY, weight, thisWeight);
            scaleZ = Average(scaleZ, pose.scaleZ, weight, thisWeight);

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

        private float Average(float thisValue, float givenValue, float weight, float thisWeight = 1) => (thisValue * thisWeight + givenValue * weight) / (thisWeight + weight);

        IAnimationResult IAnimationResult.Identity()
        {
            return new PlayerModelAnimationPose();
        }
    }
}