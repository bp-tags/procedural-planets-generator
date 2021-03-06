﻿using MyEngine.Components;
using MyEngine.Events;
using Neitri;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MyEngine
{
	public class Entity : System.IDisposable
	{


		public InputSystem Input => Scene.Input;
		public MyDebug Debug => Scene.Debug;
		public Factory Factory => Scene.Factory;
		public ILog Log => Scene.Log;
		public FileSystem FileSystem => Scene.FileSystem;
		public Events.EventSystem EventSystem => Scene.EventSystem;




		public Transform Transform { get; private set; }

		public SceneSystem Scene { get; private set; }



		public IReadOnlyList<IComponent> Components => components.AsReadOnly();

		List<IComponent> components = new List<IComponent>();

		public string Name { get; set; }

		public Entity(SceneSystem scene, string name = "")
		{
			this.Scene = scene;
			this.Name = name;
			this.Transform = this.AddComponent<Transform>();
		}

		public T GetComponent<T>() where T : class
		{
			lock (components)
			{
				foreach (var c in components)
				{
					if (c is T)
					{
						return c as T;
					}
				}
			}
			return null;
		}

		public List<T> GetComponents<T>() where T : class, IComponent
		{
			List<T> ret = new List<T>();
			lock (components)
			{
				foreach (var c in components)
				{
					if (c is T)
					{
						ret.Add(c as T);
					}
				}
			}
			return ret;
		}

		public T AddComponent<T>() where T : Component, new()
		{
			var componentSettingAttribute = typeof(T).GetCustomAttribute<ComponentSettingAttribute>(true);
			if (componentSettingAttribute != null && componentSettingAttribute.allowMultiple == false)
			{
				if (GetComponent<T>() != null)
				{
					Log.Warn("Attempting to add " + typeof(T) + " component, but " + typeof(ComponentSettingAttribute) + " allows only one component of this type per " + typeof(Entity) + ".");
					return null;
				}
			}

			T c = new T();
			//T c = System.Activator.CreateInstance(typeof(T), this) as T;
			lock (components)
				components.Add(c);
			c.OnAddedToEntity(this);
			return c;
		}

		public void DestroyAllComponents()
		{
			lock (components)
			{
				foreach (var component in components)
				{
					if (component is IDisposable)
						(component as IDisposable).Dispose();
				}
				components.Clear();
			}
		}

		public void DestroyComponent(IComponent component)
		{
			lock (components)
			{
				if (components.Contains(component))
					components.Remove(component);
				else
					return;
			}
			if (component is IDisposable)
				(component as IDisposable).Dispose();
		}


		bool alreadyDestroyed = false;
		public void Destroy()
		{
			if (alreadyDestroyed) return;
			alreadyDestroyed = true;
			DestroyAllComponents();
			Scene.Remove(this);
		}
		void IDisposable.Dispose() => Destroy();

		public override string ToString()
		{
			return Name;
		}
	}

	public enum ChangedFlags
	{
		Position = 1 << 0,
		Rotation = 1 << 1,
		Scale = 1 << 2,
		Bounds = 1 << 3,
		VisualRepresentation = 1 << 4,
		PhysicsSettings = 1 << 5,
		PhysicalShape = 1 << 6,
		All = 0xffff
	}
}