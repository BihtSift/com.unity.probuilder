﻿using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace UnityEngine.ProBuilder
{
	public sealed partial class ProBuilderMesh
	{
		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize()
		{
			// Used in the Editor after Undo
			InvalidateSharedVertexLookup();
		}

		/// <summary>
		/// Reset all the attribute arrays on this object.
		/// </summary>
		public void Clear()
		{
			// various editor tools expect faces & vertexes to always be valid.
			// ideally we'd null everything here, but that would break a lot of existing code.
			m_Faces = new Face[0];
			m_Positions = new Vector3[0];
			m_Textures0 = new Vector2[0];
			m_Textures2 = null;
			m_Textures3 = null;
			m_Tangents = null;
			m_SharedVertexes = new SharedVertex[0];
			m_SharedTextures = new SharedVertex[0];
			m_Colors = null;
			ClearSelection();
		}

		void OnDestroy()
		{
			// Time.frameCount is zero when loading scenes in the Editor. It's the only way I could figure to
			// differentiate between OnDestroy invoked from user delete & editor scene loading.
			if (!preserveMeshAssetOnDestroy &&
				Application.isEditor &&
				!Application.isPlaying &&
				Time.frameCount > 0)
			{
				if (meshWillBeDestroyed != null)
					meshWillBeDestroyed(this);
				else
					DestroyImmediate(gameObject.GetComponent<MeshFilter>().sharedMesh, true);
			}
		}

		internal static ProBuilderMesh CreateInstanceWithPoints(Vector3[] positions)
		{
			if (positions.Length % 4 != 0)
			{
				Log.Warning("Invalid Geometry. Make sure vertexes in are pairs of 4 (faces).");
				return null;
			}

			GameObject go = new GameObject();
			ProBuilderMesh pb = go.AddComponent<ProBuilderMesh>();
			go.name = "ProBuilder Mesh";
			pb.GeometryWithPoints(positions);

			return pb;
		}

		/// <summary>
		/// Create a new GameObject with a ProBuilderMesh component, MeshFilter, and MeshRenderer, then initializes the ProBuilderMesh with a set of positions and faces.
		/// </summary>
		/// <param name="positions">Vertex positions array.</param>
		/// <param name="faces">Faces array.</param>
		/// <returns></returns>
		public static ProBuilderMesh Create(IEnumerable<Vector3> positions, IEnumerable<Face> faces)
		{
			GameObject go = new GameObject();
			ProBuilderMesh pb = go.AddComponent<ProBuilderMesh>();
			go.name = "ProBuilder Mesh";
			pb.RebuildWithPositionsAndFaces(positions, faces);
			return pb;
		}

		/// <summary>
		/// Create a new GameObject with a ProBuilderMesh component, MeshFilter, and MeshRenderer, then initializes the ProBuilderMesh with a set of positions and faces.
		/// </summary>
		/// <param name="vertexes">Vertex positions array.</param>
		/// <param name="faces">Faces array.</param>
		/// <returns></returns>
		public static ProBuilderMesh[] Create(IEnumerable<Vertex> vertexes, IEnumerable<Face> faces, IEnumerable<SharedVertex> sharedVertexes = null, IEnumerable<SharedVertex> sharedTextures = null)
		{
			// todo
			throw new NotImplementedException();
		}

		void GeometryWithPoints(Vector3[] points)
		{
			// Wrap in faces
			Face[] f = new Face[points.Length / 4];

			for (int i = 0; i < points.Length; i += 4)
			{
				f[i / 4] = new Face(new int[6]
					{
						i + 0, i + 1, i + 2,
						i + 1, i + 3, i + 2
					},
					BuiltinMaterials.defaultMaterial,
					AutoUnwrapSettings.tile,
					0,
					-1,
					-1,
					false);
			}

			Clear();
			positions = points;
			m_Faces = f;
			m_SharedVertexes = SharedVertexesUtility.GetSharedIndexesWithPositions(points);

			ToMesh();
			Refresh();
		}

		/// <summary>
		/// Clear all mesh attributes and reinitialize with new positions and face collections.
		/// </summary>
		/// <param name="vertexes">Vertex positions array.</param>
		/// <param name="faces">Faces array.</param>
		public void RebuildWithPositionsAndFaces(IEnumerable<Vector3> vertexes, IEnumerable<Face> faces)
		{
			if (vertexes == null)
				throw new ArgumentNullException("vertexes");

			Clear();
			m_Positions = vertexes.ToArray();
			m_Faces = faces.ToArray();
			m_SharedVertexes = SharedVertexesUtility.GetSharedIndexesWithPositions(m_Positions);
			ToMesh();
			Refresh();
		}

		/// <summary>
		/// Wraps ToMesh and Refresh in a single call.
		/// </summary>
		/// <seealso cref="ToMesh"/>
		/// <seealso cref="Refresh"/>
		public void Rebuild()
		{
			ToMesh();
			Refresh();
		}

		/// <summary>
		/// Rebuild the mesh positions and submeshes. If vertex count matches new positions array the existing attributes are kept, otherwise the mesh is cleared. UV2 is the exception, it is always cleared.
		/// </summary>
		/// <param name="preferredTopology">Triangles and Quads are supported.</param>
		public void ToMesh(MeshTopology preferredTopology = MeshTopology.Triangles)
		{
			Mesh m = mesh;

			// if the mesh vertex count hasn't been modified, we can keep most of the mesh elements around
			if (m != null && m.vertexCount == m_Positions.Length)
				m = mesh;
			else if (m == null)
				m = new Mesh();
			else
				m.Clear();

			m.vertices = m_Positions;
			m.uv2 = null;

			Submesh[] submeshes = Submesh.GetSubmeshes(facesInternal, preferredTopology);
			m.subMeshCount = submeshes.Length;

			for (int i = 0; i < m.subMeshCount; i++)
				m.SetIndices(submeshes[i].m_Indexes, submeshes[i].m_Topology, i, false);

			m.name = string.Format("pb_Mesh{0}", id);

			GetComponent<MeshFilter>().sharedMesh = m;
			GetComponent<MeshRenderer>().sharedMaterials = submeshes.Select(x => x.m_Material).ToArray();
		}

		/// <summary>
		/// Deep copy the mesh attribute arrays back to itself. Useful when copy/paste creates duplicate references.
		/// </summary>
		internal void MakeUnique()
		{
			// deep copy arrays of reference types
			sharedVertexes = sharedVertexesInternal;
			SetSharedTextures(sharedTextureLookup);
			faces = faces.Select(x => new Face(x));

			// set a new UnityEngine.Mesh instance
			mesh = new Mesh();

			ToMesh();
			Refresh();
		}

		/// <summary>
		/// Copy mesh data from another mesh to self.
		/// </summary>
		/// <param name="other"></param>
		public void CopyFrom(ProBuilderMesh other)
		{
			if (other == null)
				throw new ArgumentNullException("other");

			Clear();
			positions = other.positions;
			sharedVertexes = other.sharedVertexesInternal;
			SetSharedTextures(other.sharedTextureLookup);
			faces = other.faces.Select(x => new Face(x));

			List<Vector4> uvs = new List<Vector4>();

			for (var i = 0; i < k_UVChannelCount; i++)
			{
				other.GetUVs(1, uvs);
				SetUVs(1, uvs);
			}

			tangents = other.tangents;
			colors = other.colors;
			userCollisions = other.userCollisions;
			selectable = other.selectable;
			unwrapParameters = new UnwrapParameters(other.unwrapParameters);
		}

		/// <summary>
		/// Recalculates mesh attributes: normals, collisions, UVs, tangents, and colors.
		/// </summary>
		/// <param name="mask">
		/// Optionally pass a mask to define what components are updated (UV and collisions are expensive to rebuild, and can usually be deferred til completion of task).
		/// </param>
		public void Refresh(RefreshMask mask = RefreshMask.All)
		{
			// Mesh
			if ((mask & RefreshMask.UV) > 0)
				RefreshUV();

			if ((mask & RefreshMask.Colors) > 0)
				RefreshColors();

			if ((mask & RefreshMask.Normals) > 0)
				RefreshNormals();

			if ((mask & RefreshMask.Tangents) > 0)
				RefreshTangents();

			if ((mask & RefreshMask.Collisions) > 0)
				RefreshCollisions();
		}

		void RefreshCollisions()
		{
			Mesh m = mesh;

			m.RecalculateBounds();

			if (!userCollisions && GetComponent<Collider>())
			{
				foreach (Collider c in gameObject.GetComponents<Collider>())
				{
					System.Type t = c.GetType();

					if (t == typeof(BoxCollider))
					{
						((BoxCollider)c).center = m.bounds.center;
						((BoxCollider)c).size = m.bounds.size;
					}
					else if (t == typeof(SphereCollider))
					{
						((SphereCollider)c).center = m.bounds.center;
						((SphereCollider)c).radius = Math.LargestValue(m.bounds.extents);
					}
					else if (t == typeof(CapsuleCollider))
					{
						((CapsuleCollider)c).center = m.bounds.center;
						Vector2 xy = new Vector2(m.bounds.extents.x, m.bounds.extents.z);
						((CapsuleCollider)c).radius = Math.LargestValue(xy);
						((CapsuleCollider)c).height = m.bounds.size.y;
					}
					else if (t == typeof(WheelCollider))
					{
						((WheelCollider)c).center = m.bounds.center;
						((WheelCollider)c).radius = Math.LargestValue(m.bounds.extents);
					}
					else if (t == typeof(MeshCollider))
					{
						gameObject.GetComponent<MeshCollider>().sharedMesh = null; // this is stupid.
						gameObject.GetComponent<MeshCollider>().sharedMesh = m;
					}
				}
			}
		}

		/// <summary>
		/// Returns a new unused texture group id.
		/// Will be greater than or equal to i.
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		internal int GetUnusedTextureGroup(int i = 1)
		{
			while (System.Array.Exists(facesInternal, element => element.textureGroup == i))
				i++;

			return i;
		}

		/// <summary>
		/// Returns a new unused element group.
		/// Will be greater than or equal to i.
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		internal int UnusedElementGroup(int i = 1)
		{
			while (System.Array.Exists(facesInternal, element => element.elementGroup == i))
				i++;

			return i;
		}

		/// <summary>
		/// Re-project AutoUV faces and re-assign ManualUV to mesh.uv channel.
		/// </summary>
		void RefreshUV()
		{
			RefreshUV(facesInternal);
		}

		/// <summary>
		/// Re-project AutoUV faces and re-assign ManualUV to mesh.uv channel.
		/// </summary>
		/// <param name="facesToRefresh"></param>
		internal void RefreshUV(IEnumerable<Face> facesToRefresh)
		{
			Vector2[] oldUvs = mesh.uv;
			Vector2[] newUVs;

			// thanks to the upgrade path, this is necessary.  maybe someday remove it.
			if (m_Textures0 != null && m_Textures0.Length == vertexCount)
			{
				newUVs = m_Textures0;
			}
			else
			{
				if (oldUvs != null && oldUvs.Length == vertexCount)
				{
					newUVs = oldUvs;
				}
				else
				{
					foreach (Face f in this.facesInternal)
						f.manualUV = false;

					// this necessitates rebuilding ALL the face uvs, so make sure we do that.
					facesToRefresh = this.facesInternal;

					newUVs = new Vector2[vertexCount];
				}
			}

			int n = -2;
			var textureGroups = new Dictionary<int, List<Face>>();
			bool anyWorldSpace = false;
			List<Face> group;

			foreach (Face f in facesToRefresh)
			{
				if (f.uv.useWorldSpace)
					anyWorldSpace = true;

				if (f == null || f.manualUV)
					continue;

				if (f.textureGroup > 0 && textureGroups.TryGetValue(f.textureGroup, out group))
					group.Add(f);
				else
					textureGroups.Add(f.textureGroup > 0 ? f.textureGroup : n--, new List<Face>() { f });
			}

			// Add any non-selected faces in texture groups to the update list
			if (this.facesInternal.Length != facesToRefresh.Count())
			{
				foreach (Face f in this.facesInternal)
				{
					if (f.manualUV)
						continue;

					if (textureGroups.ContainsKey(f.textureGroup) && !textureGroups[f.textureGroup].Contains(f))
						textureGroups[f.textureGroup].Add(f);
				}
			}

			n = 0;

			Vector3[] world = anyWorldSpace ? this.VertexesInWorldSpace() : null;

			foreach (KeyValuePair<int, List<Face>> kvp in textureGroups)
			{
				Vector3 nrm;
				int[] indexes = kvp.Value.SelectMany(x => x.distinctIndexesInternal).ToArray();

				if (kvp.Value.Count > 1)
					nrm = Projection.FindBestPlane(m_Positions, indexes).normal;
				else
					nrm = Math.Normal(this, kvp.Value[0]);

				if (kvp.Value[0].uv.useWorldSpace)
					UnwrappingUtility.PlanarMap2(world, newUVs, indexes, kvp.Value[0].uv, transform.TransformDirection(nrm));
				else
					UnwrappingUtility.PlanarMap2(positionsInternal, newUVs, indexes, kvp.Value[0].uv, nrm);
			}

			m_Textures0 = newUVs;
			mesh.uv = newUVs;

			if (HasArrays(MeshArrays.Texture2))
				mesh.SetUVs(2, m_Textures2);
			if (HasArrays(MeshArrays.Texture3))
				mesh.SetUVs(3, m_Textures3);
		}

		void RefreshColors()
		{
			Mesh m = GetComponent<MeshFilter>().sharedMesh;
			m.colors = m_Colors;
		}

		/// <summary>
		/// Set the vertex colors for a @"UnityEngine.ProBuilder.Face".
		/// </summary>
		/// <param name="face">The target face.</param>
		/// <param name="color">The color to set this face's referenced vertexes to.</param>
		public void SetFaceColor(Face face, Color color)
		{
			if (face == null)
				throw new ArgumentNullException("face");

			if (m_Colors == null)
				m_Colors = ArrayUtility.Fill<Color>(Color.white, vertexCount);

			foreach (int i in face.distinctIndexesInternal)
				m_Colors[i] = color;
		}

		void RefreshNormals()
		{
			GetComponent<MeshFilter>().sharedMesh.normals = CalculateNormals();
		}

		void RefreshTangents()
		{
			Mesh m = GetComponent<MeshFilter>().sharedMesh;

			if (m_Tangents != null && m_Tangents.Length == vertexCount)
				m.tangents = m_Tangents;
			else
				MeshUtility.GenerateTangent(m);
		}

		/// <summary>
		/// Calculate mesh normals without taking into account smoothing groups.
		/// </summary>
		/// <returns>A new array of the vertex normals.</returns>
		/// <seealso cref="CalculateNormals"/>
		public Vector3[] CalculateHardNormals()
		{
			Vector3[] perTriangleNormal = new Vector3[vertexCount];
			Vector3[] vertexes = positionsInternal;
			Vector3[] normals = new Vector3[vertexCount];
			int[] perTriangleAvg = new int[vertexCount];
			Face[] fces = facesInternal;

			for (int faceIndex = 0, fc = fces.Length; faceIndex < fc; faceIndex++)
			{
				int[] indexes = fces[faceIndex].indexesInternal;

				for (var tri = 0; tri < indexes.Length; tri += 3)
				{
					int a = indexes[tri], b = indexes[tri + 1], c = indexes[tri + 2];

					Vector3 cross = Math.Normal(vertexes[a], vertexes[b], vertexes[c]);
					cross.Normalize();

					perTriangleNormal[a].x += cross.x;
					perTriangleNormal[b].x += cross.x;
					perTriangleNormal[c].x += cross.x;

					perTriangleNormal[a].y += cross.y;
					perTriangleNormal[b].y += cross.y;
					perTriangleNormal[c].y += cross.y;

					perTriangleNormal[a].z += cross.z;
					perTriangleNormal[b].z += cross.z;
					perTriangleNormal[c].z += cross.z;

					perTriangleAvg[a]++;
					perTriangleAvg[b]++;
					perTriangleAvg[c]++;
				}
			}

			for (var i = 0; i < vertexCount; i++)
			{
				normals[i].x = perTriangleNormal[i].x / perTriangleAvg[i];
				normals[i].y = perTriangleNormal[i].y / perTriangleAvg[i];
				normals[i].z = perTriangleNormal[i].z / perTriangleAvg[i];
			}

			return normals;
		}

		/// <summary>
		/// Calculates the normals for a mesh, taking into account smoothing groups.
		/// </summary>
		/// <returns>A Vector3 array of the mesh normals</returns>
		public Vector3[] CalculateNormals()
		{
			Vector3[] normals = CalculateHardNormals();

			// average the soft edge faces
			int vc = vertexCount;
			int[] smoothGroup = new int[vc];
			SharedVertex[] si = sharedVertexesInternal;
			Face[] fcs = facesInternal;
			int smoothGroupMax = 24;

			// Create a lookup of each triangles smoothing group.
			foreach (var face in fcs)
			{
				foreach (int tri in face.distinctIndexesInternal)
				{
					smoothGroup[tri] = face.smoothingGroup;

					if (face.smoothingGroup >= smoothGroupMax)
						smoothGroupMax = face.smoothingGroup + 1;
				}
			}

			Vector3[] averages = new Vector3[smoothGroupMax];
			float[] counts = new float[smoothGroupMax];

			// For each sharedIndexes group (individual vertex), find vertexes that are in the same smoothing
			// group and average their normals.
			for (var i = 0; i < si.Length; i++)
			{
				for (var n = 0; n < smoothGroupMax; n++)
				{
					averages[n].x = 0f;
					averages[n].y = 0f;
					averages[n].z = 0f;
					counts[n] = 0f;
				}

				for (var n = 0; n < si[i].Count; n++)
				{
					int index = si[i][n];
					int group = smoothGroup[index];

					// Ideally this should only continue on group == NONE, but historically negative values have also
					// been treated as no smoothing.
					if (group <= Smoothing.smoothingGroupNone ||
						(group > Smoothing.smoothRangeMax && group < Smoothing.hardRangeMax))
						continue;

					averages[group].x += normals[index].x;
					averages[group].y += normals[index].y;
					averages[group].z += normals[index].z;
					counts[group] += 1f;
				}

				for (int n = 0; n < si[i].Count; n++)
				{
					int index = si[i][n];
					int group = smoothGroup[index];

					if (group <= Smoothing.smoothingGroupNone ||
						(group > Smoothing.smoothRangeMax && group < Smoothing.hardRangeMax))
						continue;

					normals[index].x = averages[group].x / counts[group];
					normals[index].y = averages[group].y / counts[group];
					normals[index].z = averages[group].z / counts[group];

					normals[index].Normalize();
				}

			}

			return normals;
		}

		/// <summary>
		/// Find the index of a vertex index (triangle) in an IntArray[]. The index returned is called the common index, or shared index in some cases.
		/// </summary>
		/// <remarks>Aids in removing duplicate vertex indexes.</remarks>
		/// <returns>The common (or shared) index.</returns>
		public int GetSharedVertexHandle(int vertex)
		{
			int res;

			if (m_SharedVertexLookup.TryGetValue(vertex, out res))
				return res;

			for (int i = 0; i < m_SharedVertexes.Length; i++)
			{
				for(int n = 0, c = m_SharedVertexes[i].Count; i < c; i++)
					if (m_SharedVertexes[i][n] == vertex)
						return i;
			}

			return -1;
		}

		public HashSet<int> GetSharedVertexHandles(IEnumerable<int> vertexes)
		{
			var lookup = sharedVertexLookup;
			HashSet<int> common = new HashSet<int>();
			foreach (var i in vertexes)
				common.Add(lookup[i]);
			return common;
		}

		public List<int> GetCoincidentVertexes(IEnumerable<int> vertexes)
		{
			if (vertexes == null)
                throw new ArgumentNullException("vertexes");

			List<int> shared = new List<int>();
			GetCoincidentVertexes(vertexes, shared);
			return shared;
		}

		public void GetCoincidentVertexes(IEnumerable<int> vertexes, List<int> coincident)
		{
			if (vertexes == null)
                throw new ArgumentNullException("vertexes");

			if (coincident == null)
                throw new ArgumentNullException("coincident");

			HashSet<int> used = new HashSet<int>();
			coincident.Clear();
			var lookup = sharedVertexLookup;

			foreach (var v in vertexes)
			{
				var common = lookup[v];

				if (used.Add(common))
					coincident.AddRange(m_SharedVertexes[common]);
			}
		}

		public void GetCoincidentVertexes(int vertex, List<int> coincident)
		{
			if (vertex < 0 || vertex >= m_SharedVertexes.Length)
                throw new ArgumentOutOfRangeException("vertex");

			if (coincident == null)
                throw new ArgumentNullException("coincident");

			coincident.Clear();
			coincident.AddRange(m_SharedVertexes[sharedVertexLookup[vertex]]);
		}

		public void SetVertexesCoincident(IEnumerable<int> vertexes)
		{
			var lookup = sharedVertexLookup;
			int index = lookup.Count;
			foreach (var v in vertexes)
				lookup[v] = index;
			SetSharedVertexes(lookup);
		}

		public void SetTexturesCoincident(IEnumerable<int> vertexes)
		{
			var lookup = sharedTextureLookup;
			int index = lookup.Count;
			foreach (var v in vertexes)
				lookup[v] = index;
			SetSharedTextures(lookup);
		}

		public void AddToSharedVertex(int sharedVertexHandle, int vertex)
		{
			if(sharedVertexHandle < 0 || sharedVertexHandle >= m_SharedVertexes.Length)
				throw new ArgumentOutOfRangeException("sharedVertexHandle");

			m_SharedVertexes[sharedVertexHandle].Add(vertex);
		}

		public void RemoveFromSharedVertexes(IEnumerable<int> vertexes)
		{
			var lookup = sharedVertexLookup;
			foreach (var v in vertexes)
				lookup.Remove(v);
			SetSharedVertexes(lookup);
		}

		public void RemoveFromSharedTextures(IEnumerable<int> vertexes)
		{
			var lookup = sharedTextureLookup;
			foreach (var v in vertexes)
				lookup.Remove(v);
			SetSharedTextures(lookup);
		}
	}
}

