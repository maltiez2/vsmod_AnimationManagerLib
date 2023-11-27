﻿using System;
using Vintagestory.API.Common;
using AnimationManagerLib.API;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationFrame : IAnimationResult
    {
        public Dictionary<string, PlayerModelAnimationPose> mPoses { get; }

        private readonly AnimationMetaData mMetaData;

        public PlayerModelAnimationFrame()
        {
            mPoses = new();
        }
        public PlayerModelAnimationFrame(Dictionary<string, PlayerModelAnimationPose> poses, AnimationMetaData metaData)
        {
            mPoses = poses;
            mMetaData = metaData;
        }
        public PlayerModelAnimationFrame(Dictionary<string, PlayerModelAnimationPose> poses, AnimationMetaData metaData, bool shallowCopy = true)
        {
            if (shallowCopy)
            {
                mPoses = poses.ToDictionary(entry => entry.Key,
                                            entry => entry.Value);
                mMetaData = metaData;
            }
            else
            {
                mPoses = poses.ToDictionary(entry => entry.Key,
                                            entry => (PlayerModelAnimationPose) (entry.Value as ICloneable).Clone());
                mMetaData = metaData?.Clone();
            }
        }

        public void ApplyByAddition(ElementPose shapePose, string name)
        {
            if (mPoses.ContainsKey(name)) mPoses[name].ApplyByAddition(shapePose);
        }

        public void ApplyByAverage(ElementPose shapePose, string name, float poseWeight, float thisWeight)
        {
            if (mPoses.ContainsKey(name)) mPoses[name].ApplyByAverage(shapePose, poseWeight, thisWeight);
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
                    (pose as IAnimationResult).Average(mPoses[key], thisWeight / pose.ElementWeight, weight);
                }
            }

            foreach ((string key, var pose) in mPoses)
            {
                if (!clone.mPoses.ContainsKey(key))
                {
                    clone.mPoses.Add(key, new PlayerModelAnimationPose());
                    (clone.mPoses[key] as IAnimationResult).Average(pose, thisWeight / pose.ElementWeight, weight);
                }
            }

            return clone;
        }
        IAnimationResult IAnimationResult.Lerp(IAnimationResult value, float progress)
        {
            if (!(value is PlayerModelAnimationFrame)) throw new ArgumentException("[PlayerModelAnimationFrame] Argument should be an 'ElementPose'");

            PlayerModelAnimationFrame clone = value.Clone() as PlayerModelAnimationFrame;

            foreach ((string key, var pose) in clone.mPoses)
            {
                if (mPoses.ContainsKey(key))
                {
                    (pose as IAnimationResult).Lerp(mPoses[key], 1 - progress);
                }
            }

            foreach ((string key, var pose) in mPoses)
            {
                if (!clone.mPoses.ContainsKey(key))
                {
                    clone.mPoses.Add(key, new PlayerModelAnimationPose());
                    (clone.mPoses[key] as IAnimationResult).Lerp(pose, 1 - progress);
                }
            }

            return clone;
        }

        object ICloneable.Clone()
        {
            PlayerModelAnimationFrame clone = new PlayerModelAnimationFrame(mPoses, mMetaData, shallowCopy: false);

            return clone;
        }

        IAnimationResult IAnimationResult.Identity()
        {
            return new PlayerModelAnimationFrame();
        }
    }

    public class PlayerModelAnimationPose : IAnimationResult
    {
        public EnumAnimationBlendMode? BlendMode { get; set; } = EnumAnimationBlendMode.AddAverage;
        public float ElementWeight { get; set; } = 1f;

        public float? degX { get; set; } = null;
        public float? degY { get; set; } = null;
        public float? degZ { get; set; } = null;

        public float? translateX { get; set; } = null;
        public float? translateY { get; set; } = null;
        public float? translateZ { get; set; } = null;

        public bool RotShortestDistanceX { get; set; } = true;
        public bool RotShortestDistanceY { get; set; } = true;
        public bool RotShortestDistanceZ { get; set; } = true;

        public PlayerModelAnimationPose()
        {

        }

        public PlayerModelAnimationPose(AnimationKeyFrameElement pose, EnumAnimationBlendMode? blendMode, float weight)
        {
            BlendMode = blendMode;
            ElementWeight = weight;
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

        public void ApplyByAddition(ElementPose pose)
        {
            pose.translateX += translateX / 16 ?? 0;
            pose.translateY += translateY / 16 ?? 0;
            pose.translateZ += translateZ / 16 ?? 0;
            pose.degX += degX ?? 0;
            pose.degY += degY ?? 0;
            pose.degZ += degZ ?? 0;
        }

        public void ApplyByAverage(ElementPose pose, float poseWeight, float thisWeight)
        {
            if (translateX != null) pose.translateX = Average((float)translateX / 16, pose.translateX, poseWeight, thisWeight * 1);
            if (translateY != null) pose.translateY = Average((float)translateY / 16, pose.translateY, poseWeight, thisWeight * 1);
            if (translateZ != null) pose.translateZ = Average((float)translateZ / 16, pose.translateZ, poseWeight, thisWeight * 1);
            if (degX != null) pose.degX = Average((float)degX, pose.degX, poseWeight, thisWeight * 1);
            if (degY != null) pose.degY = Average((float)degY, pose.degY, poseWeight, thisWeight * 1);
            if (degZ != null) pose.degZ = Average((float)degZ, pose.degZ, poseWeight, thisWeight * 1);
        }

        IAnimationResult IAnimationResult.Add(IAnimationResult value)
        {
            if (value is not PlayerModelAnimationPose) throw new ArgumentException(" [PlayerModelAnimationPose] Argument should be an 'PlayerModelAnimationPose'");

            PlayerModelAnimationPose pose = value as PlayerModelAnimationPose;

            AddToPose(pose);

            return this;
        }

        private void AddToPose(PlayerModelAnimationPose pose)
        {
            translateX = Add(translateX, pose.translateX);
            translateY = Add(translateY, pose.translateY);
            translateZ = Add(translateZ, pose.translateZ);
            degX = Add(degX, pose.degX);
            degY = Add(degY, pose.degY);
            degZ = Add(degZ, pose.degZ);
        }

        IAnimationResult IAnimationResult.Average(IAnimationResult value, float weight, float thisWeight)
        {
            if (value is not PlayerModelAnimationPose) throw new ArgumentException(" [PlayerModelAnimationPose] Argument should be an 'PlayerModelAnimationPose'");

            PlayerModelAnimationPose pose = value as PlayerModelAnimationPose;

            float elementWeight = thisWeight * 1;
            AverageToPose(pose, weight, elementWeight);
            //pose.ElementWeight += ElementWeight;

            /*switch (BlendMode ?? EnumAnimationBlendMode.Average)
            {
                case EnumAnimationBlendMode.Average:
                    AverageToPose(pose, weight, elementWeight);
                    pose.ElementWeight += elementWeight;
                    break;
                case EnumAnimationBlendMode.Add:
                    AddToPose(pose);
                    break;
                case EnumAnimationBlendMode.AddAverage:
                    AddToPose(pose);
                    pose.ElementWeight += elementWeight;
                    break;
            }*/

            return this;
        }

        private void AverageToPose(PlayerModelAnimationPose pose, float weight, float thisWeight)
        {
            translateX = Average(translateX, pose.translateX, weight, thisWeight);
            translateY = Average(translateY, pose.translateY, weight, thisWeight);
            translateZ = Average(translateZ, pose.translateZ, weight, thisWeight);
            degX = Average(degX, pose.degX, weight, thisWeight); // @TODO shortest distance
            degY = Average(degY, pose.degY, weight, thisWeight);
            degZ = Average(degZ, pose.degZ, weight, thisWeight);
        }

        IAnimationResult IAnimationResult.Lerp(IAnimationResult value, float progress)
        {
            if (value is not PlayerModelAnimationPose) throw new ArgumentException(" [PlayerModelAnimationPose] Argument should be an 'PlayerModelAnimationPose'");

            PlayerModelAnimationPose pose = value as PlayerModelAnimationPose;

            LerpToPose(pose, progress);

            return this;
        }

        private void LerpToPose(PlayerModelAnimationPose pose, float progress)
        {
            translateX = Lerp(translateX, pose.translateX, progress);
            translateY = Lerp(translateY, pose.translateY, progress);
            translateZ = Lerp(translateZ, pose.translateZ, progress);
            degX = Lerp(degX, pose.degX, progress); // @TODO shortest distance
            degY = Lerp(degY, pose.degY, progress);
            degZ = Lerp(degZ, pose.degZ, progress);
        }

        object ICloneable.Clone()
        {
            PlayerModelAnimationPose clone = (PlayerModelAnimationPose)MemberwiseClone();

            return clone;
        }

        static private float? Lerp(float? thisValue, float? givenValue, float progress) => thisValue == null && givenValue == null ? null : Average(thisValue ?? 0, givenValue ?? 0, progress, 1 - progress);
        static private float? Average(float? thisValue, float? givenValue, float weight, float thisWeight = 1) => thisValue == null || givenValue == null ? thisValue ?? givenValue : Average((float)thisValue, (float)givenValue, weight, thisWeight);
        static private float Average(float thisValue, float givenValue, float weight, float thisWeight = 1) => (thisValue * thisWeight + givenValue * weight) / (thisWeight + weight);
        static private float? Add(float? thisValue, float? valueToAdd) => thisValue != null ? thisValue + valueToAdd ?? 0 : valueToAdd;

        IAnimationResult IAnimationResult.Identity()
        {
            return new PlayerModelAnimationPose();
        }
    }
}