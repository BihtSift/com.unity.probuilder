using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ProBuilder.Core;

namespace ProBuilder.EditorCore
{
	/// <summary>
	/// Used to pop up the window on import.
	/// </summary>
	[InitializeOnLoad]
	static class pb_AboutWindowSetup
	{
		static pb_AboutWindowSetup()
		{
			EditorApplication.delayCall += () => { pb_AboutWindow.Init(false); };
		}
	}

	/// <summary>
	///	Changelog.txt file should follow this format:
	///		| # Product Name 2.1.0
	///		|
	///		| ## Features
	///		|
	///		| - All kinds of awesome stuff
	///		| - New flux capacitor design achieves time travel at lower velocities.
	///		| - Dark matter reactor recalibrated.
	///		|
	///		| ## Bug Fixes
	///		|
	///		| - No longer explodes when spacebar is pressed.
	///		| - Fix rolling issue in rickmeter.
	///		|
	///		| # Changes
	///		|
	///		| - Changed Blue to Red.
	///		| - Enter key now causes explosions.
	///	This path is relative to the PRODUCT_ROOT path.
	///	Note that your changelog may contain multiple entries.  Only the top-most
	///	entry will be displayed.
	/// </summary>
	class pb_AboutWindow : EditorWindow
	{
		GUIContent m_LearnContent = new GUIContent("Learn ProBuilder", "Documentation");
		GUIContent m_ForumLinkContent = new GUIContent("Support Forum", "ProCore Support Forum");
		GUIContent m_ContactContent = new GUIContent("Contact Us", "Send us an email!");
		GUIContent m_BannerContent = new GUIContent("", "ProBuilder Quick-Start Video Tutorials");

		const string k_VideoUrl = @"http://bit.ly/pbstarter";
		const string k_LearnUrl = @"http://procore3d.com/docs/probuilder";
		const string k_SupportUrl = @"http://www.procore3d.com/forum/";
		const string k_ContactEmailUrl = @"http://www.procore3d.com/about/";
		const float k_BannerWidth = 480f;
		const float k_BannerHeight = 270f;

		const string k_AboutWindowVersionPref = "ProBuilder_AboutWindowIdentifier";
		const string k_AboutPrefFormat = "M.m.ptb";

		internal const string k_FontRegular = "Asap-Regular.otf";
		internal const string k_FontMedium = "Asap-Medium.otf";

		// Use less contast-y white and black font colors for better readabililty
		public static readonly Color k_FontWhite = HexToColor(0xCECECE);
		public static readonly Color k_FontBlack = HexToColor(0x545454);
		public static readonly Color k_FontBlueNormal = HexToColor(0x00AAEF);
		public static readonly Color k_FontBlueHover = HexToColor(0x008BEF);

		string m_ProductName = pb_Constant.PRODUCT_NAME;
		pb_VersionInfo m_changeLogVersionInfo;
		string m_ChangeLogRichText = "";
		static bool m_CancelImportPopup = false;

		internal static GUIStyle bannerStyle,
								header1Style,
								versionInfoStyle,
								linkStyle,
								separatorStyle,
								changelogStyle,
								changelogTextStyle;

		Vector2 scroll = Vector2.zero;

		/// <summary>
		/// Cancel the About window popup on asset import. Used by PackageImporter through reflection to prevent window
		/// from popping up when the EditorCore DLL is about to be disabled.
		/// </summary>
		public static void CancelImportPopup()
		{
			m_CancelImportPopup = true;
		}

		/// <summary>
		/// Return true if Init took place, false if not.
		/// </summary>
		/// <param name="fromMenu"></param>
		/// <returns></returns>
		public static bool Init (bool fromMenu)
		{
			// added as a way for the upm converter check to cancel the about popup when the new editor dll is going to
			// be immediately disabled. exiting here allows the popup to run when the editor is re-enabled (ie, prefs
			// doesn't set the version to the newly imported editorcore).
			if (m_CancelImportPopup)
			{
				m_CancelImportPopup = false;
				return false;
			}

			if(fromMenu || pb_PreferencesInternal.GetString(k_AboutWindowVersionPref) != pb_Version.Current.ToString(k_AboutPrefFormat))
			{
				pb_PreferencesInternal.SetString(k_AboutWindowVersionPref, pb_Version.Current.ToString(k_AboutPrefFormat), pb_PreferenceLocation.Global);
				GetWindow(typeof(pb_AboutWindow), true, pb_Constant.PRODUCT_NAME, true).ShowUtility();
				return true;
			}
			else
			{
				return false;
			}
		}

		static Color HexToColor(uint x)
		{
			return new Color( 	((x >> 16) & 0xFF) / 255f,
								((x >> 8) & 0xFF) / 255f,
								(x & 0xFF) / 255f,
								1f);
		}

		public static void InitGuiStyles()
		{
			bannerStyle = new GUIStyle()
			{
				// RectOffset(left, right, top, bottom)
				margin = new RectOffset(12, 12, 12, 12),
				normal = new GUIStyleState() {
					background = pb_FileUtil.LoadInternalAsset<Texture2D>("About/Images/Banner_Normal.png")
				},
				hover = new GUIStyleState() {
					background = pb_FileUtil.LoadInternalAsset<Texture2D>("About/Images/Banner_Hover.png")
				},
			};

			header1Style = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				alignment = TextAnchor.MiddleCenter,
				fontSize = 24,
				// fontStyle = FontStyle.Bold,
				font = pb_FileUtil.LoadInternalAsset<Font>("About/Font/" + k_FontMedium),
				normal = new GUIStyleState() { textColor = EditorGUIUtility.isProSkin ? k_FontWhite : k_FontBlack }
			};

			versionInfoStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				fontSize = 14,
				font = pb_FileUtil.LoadInternalAsset<Font>("About/Font/" + k_FontRegular),
				normal = new GUIStyleState() { textColor = EditorGUIUtility.isProSkin ? k_FontWhite : k_FontBlack }
			};

			linkStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				alignment = TextAnchor.MiddleCenter,
				fontSize = 16,
				font = pb_FileUtil.LoadInternalAsset<Font>("About/Font/" + k_FontRegular),
				normal = new GUIStyleState() {
					textColor = k_FontBlueNormal,
					background = pb_FileUtil.LoadInternalAsset<Texture2D>(
						string.Format("About/Images/ScrollBackground_{0}.png", EditorGUIUtility.isProSkin ? "Pro" : "Light"))
				},
				hover = new GUIStyleState() {
					textColor = k_FontBlueHover,
					background = pb_FileUtil.LoadInternalAsset<Texture2D>(
						string.Format("About/Images/ScrollBackground_{0}.png", EditorGUIUtility.isProSkin ? "Pro" : "Light"))
				}
			};

			separatorStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				alignment = TextAnchor.MiddleCenter,
				fontSize = 16,
				font = pb_FileUtil.LoadInternalAsset<Font>("About/Font/" + k_FontRegular),
				normal = new GUIStyleState() { textColor = EditorGUIUtility.isProSkin ? k_FontWhite : k_FontBlack }
			};

			changelogStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				font = pb_FileUtil.LoadInternalAsset<Font>("About/Font/" + k_FontRegular),
				richText = true,
				normal = new GUIStyleState() { background = pb_FileUtil.LoadInternalAsset<Texture2D>(
					string.Format("About/Images/ScrollBackground_{0}.png",
						EditorGUIUtility.isProSkin ? "Pro" : "Light"))
				}
			};

			changelogTextStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
				font = pb_FileUtil.LoadInternalAsset<Font>("About/Font/" + k_FontRegular),
				fontSize = 14,
				normal = new GUIStyleState() { textColor = EditorGUIUtility.isProSkin ? k_FontWhite : k_FontBlack },
				richText = true,
				wordWrap = true
			};
		}

		public void OnEnable()
		{
			InitGuiStyles();

			Texture2D banner = bannerStyle.normal.background;

			if(banner == null)
			{
				pb_Log.Warning("Could not load About window resources");
				EditorApplication.delayCall += Close;
			}
			else
			{
				bannerStyle.fixedWidth = k_BannerWidth; // banner.width;
				bannerStyle.fixedHeight = k_BannerHeight; // banner.height;

				this.wantsMouseMove = true;

				this.minSize = new Vector2(k_BannerWidth + 24, k_BannerHeight * 2.5f);
				this.maxSize = new Vector2(k_BannerWidth + 24, k_BannerHeight * 2.5f);

				if(!m_ProductName.Contains("Basic"))
					m_ProductName = "ProBuilder Advanced";
			}

			TextAsset changeText = pb_FileUtil.LoadInternalAsset<TextAsset>("About/changelog.txt");

			string raw = changeText != null ? changeText.text : "";

			if (!string.IsNullOrEmpty(raw))
			{
				pb_VersionUtil.FormatChangelog(raw, out m_changeLogVersionInfo, out m_ChangeLogRichText);
				if(!pb_Version.Current.Equals(m_changeLogVersionInfo))
					pb_Log.Info("Changelog version does not match internal version. {0} != {1}",
						m_changeLogVersionInfo.ToString(k_AboutPrefFormat),
						pb_Version.Current.ToString(k_AboutPrefFormat));
			}
		}

		void OnGUI()
		{
			if (bannerStyle.normal.background == null)
			{
				GUILayout.Label("Could Not Load About Window", EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
				return;
			}

			Vector2 mousePosition = Event.current.mousePosition;

			if( GUILayout.Button(m_BannerContent, bannerStyle) )
				Application.OpenURL(k_VideoUrl);

			if(GUILayoutUtility.GetLastRect().Contains(mousePosition))
				Repaint();

			GUILayout.BeginVertical(changelogStyle);

			GUILayout.Label(m_ProductName, header1Style);

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

				if(GUILayout.Button(m_LearnContent, linkStyle))
					Application.OpenURL(k_LearnUrl);

				GUILayout.Label("|", separatorStyle);

				if(GUILayout.Button(m_ForumLinkContent, linkStyle))
					Application.OpenURL(k_SupportUrl);

				GUILayout.Label("|", separatorStyle);

				if(GUILayout.Button(m_ContactContent, linkStyle))
					Application.OpenURL(k_ContactEmailUrl);

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			if(GUILayoutUtility.GetLastRect().Contains(mousePosition))
				Repaint();

			GUILayout.EndVertical();

			// always bold the first line (cause it's the version info stuff)
			scroll = EditorGUILayout.BeginScrollView(scroll, changelogStyle);
			GUILayout.Label(string.Format("Version: {0}", m_changeLogVersionInfo.ToString("M.m.p")), versionInfoStyle);
			GUILayout.Label("\n" + m_ChangeLogRichText, changelogTextStyle);
			EditorGUILayout.EndScrollView();

			GUILayout.Label(pb_Version.Current.ToString());
		}

		/// <summary>
		/// Draw a horizontal line across the screen and update the guilayout.
		/// </summary>
		void HorizontalLine()
		{
			Rect r = GUILayoutUtility.GetLastRect();
			Color og = GUI.backgroundColor;
			GUI.backgroundColor = Color.black;
			GUI.Box(new Rect(0f, r.y + r.height + 2, Screen.width, 2f), "");
			GUI.backgroundColor = og;

			GUILayout.Space(6);
		}
	}
}