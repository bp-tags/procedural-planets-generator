﻿using MyEngine;
using MyEngine.Components;
using Neitri;
using OpenTK;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyGame.PlanetaryBody
{
	public partial class Chunk
	{
		int NumberOfVerticesOnEdge => planetaryBody.chunkNumberOfVerticesOnEdge;

		List<Mesh.VertexIndex> GetEdgeVertices()
		{
			var skirtIndicies = new List<Mesh.VertexIndex>();
			// gather the edge vertices indicies
			{
				int lineStartIndex = 0;
				int nextLineStartIndex = 1;
				int numberOfVerticesInBetween = 0;
				skirtIndicies.Add(0); // first line
									  // top and all middle lines
				for (int i = 1; i < NumberOfVerticesOnEdge - 1; i++)
				{
					lineStartIndex = nextLineStartIndex;
					nextLineStartIndex = lineStartIndex + numberOfVerticesInBetween + 2;
					skirtIndicies.Add(lineStartIndex);
					skirtIndicies.Add((lineStartIndex + numberOfVerticesInBetween + 1));
					numberOfVerticesInBetween++;
				}
				// bottom line
				lineStartIndex = nextLineStartIndex;
				for (int i = 0; i < NumberOfVerticesOnEdge; i++)
				{
					skirtIndicies.Add((lineStartIndex + i));
				}
			}
			return skirtIndicies;
		}

		void CreateRendererAndGenerateMesh()
		{
			if (parentChunk != null && parentChunk.renderer == null)
			{
				parentChunk.RequestMeshGeneration();
				return;
			}
			lock (this)
			{
				if (isGenerated) return;
				isGenerated = true;
			}

			var mesh = new Mesh();// "PlanetaryBodyChunk depth:" + subdivisionDepth + " #" + numbetOfChunksGenerated);
			numberOfChunksGenerated++;

			bool useSkirts = false;
			useSkirts = true;

			// generate evenly spaced vertices, then we make triangles out of them
			var positionsFinal = new List<Vector3>();
			var normalsFinal = mesh.Normals;

			// the planetary chunk vertices blend from positonsInitial to positionsFinal
			// to smoothly blend in more detail as camera closes in
			// var positionsInitial = new List<Vector3>();
			var positionsInitial = new Mesh.VertexBufferObject<Vector3>()
			{
				ElementType = typeof(float),
				DataStrideInElementsNumber = 3,
			};
			var normalsInitial = new Mesh.VertexBufferObject<Vector3>()
			{
				ElementType = typeof(float),
				DataStrideInElementsNumber = 3,
			};

			// generate all of our vertices
			if (childPosition == ChildPosition.NoneNoParent)
			{
				//positionsFinal.Add(noElevationRange.a.ToVector3());
				positionsFinal.Add(planetaryBody.GetFinalPos(noElevationRange.a).ToVector3());

				// add positions, line by line
				{
					int numberOfVerticesInBetween = 0;
					for (uint y = 1; y < NumberOfVerticesOnEdge; y++)
					{
						var percent = y / (float)(NumberOfVerticesOnEdge - 1);
						var start = MyMath.Slerp(noElevationRange.a, noElevationRange.b, percent);
						var end = MyMath.Slerp(noElevationRange.a, noElevationRange.c, percent);
						//positionsFinal.Add(start.ToVector3());
						positionsFinal.Add(planetaryBody.GetFinalPos(start).ToVector3());

						if (numberOfVerticesInBetween > 0)
						{
							for (uint x = 1; x <= numberOfVerticesInBetween; x++)
							{
								var v = MyMath.Slerp(start, end, x / (float)(numberOfVerticesInBetween + 1));
								//positionsFinal.Add(v.ToVector3());
								positionsFinal.Add(planetaryBody.GetFinalPos(v).ToVector3());
							}
						}
						//positionsFinal.Add(end.ToVector3());
						positionsFinal.Add(planetaryBody.GetFinalPos(end).ToVector3());
						numberOfVerticesInBetween++;
					}
				}
			}
			else
			{
				// take some vertices from parents
				{
					var parentVertices = parentChunk.renderer.Mesh.Vertices;

					positionsFinal.Resize(parentVertices.Count);

					var parentIndicies = new ParentIndiciesEnumerator(parentChunk, childPosition);

					int i;

					i = 0;
					positionsFinal[i] = parentVertices[parentIndicies.Current];
					parentIndicies.MoveNext();
					i++;

					// copy position from parent
					int numberOfVerticesOnLine = 2;
					for (int y = 1; y < NumberOfVerticesOnEdge; y++)
					{
						for (int x = 0; x < numberOfVerticesOnLine; x++)
						{
							if (y % 2 == 0)
							{
								if (x % 2 == 0)
								{
									positionsFinal[i] = parentVertices[parentIndicies.Current];
									parentIndicies.MoveNext();
								}
							}
							i++;
						}
						numberOfVerticesOnLine++;
					}

					// fill in positions in between
					i = 1;
					numberOfVerticesOnLine = 2;
					for (int y = 1; y < NumberOfVerticesOnEdge; y++)
					{
						for (int x = 0; x < numberOfVerticesOnLine; x++)
						{
							if (y % 2 == 0)
							{
								if (x % 2 == 0)
								{
								}
								else
								{
									int a = i - 1;
									int b = i + 1;
									positionsFinal[i] = planetaryBody.GetFinalPos((positionsFinal[a].ToVector3d() + positionsFinal[b].ToVector3d()) / 2.0f).ToVector3();
								}
							}
							else
							{
								if (x % 2 == 0)
								{
									int a = i - numberOfVerticesOnLine + 1;
									int b = i + numberOfVerticesOnLine;
									positionsFinal[i] = planetaryBody.GetFinalPos((positionsFinal[a].ToVector3d() + positionsFinal[b].ToVector3d()) / 2.0f).ToVector3();
								}
								else
								{
									int a = i - numberOfVerticesOnLine;
									int b = i + numberOfVerticesOnLine + 1;
									positionsFinal[i] = planetaryBody.GetFinalPos((positionsFinal[a].ToVector3d() + positionsFinal[b].ToVector3d()) / 2.0f).ToVector3();
								}
							}
							i++;
						}
						numberOfVerticesOnLine++;
					}
				}
			}

			List<int> indicies;
			GetIndiciesList(NumberOfVerticesOnEdge, out indicies);

			mesh.Vertices.SetData(positionsFinal);
			mesh.TriangleIndicies.SetData(indicies);
			mesh.RecalculateNormals();

			// fill in initial positions, every odd positon is average of the two neighbouring final positions
			{
				positionsInitial.Resize(positionsFinal.Count);
				normalsInitial.Resize(positionsFinal.Count);

				int numberOfVerticesOnLine;
				int i;

				{
					var parentIndicies = new ParentIndiciesEnumerator(parentChunk, childPosition);
					IList<Vector3> parentNormals = null;
					i = 0;
					if (childPosition == ChildPosition.NoneNoParent)
					{
						normalsInitial[i] = normalsFinal[i];
					}
					else
					{
						parentNormals = parentChunk.renderer.Mesh.Normals;
						normalsInitial[i] = parentNormals[parentIndicies.Current];
						parentIndicies.MoveNext();
					}
					i++;
					numberOfVerticesOnLine = 2;
					for (int y = 1; y < NumberOfVerticesOnEdge; y++)
					{
						for (int x = 0; x < numberOfVerticesOnLine; x++)
						{
							if (y % 2 == 0)
							{
								if (x % 2 == 0)
								{
									if (childPosition == ChildPosition.NoneNoParent)
									{
										normalsInitial[i] = normalsFinal[i];
									}
									else
									{
										normalsInitial[i] = parentNormals[parentIndicies.Current];
										parentIndicies.MoveNext();
									}
								}
							}
							i++;
						}
						numberOfVerticesOnLine++;
					}
				}

				i = 0;
				positionsInitial[i] = positionsFinal[i];
				if (childPosition == ChildPosition.NoneNoParent) normalsInitial[i] = normalsFinal[i];
				i++;

				numberOfVerticesOnLine = 2;
				for (int y = 1; y < NumberOfVerticesOnEdge; y++)
				{
					for (int x = 0; x < numberOfVerticesOnLine; x++)
					{
						if (y % 2 == 0)
						{
							if (x % 2 == 0)
							{
								positionsInitial[i] = positionsFinal[i];
							}
							else
							{
								int a = i - 1;
								int b = i + 1;
								positionsInitial[i] = (positionsFinal[a] + positionsFinal[b]) / 2.0f;
								normalsInitial[i] = (normalsInitial[a] + normalsInitial[b]) / 2.0f;
							}
						}
						else
						{
							if (x % 2 == 0)
							{
								int a = i - numberOfVerticesOnLine + 1;
								int b = i + numberOfVerticesOnLine;
								positionsInitial[i] = (positionsFinal[a] + positionsFinal[b]) / 2.0f;
								normalsInitial[i] = (normalsInitial[a] + normalsInitial[b]) / 2.0f;
							}
							else
							{
								int a = i - numberOfVerticesOnLine;
								int b = i + numberOfVerticesOnLine + 1;
								positionsInitial[i] = (positionsFinal[a] + positionsFinal[b]) / 2.0f;
								normalsInitial[i] = (normalsInitial[a] + normalsInitial[b]) / 2.0f;
							}
						}
						i++;
					}

					numberOfVerticesOnLine++;
				}
			}

			// DEBUG
			/*
			for (int i = 0; i < positionsFinal.Count; i++)
			{
				normalsFinal[i].Normalize();
				normalsInitial[i].Normalize();
				//normalsInitial[i] = Vector3.Zero;
			}
			*/

			//Mesh.CalculateNormals(mesh.triangleIndicies, positionsInitial, normalsInitial);

			mesh.VertexArrayObj.AddVertexBufferObject("positionsInitial", positionsInitial);
			mesh.VertexArrayObj.AddVertexBufferObject("normalsInitial", normalsInitial);

			var edgeVertices = this.GetEdgeVertices().ToArray();

			var smoothEdgeNormals = true;
			smoothEdgeNormals = false;
			if (smoothEdgeNormals)
			{
				var v = mesh.Vertices;
				for (int i = 0; i < edgeVertices.Length; i++)
				{
					var evi = edgeVertices[i];
				}
			}

			if (useSkirts)
			{
				var skirtVertices = mesh.Duplicate(edgeVertices, mesh.Vertices, mesh.Normals, positionsInitial, normalsInitial);
				var moveAmount = this.realVisibleRange.ToBoundingSphere().radius / 1000;
				mesh.MoveVertices(skirtVertices, this.realVisibleRange.CenterPos.Towards(Vector3d.Zero).ToVector3() * (float)moveAmount, mesh.Vertices, positionsInitial);
			}

			mesh.RecalculateBounds();

			if (renderer != null) throw new Exception("something went terribly wrong, renderer should be null");
			renderer = planetaryBody.Entity.AddComponent<MeshRenderer>();
			renderer.Mesh = mesh;

			if (planetaryBody.planetMaterial != null) renderer.Material = planetaryBody.planetMaterial.CloneTyped();
			renderer.RenderingMode = RenderingMode.DontRender;
			this.visibility = 0;
		}
	}
}