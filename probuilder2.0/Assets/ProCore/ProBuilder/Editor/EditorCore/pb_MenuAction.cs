﻿using UnityEngine;
using UnityEditor;
using System.Collections;
using ProBuilder2.Interface;
using ProBuilder2.Common;

namespace ProBuilder2.EditorCommon
{
	/**
	 *	Connects a GUI button to an action.
	 */
	[System.Serializable]
	public abstract class pb_MenuAction
	{
		protected const char CMD_SUPER 	= pb_Constant.CMD_SUPER;
		protected const char CMD_SHIFT 	= pb_Constant.CMD_SHIFT;
		protected const char CMD_OPTION = pb_Constant.CMD_OPTION;
		protected const char CMD_ALT 	= pb_Constant.CMD_ALT;
		protected const char CMD_DELETE = pb_Constant.CMD_DELETE;

		public delegate void SettingsDelegate();

		public static pb_Object[] selection 
		{
			get
			{
				return pbUtil.GetComponents<pb_Object>(Selection.transforms);
			}
		}

		protected static GUIStyle _buttonStyle = null;
		protected static GUIStyle buttonStyle
		{
			get
			{
				if(_buttonStyle == null)
				{
					_buttonStyle = new GUIStyle();
					_buttonStyle.border = new RectOffset(1,1,1,1);
					_buttonStyle.alignment = TextAnchor.MiddleCenter;
					_buttonStyle.normal.background = pb_IconUtility.GetIcon("Button_Normal");
					_buttonStyle.hover.background = pb_IconUtility.GetIcon("Button_Hover");
					_buttonStyle.active.background = pb_IconUtility.GetIcon("Button_Pressed");
					_buttonStyle.margin = new RectOffset(4,4,4,4);
					_buttonStyle.padding = new RectOffset(4,4,4,4);
				}
				return _buttonStyle;
			}
		}

		protected Texture2D _desaturatedIcon = null;
		protected Texture2D desaturatedIcon
		{
			get
			{
				if(_desaturatedIcon == null)
				{
					if(icon == null)
						return null;

					_desaturatedIcon = pb_IconUtility.GetIcon(icon.name + "_disabled");

					// @todo
					// if(!_desaturatedIcon)
					// {
					// 	string path = AssetDatabase.GetAssetPath(icon);
					// 	TextureImporter imp = (TextureImporter) AssetImporter.GetAtPath( path );

					// 	if(!imp)
					// 	{
					// 		Debug.Log("Couldn't find importer : " + icon);
					// 		return null;
					// 	}

					// 	imp.isReadable = true;
					// 	imp.SaveAndReimport();

					// 	Color32[] px = icon.GetPixels32();

					// 	imp.isReadable = false;
					// 	imp.SaveAndReimport();

					// 	int gray = 0;

					// 	for(int i = 0; i < px.Length; i++)
					// 	{
					// 		gray = (System.Math.Min(px[i].r, System.Math.Min(px[i].g, px[i].b)) + System.Math.Max(px[i].r, System.Math.Max(px[i].g, px[i].b))) / 2;
					// 		px[i].r = (byte) gray;
					// 		px[i].g = (byte) gray;
					// 		px[i].b = (byte) gray;
					// 	}

					// 	_desaturatedIcon = new Texture2D(icon.width, icon.height);
					// 	_desaturatedIcon.hideFlags = HideFlags.HideAndDontSave;
					// 	_desaturatedIcon.SetPixels32(px);
					// 	_desaturatedIcon.Apply();

					// 	byte[] bytes = _desaturatedIcon.EncodeToPNG();
					// 	System.IO.File.WriteAllBytes(path.Replace(".png", "_disabled.png"), bytes);
					// }
				}

				return _desaturatedIcon;
			}
		}

		public abstract pb_IconGroup group { get; }
		public abstract Texture2D icon { get; }
		public abstract pb_TooltipContent tooltip { get; }

		public virtual bool IsHidden() { return false; }
		public abstract bool IsEnabled();
		public virtual bool SettingsEnabled() { return false; }
		
		public abstract pb_ActionResult DoAction();
		public virtual void OnSettingsGUI() {}

		public bool DoButton(bool showOptions, ref Rect optionsRect)
		{
			bool wasEnabled = GUI.enabled;
			bool buttonEnabled = IsEnabled();
			
			GUI.enabled = buttonEnabled;

			GUI.backgroundColor = pb_IconGroupUtility.GetColor(group);

			if( GUILayout.Button(buttonEnabled || !desaturatedIcon ? icon : desaturatedIcon, buttonStyle) )
			{
				if(showOptions && SettingsEnabled())
					pb_MenuOption.Show(OnSettingsGUI);
				else
				{
					pb_ActionResult result = DoAction();
					pb_Editor_Utility.ShowNotification(result.notification);
				}
			}

			GUI.backgroundColor = Color.white;

			if(SettingsEnabled())
			{
				Rect r = GUILayoutUtility.GetLastRect();
				r.x = r.x + r.width - 18;
				r.y += 2;
				r.width = 17;
				r.height = 17;
				GUI.Label(r, pb_IconUtility.GetIcon("Options"));
				optionsRect = r;
				GUI.enabled = wasEnabled;
				return buttonEnabled;
			}
			else
			{
				GUI.enabled = wasEnabled;
				return false;
			}
		}

		/**
		 *	Get the rendered width of this GUI item.
		 */
		public Vector2 GetSize()
		{
			return buttonStyle.CalcSize( pb_GUI_Utility.TempGUIContent(null, null, icon) );
		}
	}
}
