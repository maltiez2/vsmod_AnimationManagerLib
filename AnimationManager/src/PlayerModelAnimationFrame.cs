using System;
using Vintagestory.API.Common;
using AnimationManagerLib.API;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationFrame : ElementPose, IAnimationResult
    {
        public PlayerModelAnimationFrame()
        {
            Clear();
        }

        public PlayerModelAnimationFrame(ElementPose pose)
        {
            ForElement = pose.ForElement;
            AnimModelMatrix = pose.AnimModelMatrix;
            ChildElementPoses = pose.ChildElementPoses;
            degOffX = pose.degOffX;
            degOffY = pose.degOffY;
            degOffZ = pose.degOffZ;
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

        IAnimationResult IAnimationResult.Add(IAnimationResult value)
        {
            if (!(value is ElementPose)) throw new ArgumentException(" [PlayerModelAnimationFrame] Argument should be an 'ElementPose'");

            PlayerModelAnimationFrame pose = new PlayerModelAnimationFrame(value as ElementPose);

            pose.translateX += translateX;
            pose.translateY += translateY;
            pose.translateZ += translateZ;
            pose.degX += degX;
            pose.degY += degY;
            pose.degZ += degZ;
            pose.scaleX += scaleX;
            pose.scaleY += scaleY;
            pose.scaleZ += scaleZ;

            return value;
        }

        IAnimationResult IAnimationResult.Average(IAnimationResult value, float weight, float thisWeight)
        {
            if (!(value is ElementPose)) throw new ArgumentException(" [PlayerModelAnimationFrame] Argument should be an 'ElementPose'");

            PlayerModelAnimationFrame pose = new PlayerModelAnimationFrame(value as ElementPose);

            pose.translateX = Average(translateX, pose.translateX, weight, thisWeight);
            pose.translateY = Average(translateY, pose.translateY, weight, thisWeight);
            pose.translateZ = Average(translateZ, pose.translateZ, weight, thisWeight);
            pose.degX = Average(degX, pose.degX, weight, thisWeight); // @TODO shortest distance
            pose.degY = Average(degY, pose.degY, weight, thisWeight);
            pose.degZ = Average(degZ, pose.degZ, weight, thisWeight);
            pose.scaleX = Average(scaleX, pose.scaleX, weight, thisWeight);
            pose.scaleY = Average(scaleY, pose.scaleY, weight, thisWeight);
            pose.scaleZ = Average(scaleZ, pose.scaleZ, weight, thisWeight);

            return value;
        }

        object ICloneable.Clone()
        {
            PlayerModelAnimationFrame clone = (PlayerModelAnimationFrame)MemberwiseClone();

            return clone;
        }

        IAnimationResult IAnimationResult.Subtract(IAnimationResult value)
        {
            throw new NotImplementedException();
        }

        private float Average(float thisValue, float givenValue, float weight, float thisWeight = 1) => (thisValue * thisWeight + givenValue * weight) / (thisWeight + weight);

        IAnimationResult IAnimationResult.Identity()
        {
            return new PlayerModelAnimationFrame();
        }
    }

}