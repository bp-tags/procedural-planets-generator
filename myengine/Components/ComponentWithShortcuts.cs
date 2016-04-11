﻿using System;
using System.Collections.Generic;
using System.Text;

namespace MyEngine.Components
{
    public abstract class ComponentWithShortcuts : Component
    {
        public InputSystem Input
        {
            get
            {
                return Entity.Input;
            }
        }
        public Transform Transform
        {
            get
            {
                return Entity.Transform;
            }
        }
        public SceneSystem Scene
        {
            get
            {
                return Entity.Scene;
            }
        }
        public ComponentWithShortcuts(Entity entity) : base(entity)
        {

        }

    }
}