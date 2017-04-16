﻿using Neitri;
using Neitri.Base;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace MyEngine.Components
{
	[Flags]
	public enum RenderStatus
	{
		NotRendered = 0,
		Rendered = (1 << 1),
		RenderedForced = (1 << 1) | (1 << 2),
		Visible = (1 << 3),
		RenderedAndVisible = (1 << 1) | (1 << 3),
		Unknown = (1 << 9),
	}

	public class RenderContext
	{
		public static readonly RenderContext Geometry = new RenderContext("geometry");
		public static readonly RenderContext Shadows = new RenderContext("shadow");
		public static readonly RenderContext Depth = new RenderContext("depth");
		public string Name { get; set; }

		public RenderContext(string name)
		{
			this.Name = name;
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	public interface IRenderable
	{
		Material Material { get; }
		bool ForcePassCulling { get; }

		bool ShouldRenderInContext(Camera camera, RenderContext renderContext);
		void SetCameraRenderStatusFeedback(Camera camera, RenderStatus renderStatus);

		void UploadUBOandDraw(CameraData camera, UniformBlock ubo);

		IEnumerable<Vector3> GetCameraSpaceOccluderTriangles(CameraData camera);
		Bounds GetFloatingOriginSpaceBounds(WorldPos viewPointPos);
		CameraSpaceBounds GetCameraSpaceBounds(CameraData camera);
	}

	public abstract class Renderer : ComponentWithShortcuts, IRenderable, IDisposable
	{
		MyRenderingMode renderingMode;
		public virtual MyRenderingMode RenderingMode
		{
			get => renderingMode;
			set
			{
				if (value != renderingMode)
				{
					DataToRender.IncreaseVersion();
					renderingMode = value;
				}
			}
		}

		Material material;
		public virtual Material Material
		{
			get => material;
			set
			{
				if (value != material)
				{
					DataToRender.IncreaseVersion();
					material = value;
				}
			}
		}

		Dictionary<Camera, RenderStatus> cameraToRenderStatus = new Dictionary<Camera, RenderStatus>();

		bool forcePassCulling;
		public virtual bool ForcePassCulling
		{
			get => forcePassCulling;
			set
			{
				if (value != forcePassCulling)
				{
					DataToRender.IncreaseVersion();
					forcePassCulling = value;
				}
			}
		}

		protected RenderableData DataToRender => Entity.Scene.DataToRender;

		public override void OnAddedToEntity(Entity entity)
		{
			base.OnAddedToEntity(entity);

			DataToRender.Add(this);
		}

		public abstract Bounds GetFloatingOriginSpaceBounds(WorldPos viewPointPos);

		public virtual void UploadUBOandDraw(CameraData camera, UniformBlock ubo)
		{
		}

		public virtual void SetCameraRenderStatusFeedback(Camera camera, RenderStatus renderStatus)
		{
			cameraToRenderStatus[camera] = renderStatus;
		}

		public virtual RenderStatus GetCameraRenderStatusFeedback(Camera camera)
		{
			return cameraToRenderStatus.GetValue(camera, RenderStatus.Unknown);
		}

		public virtual bool ShouldRenderInContext(Camera camera, RenderContext renderContext)
		{
			if (renderContext == RenderContext.Geometry && RenderingMode.HasFlag(MyRenderingMode.RenderGeometry)) return true;
			if (renderContext == RenderContext.Shadows && RenderingMode.HasFlag(MyRenderingMode.CastShadows)) return true;
			return false;
		}

		public void Dispose()
		{
			DataToRender.Remove(this);
		}

		public override string ToString()
		{
			return Entity.Name;
		}

		public abstract IEnumerable<Vector3> GetCameraSpaceOccluderTriangles(CameraData camera);
		public abstract CameraSpaceBounds GetCameraSpaceBounds(CameraData camera);

	}
}