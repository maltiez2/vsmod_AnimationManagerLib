using System;
using Vintagestory.API.Common;
using AnimationManagerLib.API;
using Vintagestory.API.Common.Entities;

namespace AnimationManagerLib
{
    public class PlayerModelAnimationBehavior : EntityBehavior
    {
        private readonly Entity mEntity;

        public PlayerModelAnimationBehavior(Entity entity) : base(entity)
        {
            mEntity = entity;
        }

        public override string PropertyName()
        {
            return "playermodelsnimationmehavior";
        }
    }
}
