using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder.UI;
using System.Linq;
using System.Collections.Generic;
using Parabox.STL;
using System.IO;
using UnityEngine.ProBuilder;
using UnityEditor.ProBuilder;
using EditorUtility = UnityEditor.EditorUtility;

namespace UnityEditor.ProBuilder.Actions
{
	sealed class ExportAsset : MenuAction
	{
		public override ToolbarGroup group { get { return ToolbarGroup.Export; } }
		public override Texture2D icon { get { return null; } }
		public override TooltipContent tooltip { get { return s_Tooltip; } }

		static readonly TooltipContent s_Tooltip = new TooltipContent
		(
			"Export Asset",
			"Export a Unity mesh asset file."
		);

		public override bool enabled
		{
			get { return MeshSelection.count > 0; }
		}

		public override bool hidden
		{
			get { return true; }
		}

		public override ActionResult DoAction()
		{
			var res = ExportWithFileDialog( MeshSelection.TopInternal() );
			Export.PingExportedModel(res);
			return new ActionResult(ActionResult.Status.Success, "Make Asset & Prefab");
		}

		/// <summary>
		/// Export meshes to a Unity asset.
		/// </summary>
		/// <param name="meshes"></param>
		/// <returns></returns>
		public static string ExportWithFileDialog(IEnumerable<ProBuilderMesh> meshes)
		{
			if(meshes == null || !meshes.Any())
				return "";

			string res = null;

			if(meshes.Count() < 2)
			{
				ProBuilderMesh first = meshes.FirstOrDefault();

				if(first == null)
					return null;

				string name = first.name;
				string path = UnityEditor.EditorUtility.SaveFilePanel("Export to Asset", "Assets", name, "prefab");

				if(string.IsNullOrEmpty(path))
					return null;

				string directory = Path.GetDirectoryName(path);
				name = Path.GetFileNameWithoutExtension(path);
				string meshPath = string.Format("{0}/{1}.asset", directory, first.mesh.name).Replace("\\", "/");
				string prefabPath = string.Format("{0}/{1}.prefab", directory, first.name).Replace("\\", "/");

				// If a file dialog was presented that means the user has already been asked to overwrite.
				if(File.Exists(meshPath))
					AssetDatabase.DeleteAsset(meshPath.Replace(Application.dataPath, "Assets"));

				if(File.Exists(prefabPath))
					AssetDatabase.DeleteAsset(prefabPath.Replace(Application.dataPath, "Assets"));

				res = DoExport(path, first);
			}
			else
			{
				string path = UnityEditor.EditorUtility.SaveFolderPanel("Export to Asset", "Assets", "");

				if(string.IsNullOrEmpty(path) || !Directory.Exists(path))
					return null;

				foreach(ProBuilderMesh pb in meshes)
					res = DoExport(string.Format("{0}/{1}.asset", path, pb.name), pb);
			}

			return res;
		}

		static string DoExport(string path, ProBuilderMesh pb)
		{
			string directory = Path.GetDirectoryName(path).Replace("\\", "/");
			string name = Path.GetFileNameWithoutExtension(path);
			string relativeDirectory = string.Format("Assets{0}", directory.Replace(Application.dataPath, ""));

			pb.ToMesh();
			pb.Refresh();
			pb.Optimize(true);

			string meshPath = AssetDatabase.GenerateUniqueAssetPath(string.Format("{0}/{1}.asset", relativeDirectory, pb.mesh.name));

			AssetDatabase.CreateAsset(pb.mesh, meshPath);

			pb.MakeUnique();

			Mesh meshAsset = (Mesh) AssetDatabase.LoadAssetAtPath(meshPath, typeof(Mesh));

			var go = Object.Instantiate(pb.gameObject);
			var dup = go.GetComponent<ProBuilderMesh>();
			var entity = go.GetComponent<Entity>();
			if(entity != null)
				Object.DestroyImmediate(entity);
			dup.preserveMeshAssetOnDestroy = true;
			Object.DestroyImmediate(dup);
			go.GetComponent<MeshFilter>().sharedMesh = meshAsset;
			string relativePrefabPath = string.Format("{0}/{1}.prefab", relativeDirectory, name);
			string prefabPath = AssetDatabase.GenerateUniqueAssetPath(relativePrefabPath);
#if UNITY_2018_3_OR_NEWER
			PrefabUtility.CreatePrefab(prefabPath, go);
#else
			PrefabUtility.CreatePrefab(prefabPath, go, ReplacePrefabOptions.Default);
#endif
			Object.DestroyImmediate(go);

			return meshPath;
		}
	}
}
