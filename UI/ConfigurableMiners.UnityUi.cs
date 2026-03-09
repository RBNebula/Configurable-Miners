using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RBN.Modlib.Game;
using RBN.Modlib.Persistence;
using RBN.Modlib.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConfigurableMiners;
public sealed partial class ConfigurableMiners
{
	private static readonly bool UseUnityUi = true;

	private GameObject? _unityUiRoot;
	private Text? _unityTitleText;
	private Text? _unityHeaderTargetText;
	private Text? _unityHeaderCoordsText;
	private const float UiLeftLabelWidth = 170f;
	private const float UiStateButtonWidth = 120f;
	private const float UiSecondaryLabelWidth = 130f;
	private const float UiRowSpacing = 8f;

	private Slider? _unitySpawnRateSlider;
	private InputField? _unitySpawnRateInput;
	private Slider? _unitySpawnProbabilitySlider;
	private InputField? _unitySpawnProbabilityInput;
	private Slider? _unityAreaRadiusSlider;
	private InputField? _unityAreaRadiusInput;

	private Button? _unityOverrideButton;
	private Text? _unityOverrideButtonText;
	private Button? _unityAllowGemsButton;
	private Text? _unityAllowGemsButtonText;
	private Button? _unityPolishedGemsButton;
	private Text? _unityPolishedGemsButtonText;
	private Button? _unityAllowGeodesButton;
	private Text? _unityAllowGeodesButtonText;
	private Button? _unityPolishedGeodesButton;
	private Text? _unityPolishedGeodesButtonText;
	private Text? _unityGroupText;
	private Text? _unityConfigButtonText;
	private Text? _unityGroupConfigButtonText;
	private GameObject? _unityPresetsSection;
	private RectTransform? _unityPresetsContent;
	private Text? _unityMinerTuningSectionTitle;
	private RectTransform? _unityMinerTuningContent;
	private GameObject? _unityConfigOverlay;
	private RectTransform? _unityConfigOverlayContent;
	private GameObject? _unityGroupConfigOverlay;
	private RectTransform? _unityGroupConfigOverlayContent;
	private Button? _unityConfigHoverCenterButton;
	private Button? _unityConfigHoverTopButton;
	private Button? _unityConfigHoverBottomButton;
	private bool _unityConfigOpen;
	private bool _unityGroupConfigOpen;

	private readonly Toggle?[] _unitySlotEnabledToggles = new Toggle?[4];
	private readonly Dropdown?[] _unitySlotDropdowns = new Dropdown?[4];
	private readonly InputField?[] _unitySlotPercentInputs = new InputField?[4];
	private readonly Toggle?[] _unitySlotLockedToggles = new Toggle?[4];
	private readonly List<Text> _unityPresetNameLabels = new();
	private readonly List<OutputOption>[] _unitySlotOptionMaps =
	{
		new List<OutputOption>(),
		new List<OutputOption>(),
		new List<OutputOption>(),
		new List<OutputOption>()
	};

	private bool _unityUiSyncing;
	private Font? _unityFont;
	private static Sprite? _unityUiSolidSprite;
	private static Sprite? _unityUiCircleSprite;
	private readonly List<UnityConfigSliderBinding> _unityConfigSliderBindings = new();
	private readonly List<UnityConfigToggleBinding> _unityConfigToggleBindings = new();
	private readonly List<UnityConfigGroupNameBinding> _unityConfigGroupNameBindings = new();
	private readonly List<UnityConfigPresetNameBinding> _unityConfigPresetNameBindings = new();
	private readonly List<UnityGroupToggleRowBinding> _unityGroupToggleRows = new();

	private sealed class UnityConfigSliderBinding
	{
		public Slider slider = null!;
		public InputField input = null!;
		public Func<float> getter = null!;
		public string format = "0.##";
	}

	private sealed class UnityConfigToggleBinding
	{
		public Button button = null!;
		public Text label = null!;
		public Func<bool> getter = null!;
	}

	private sealed class UnityConfigGroupNameBinding
	{
		public int group;
		public Text label = null!;
		public InputField input = null!;
	}

	private sealed class UnityConfigPresetNameBinding
	{
		public int presetIndex;
		public Text label = null!;
		public InputField input = null!;
	}

	private sealed class UnityGroupToggleRowBinding
	{
		public int group;
		public Text label = null!;
	}

	private void SetUnityUiVisible(bool visible)
	{
		if (!UseUnityUi) return;
		EnsureUnityUiBuilt();
		if (_unityUiRoot != null)
		{
			_unityUiRoot.SetActive(visible);
		}
		if (!visible)
		{
			_unityConfigOpen = false;
			_unityGroupConfigOpen = false;
			RefreshUnityConfigOverlay();
		}
		if (visible)
		{
			RefreshUnityUi();
		}
	}

	private void DestroyUnityUi()
	{
		if (_unityUiRoot != null)
		{
			Destroy(_unityUiRoot);
			_unityUiRoot = null;
		}
	}

	private void EnsureUnityUiBuilt()
	{
		if (!UseUnityUi || _unityUiRoot != null) return;

		EnsureEventSystem();
		_unityFont ??= Resources.GetBuiltinResource<Font>("Arial.ttf");

		var root = new GameObject(
			"ConfigurableMiners_UnityUI",
			typeof(RectTransform),
			typeof(Canvas),
			typeof(CanvasScaler),
			typeof(GraphicRaycaster));
		DontDestroyOnLoad(root);

		var canvas = root.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 6000;

		var scaler = root.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		scaler.matchWidthOrHeight = 0.5f;

		var overlay = CreateImageRect(root.transform, "Overlay", new Color(0.01f, 0.02f, 0.04f, 0.76f));
		StretchToParent(overlay, 0f);

		var panel = CreateImageRect(overlay, "Window", new Color(0.06f, 0.09f, 0.14f, 0.95f));
		panel.anchorMin = new Vector2(0.5f, 0.5f);
		panel.anchorMax = new Vector2(0.5f, 0.5f);
		panel.pivot = new Vector2(0.5f, 0.5f);
		panel.sizeDelta = new Vector2(1420f, 780f);
		panel.anchoredPosition = Vector2.zero;
		var panelOutline = panel.gameObject.AddComponent<Outline>();
		panelOutline.effectColor = new Color(0.22f, 0.66f, 0.97f, 0.5f);
		panelOutline.effectDistance = new Vector2(1f, -1f);

		var content = CreateRectOnly(panel, "Content");
		content.anchorMin = Vector2.zero;
		content.anchorMax = Vector2.one;
		content.offsetMin = new Vector2(16f, 16f);
		content.offsetMax = new Vector2(-16f, -16f);
		var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
		contentLayout.padding = new RectOffset(8, 8, 8, 8);
		contentLayout.spacing = 8f;
		contentLayout.childControlHeight = true;
		contentLayout.childControlWidth = true;
		contentLayout.childForceExpandHeight = false;
		contentLayout.childForceExpandWidth = true;

		BuildHeader(content);

		var body = CreateRectOnly(content, "Body");
		var bodyElement = body.gameObject.AddComponent<LayoutElement>();
		bodyElement.flexibleHeight = 1f;
		bodyElement.minHeight = 520f;
		var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
		bodyLayout.spacing = 12f;
		bodyLayout.childControlHeight = true;
		bodyLayout.childControlWidth = true;
		bodyLayout.childForceExpandWidth = true;
		bodyLayout.childForceExpandHeight = true;

		var presetSection = CreateSectionShell(body, "Presets", 0.24f, minWidth: 260f, preferredWidth: 280f);
		_unityPresetsSection = presetSection.gameObject;
		_unityPresetsContent = CreateSectionScrollContent(presetSection, "PresetsScroll", minHeight: 500f);
		var tuningSection = CreateSectionShell(body, "Miner Tuning", 0.56f, minWidth: 540f, preferredWidth: 640f);
		var leftContent = CreateSectionContent(tuningSection);
		_unityMinerTuningContent = leftContent;
		var rightContent = CreateSection(body, "Actions", 0.20f, minWidth: 220f, preferredWidth: 230f);

		(_unitySpawnRateSlider, _unitySpawnRateInput) = CreateSliderInputRow(
			leftContent,
			"Spawn Rate (s)",
			0.01f,
			20f,
			() => _edit.spawnRate,
			v =>
			{
				_edit.spawnRate = v;
				_editDirty = true;
			},
			"0.###");

		(_unitySpawnProbabilitySlider, _unitySpawnProbabilityInput) = CreateSliderInputRow(
			leftContent,
			"Spawn Probability %",
			0f,
			100f,
			() => _edit.spawnProbability,
			v =>
			{
				_edit.spawnProbability = v;
				_editDirty = true;
			},
			"0.##");

		(_unityAreaRadiusSlider, _unityAreaRadiusInput) = CreateSliderInputRow(
			leftContent,
			"Apply Area Radius (m)",
			1f,
			250f,
			() => _areaApplyRadius,
			v =>
			{
				_areaApplyRadius = v;
				SaveSettingsToDisk();
			},
			"0.#");

		(_unityOverrideButton, _unityOverrideButtonText) = CreateStateButtonRow(leftContent, "Override Output", ToggleOverrideOutputs);
		((_unityAllowGemsButton, _unityAllowGemsButtonText), (_unityPolishedGemsButton, _unityPolishedGemsButtonText)) =
			CreatePairedStateButtonRow(leftContent, "Allow Gems", ToggleAllowGems, "Polished Gems", TogglePolishedGems);
		((_unityAllowGeodesButton, _unityAllowGeodesButtonText), (_unityPolishedGeodesButton, _unityPolishedGeodesButtonText)) =
			CreatePairedStateButtonRow(leftContent, "Allow Geodes", ToggleAllowGeodes, "Polished Geodes", TogglePolishedGeodes);

		BuildGroupSelector(leftContent);
		BuildSlotEditor(leftContent);
		RebuildUnityPresetEditor();
		BuildUnityConfigOverlay(tuningSection);
		BuildUnityGroupConfigOverlay(tuningSection);

		var actionColumn = CreateRectOnly(rightContent, "ActionColumn");
		var actionColumnLayout = actionColumn.gameObject.AddComponent<VerticalLayoutGroup>();
		actionColumnLayout.spacing = 6f;
		actionColumnLayout.childControlWidth = true;
		actionColumnLayout.childControlHeight = true;
		actionColumnLayout.childForceExpandWidth = true;
		actionColumnLayout.childForceExpandHeight = false;
		var actionColumnElement = actionColumn.gameObject.AddComponent<LayoutElement>();
		actionColumnElement.flexibleHeight = 1f;
		actionColumnElement.minHeight = 370f;

		CreateActionButton(actionColumn, "Apply", ApplySelectedConfig);
		CreateActionButton(actionColumn, "Apply Group", ApplyCurrentConfigToGroup);
		CreateActionButton(actionColumn, "Apply Area", ApplyCurrentConfigAroundPlayer);
		CreateActionButton(actionColumn, "Revert", RevertSelectedMiner);
		CreateActionButton(actionColumn, "Copy", CopySelectedConfigToClipboard);
		CreateActionButton(actionColumn, "Paste", PasteClipboardToSelectedConfig);
		CreateActionButton(actionColumn, "All On", () => ToggleAllMiners(true));
		CreateActionButton(actionColumn, "All Off", () => ToggleAllMiners(false));
		var groupConfigButton = CreateActionButton(actionColumn, "Group Config", UnityUiToggleGroupConfigOverlay);
		_unityGroupConfigButtonText = groupConfigButton.GetComponentInChildren<Text>();
		var configButton = CreateActionButton(actionColumn, "Config", UnityUiToggleConfigOverlay);
		_unityConfigButtonText = configButton.GetComponentInChildren<Text>();
		CreateActionButton(actionColumn, "Close", _closeFocusAndClose);

		_unityUiRoot = root;
		_unityConfigOpen = false;
		_unityGroupConfigOpen = false;
		RefreshUnityConfigOverlay();
		root.SetActive(false);
	}

	private void BuildHeader(Transform parent)
	{
		var row = CreateRectOnly(parent, "Header");
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 44f;
		var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		layout.spacing = 8f;
		layout.childAlignment = TextAnchor.MiddleCenter;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandHeight = false;
		layout.childForceExpandWidth = true;

		_unityHeaderTargetText = CreateTextLine(
			row,
			"Current Target: no miner found",
			13,
			FontStyle.Normal,
			TextAnchor.MiddleLeft,
			40f,
			new Color(0.8f, 0.91f, 0.99f, 0.95f));
		var leftInfoElement = _unityHeaderTargetText.gameObject.GetComponent<LayoutElement>();
		leftInfoElement.minWidth = 300f;
		leftInfoElement.flexibleWidth = 1f;

		_unityTitleText = CreateTextLine(row, "Configurable Miners", 24, FontStyle.Bold, TextAnchor.MiddleCenter, 40f, new Color(0.9f, 0.96f, 1f, 1f));
		_unityTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
		var titleElement = _unityTitleText.gameObject.GetComponent<LayoutElement>();
		titleElement.minWidth = 380f;
		titleElement.preferredWidth = 460f;
		titleElement.flexibleWidth = 0f;

		_unityHeaderCoordsText = CreateTextLine(
			row,
			"Pos: no miner found",
			13,
			FontStyle.Normal,
			TextAnchor.MiddleRight,
			40f,
			new Color(0.8f, 0.91f, 0.99f, 0.95f));
		var rightInfoElement = _unityHeaderCoordsText.gameObject.GetComponent<LayoutElement>();
		rightInfoElement.minWidth = 300f;
		rightInfoElement.flexibleWidth = 1f;
	}

	private RectTransform CreateScrollableSection(Transform parent, string title, float widthWeight, float minWidth = 320f, float preferredWidth = 0f)
	{
		return CreateSection(parent, title, widthWeight, minWidth, preferredWidth);
	}

	private RectTransform CreateSection(Transform parent, string title, float widthWeight, float minWidth = 320f, float preferredWidth = 0f)
	{
		var section = CreateSectionShell(parent, title, widthWeight, minWidth, preferredWidth);
		return CreateSectionContent(section);
	}

	private RectTransform CreateSectionContent(Transform section)
	{
		var content = CreateRectOnly(section, "Content");
		var contentElement = content.gameObject.AddComponent<LayoutElement>();
		contentElement.flexibleHeight = 1f;

		var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(6, 6, 6, 6);
		layout.spacing = 8f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		return content;
	}

	private RectTransform CreateSectionScrollContent(Transform section, string name, float minHeight = 0f)
	{
		var scrollGo = DefaultControls.CreateScrollView(new DefaultControls.Resources());
		scrollGo.name = name;
		scrollGo.transform.SetParent(section, false);
		var scrollRect = scrollGo.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;

		var scrollImage = scrollGo.GetComponent<Image>();
		if (scrollImage != null)
		{
			EnsureUnityUiImageSprite(scrollImage);
			scrollImage.color = new Color(0.08f, 0.12f, 0.18f, 0.94f);
		}

		var viewportImage = scrollGo.transform.Find("Viewport")?.GetComponent<Image>();
		if (viewportImage != null)
		{
			EnsureUnityUiImageSprite(viewportImage);
			viewportImage.color = new Color(0.07f, 0.11f, 0.17f, 0.96f);
		}

		var horizontalScrollbar = scrollGo.transform.Find("Scrollbar Horizontal");
		if (horizontalScrollbar != null)
		{
			horizontalScrollbar.gameObject.SetActive(false);
		}
		var verticalScrollbarImage = scrollGo.transform.Find("Scrollbar Vertical")?.GetComponent<Image>();
		if (verticalScrollbarImage != null)
		{
			EnsureUnityUiImageSprite(verticalScrollbarImage);
			verticalScrollbarImage.color = new Color(0.11f, 0.19f, 0.27f, 0.95f);
		}
		var verticalHandle = scrollGo.transform.Find("Scrollbar Vertical/Sliding Area/Handle")?.GetComponent<Image>();
		if (verticalHandle != null)
		{
			EnsureUnityUiImageSprite(verticalHandle);
			verticalHandle.color = new Color(0.30f, 0.66f, 0.93f, 0.95f);
		}

		var content = scrollGo.transform.Find("Viewport/Content")?.GetComponent<RectTransform>();
		if (content == null)
		{
			var fallback = CreateSectionContent(section);
			return fallback;
		}

		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.offsetMin = new Vector2(0f, 0f);
		content.offsetMax = new Vector2(0f, 0f);

		var contentLayout = content.gameObject.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
		contentLayout.padding = new RectOffset(6, 6, 6, 6);
		contentLayout.spacing = 8f;
		contentLayout.childControlWidth = true;
		contentLayout.childControlHeight = true;
		contentLayout.childForceExpandWidth = true;
		contentLayout.childForceExpandHeight = false;

		var fitter = content.gameObject.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		var scrollElement = scrollGo.GetComponent<LayoutElement>() ?? scrollGo.AddComponent<LayoutElement>();
		scrollElement.flexibleHeight = 1f;
		if (minHeight > 0f)
		{
			scrollElement.minHeight = minHeight;
		}

		return content;
	}

	private RectTransform CreateSectionShell(Transform parent, string title, float widthWeight, float minWidth = 320f, float preferredWidth = 0f)
	{
		var section = CreateImageRect(parent, title.Replace(" ", string.Empty) + "Section", new Color(0.09f, 0.13f, 0.19f, 0.95f));
		var sectionOutline = section.gameObject.AddComponent<Outline>();
		sectionOutline.effectColor = new Color(0.2f, 0.53f, 0.81f, 0.3f);
		sectionOutline.effectDistance = new Vector2(1f, -1f);

		var sectionElement = section.gameObject.AddComponent<LayoutElement>();
		sectionElement.flexibleWidth = widthWeight;
		sectionElement.minWidth = minWidth;
		if (preferredWidth > 0f)
		{
			sectionElement.preferredWidth = preferredWidth;
		}

		var sectionLayout = section.gameObject.AddComponent<VerticalLayoutGroup>();
		sectionLayout.padding = new RectOffset(12, 12, 10, 10);
		sectionLayout.spacing = 8f;
		sectionLayout.childControlWidth = true;
		sectionLayout.childControlHeight = true;
		sectionLayout.childForceExpandWidth = true;
		sectionLayout.childForceExpandHeight = false;

		var titleText = CreateTextLine(section, title, 17, FontStyle.Bold, TextAnchor.MiddleLeft, 28f, new Color(0.85f, 0.94f, 1f, 1f));
		if (string.Equals(title, "Miner Tuning", StringComparison.Ordinal))
		{
			_unityMinerTuningSectionTitle = titleText;
		}
		return section;
	}

	private void BuildUnityConfigOverlay(Transform section)
	{
		var overlay = CreateImageRect(section, "ConfigOverlay", new Color(0.08f, 0.12f, 0.18f, 1f));
		var overlayElement = overlay.gameObject.AddComponent<LayoutElement>();
		overlayElement.ignoreLayout = true;
		StretchToParent(overlay, 0f);
		overlay.SetAsLastSibling();

		var body = CreateRectOnly(overlay, "ConfigOverlayBody");
		StretchToParent(body, 8f);
		var bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
		bodyLayout.spacing = 8f;
		bodyLayout.childControlWidth = true;
		bodyLayout.childControlHeight = true;
		bodyLayout.childForceExpandWidth = true;
		bodyLayout.childForceExpandHeight = false;

		var title = CreateTextLine(body, "Config", 17, FontStyle.Bold, TextAnchor.MiddleLeft, 28f, new Color(0.9f, 0.96f, 1f, 1f));
		title.horizontalOverflow = HorizontalWrapMode.Overflow;

		var scrollGo = DefaultControls.CreateScrollView(new DefaultControls.Resources());
		scrollGo.name = "ConfigOverlayScroll";
		scrollGo.transform.SetParent(body, false);
		var scrollRect = scrollGo.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;

		var scrollImage = scrollGo.GetComponent<Image>();
		if (scrollImage != null)
		{
			EnsureUnityUiImageSprite(scrollImage);
			scrollImage.color = new Color(0.08f, 0.12f, 0.18f, 0.94f);
		}

		var viewport = scrollGo.transform.Find("Viewport")?.GetComponent<RectTransform>();
		var viewportImage = scrollGo.transform.Find("Viewport")?.GetComponent<Image>();
		if (viewportImage != null)
		{
			EnsureUnityUiImageSprite(viewportImage);
			viewportImage.color = new Color(0.07f, 0.11f, 0.17f, 0.96f);
		}

		var content = scrollGo.transform.Find("Viewport/Content")?.GetComponent<RectTransform>();
		if (content == null || viewport == null)
		{
			Logger.LogWarning("Failed to build Unity config overlay content.");
			overlay.gameObject.SetActive(false);
			_unityConfigOverlay = overlay.gameObject;
			return;
		}

		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.offsetMin = new Vector2(0f, 0f);
		content.offsetMax = new Vector2(0f, 0f);

		var contentLayout = content.gameObject.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
		contentLayout.padding = new RectOffset(6, 6, 6, 6);
		contentLayout.spacing = 8f;
		contentLayout.childControlWidth = true;
		contentLayout.childControlHeight = true;
		contentLayout.childForceExpandWidth = true;
		contentLayout.childForceExpandHeight = false;

		var fitter = content.gameObject.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		var scrollElement = scrollGo.GetComponent<LayoutElement>() ?? scrollGo.AddComponent<LayoutElement>();
		scrollElement.flexibleHeight = 1f;
		scrollElement.minHeight = 460f;

		var horizontalScrollbar = scrollGo.transform.Find("Scrollbar Horizontal");
		if (horizontalScrollbar != null)
		{
			horizontalScrollbar.gameObject.SetActive(false);
		}
		var verticalScrollbarImage = scrollGo.transform.Find("Scrollbar Vertical")?.GetComponent<Image>();
		if (verticalScrollbarImage != null)
		{
			EnsureUnityUiImageSprite(verticalScrollbarImage);
			verticalScrollbarImage.color = new Color(0.11f, 0.19f, 0.27f, 0.95f);
		}
		var verticalHandle = scrollGo.transform.Find("Scrollbar Vertical/Sliding Area/Handle")?.GetComponent<Image>();
		if (verticalHandle != null)
		{
			EnsureUnityUiImageSprite(verticalHandle);
			verticalHandle.color = new Color(0.30f, 0.66f, 0.93f, 0.95f);
		}

		_unityConfigOverlayContent = content;
		_unityConfigOverlay = overlay.gameObject;
		RebuildUnityConfigOverlayContent();
		overlay.gameObject.SetActive(false);
	}

	private void BuildUnityGroupConfigOverlay(Transform section)
	{
		var overlay = CreateImageRect(section, "GroupConfigOverlay", new Color(0.08f, 0.12f, 0.18f, 1f));
		var overlayElement = overlay.gameObject.AddComponent<LayoutElement>();
		overlayElement.ignoreLayout = true;
		StretchToParent(overlay, 0f);
		overlay.SetAsLastSibling();

		var body = CreateRectOnly(overlay, "GroupConfigOverlayBody");
		StretchToParent(body, 8f);
		var bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
		bodyLayout.spacing = 8f;
		bodyLayout.childControlWidth = true;
		bodyLayout.childControlHeight = true;
		bodyLayout.childForceExpandWidth = true;
		bodyLayout.childForceExpandHeight = false;

		var title = CreateTextLine(body, "Miner Toggle Menu", 17, FontStyle.Bold, TextAnchor.MiddleLeft, 28f, new Color(0.9f, 0.96f, 1f, 1f));
		title.horizontalOverflow = HorizontalWrapMode.Overflow;
		var subtitle = CreateTextLine(body, "Toggle all miners or specific groups", 12, FontStyle.Normal, TextAnchor.MiddleLeft, 24f, new Color(0.78f, 0.88f, 0.97f, 0.95f));
		subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
		subtitle.verticalOverflow = VerticalWrapMode.Overflow;

		var scrollGo = DefaultControls.CreateScrollView(new DefaultControls.Resources());
		scrollGo.name = "GroupConfigOverlayScroll";
		scrollGo.transform.SetParent(body, false);
		var scrollRect = scrollGo.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;

		var scrollImage = scrollGo.GetComponent<Image>();
		if (scrollImage != null)
		{
			EnsureUnityUiImageSprite(scrollImage);
			scrollImage.color = new Color(0.08f, 0.12f, 0.18f, 0.94f);
		}
		var viewportImage = scrollGo.transform.Find("Viewport")?.GetComponent<Image>();
		if (viewportImage != null)
		{
			EnsureUnityUiImageSprite(viewportImage);
			viewportImage.color = new Color(0.07f, 0.11f, 0.17f, 0.96f);
		}
		var horizontalScrollbar = scrollGo.transform.Find("Scrollbar Horizontal");
		if (horizontalScrollbar != null)
		{
			horizontalScrollbar.gameObject.SetActive(false);
		}
		var verticalScrollbarImage = scrollGo.transform.Find("Scrollbar Vertical")?.GetComponent<Image>();
		if (verticalScrollbarImage != null)
		{
			EnsureUnityUiImageSprite(verticalScrollbarImage);
			verticalScrollbarImage.color = new Color(0.11f, 0.19f, 0.27f, 0.95f);
		}
		var verticalHandle = scrollGo.transform.Find("Scrollbar Vertical/Sliding Area/Handle")?.GetComponent<Image>();
		if (verticalHandle != null)
		{
			EnsureUnityUiImageSprite(verticalHandle);
			verticalHandle.color = new Color(0.30f, 0.66f, 0.93f, 0.95f);
		}

		var content = scrollGo.transform.Find("Viewport/Content")?.GetComponent<RectTransform>();
		if (content == null)
		{
			Logger.LogWarning("Failed to build Unity group config overlay content.");
			overlay.gameObject.SetActive(false);
			_unityGroupConfigOverlay = overlay.gameObject;
			return;
		}

		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.offsetMin = new Vector2(0f, 0f);
		content.offsetMax = new Vector2(0f, 0f);

		var contentLayout = content.gameObject.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
		contentLayout.padding = new RectOffset(6, 6, 6, 6);
		contentLayout.spacing = 8f;
		contentLayout.childControlWidth = true;
		contentLayout.childControlHeight = true;
		contentLayout.childForceExpandWidth = true;
		contentLayout.childForceExpandHeight = false;

		var fitter = content.gameObject.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		var scrollElement = scrollGo.GetComponent<LayoutElement>() ?? scrollGo.AddComponent<LayoutElement>();
		scrollElement.flexibleHeight = 1f;
		scrollElement.minHeight = 500f;

		_unityGroupConfigOverlayContent = content;
		_unityGroupConfigOverlay = overlay.gameObject;
		RebuildUnityGroupConfigOverlayContent();
		overlay.gameObject.SetActive(false);
	}

	private void RebuildUnityGroupConfigOverlayContent()
	{
		if (_unityGroupConfigOverlayContent == null) return;
		_unityGroupToggleRows.Clear();

		for (var i = _unityGroupConfigOverlayContent.childCount - 1; i >= 0; i--)
		{
			var child = _unityGroupConfigOverlayContent.GetChild(i);
			Destroy(child.gameObject);
		}

		BuildUnityGroupToggleRow(
			_unityGroupConfigOverlayContent,
			"All AutoMiners",
			-1,
			() => ToggleAllMiners(true),
			() => ToggleAllMiners(false));

		EnsureGroupNamesInitialized();
		for (var group = 1; group <= _groupCount; group++)
		{
			var groupValue = group;
			BuildUnityGroupToggleRow(
				_unityGroupConfigOverlayContent,
				GetGroupLabelWithCount(GetGroupDisplayName(groupValue), groupValue),
				groupValue,
				() => ToggleGroupMiners(groupValue, true),
				() => ToggleGroupMiners(groupValue, false));
		}
	}

	private void BuildUnityGroupToggleRow(Transform parent, string labelText, int group, Action onOn, Action onOff)
	{
		var row = CreateRectOnly(parent, $"GroupToggleRow{group}");
		var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = UiRowSpacing;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 34f;

		var label = CreateTextLine(row, labelText, 13, FontStyle.Normal, TextAnchor.MiddleLeft, 32f, new Color(0.79f, 0.9f, 0.98f, 1f));
		var labelElement = label.gameObject.GetComponent<LayoutElement>();
		labelElement.preferredWidth = 250f;
		labelElement.minWidth = 250f;
		labelElement.flexibleWidth = 1f;

		CreateActionButton(row, "On", onOn, 80f, 32f);
		CreateActionButton(row, "Off", onOff, 80f, 32f);

		_unityGroupToggleRows.Add(new UnityGroupToggleRowBinding
		{
			group = group,
			label = label
		});
	}

	private void RebuildUnityConfigOverlayContent()
	{
		if (_unityConfigOverlayContent == null) return;

		_unityConfigSliderBindings.Clear();
		_unityConfigToggleBindings.Clear();
		_unityConfigGroupNameBindings.Clear();
		_unityConfigPresetNameBindings.Clear();

		for (var i = _unityConfigOverlayContent.childCount - 1; i >= 0; i--)
		{
			var child = _unityConfigOverlayContent.GetChild(i);
			Destroy(child.gameObject);
		}

		EnsureGroupNamesInitialized();

		AddUnityConfigFloatRow(
			_unityConfigOverlayContent,
			"Select Distance",
			1f,
			50f,
			() => _selectDistance,
			value =>
			{
				var next = Mathf.Clamp(value, 1f, 50f);
				if (Mathf.Abs(next - _selectDistance) <= 0.0001f) return;
				_selectDistance = next;
				SaveSettingsToDisk();
			},
			"0.#");

		AddUnityConfigFloatRow(
			_unityConfigOverlayContent,
			"Hover Distance",
			0.5f,
			50f,
			() => _hoverDistance,
			value =>
			{
				var next = Mathf.Clamp(value, 0.5f, 50f);
				if (Mathf.Abs(next - _hoverDistance) <= 0.0001f) return;
				_hoverDistance = next;
				SaveSettingsToDisk();
			},
			"0.#");

		AddUnityConfigFloatRow(
			_unityConfigOverlayContent,
			"Apply Interval",
			0.05f,
			5f,
			() => _applyInterval,
			value =>
			{
				var next = Mathf.Clamp(value, 0.05f, 5f);
				if (Mathf.Abs(next - _applyInterval) <= 0.0001f) return;
				_applyInterval = next;
				SaveSettingsToDisk();
			},
			"0.##");

		AddUnityConfigFloatRow(
			_unityConfigOverlayContent,
			"Rescan Interval",
			0.2f,
			15f,
			() => _rescanInterval,
			value =>
			{
				var next = Mathf.Clamp(value, 0.2f, 15f);
				if (Mathf.Abs(next - _rescanInterval) <= 0.0001f) return;
				_rescanInterval = next;
				SaveSettingsToDisk();
			},
			"0.##");

		AddUnityConfigFloatRow(
			_unityConfigOverlayContent,
			"Match Epsilon",
			0.02f,
			2f,
			() => _positionMatchEpsilon,
			value =>
			{
				var next = Mathf.Clamp(value, 0.02f, 2f);
				if (Mathf.Abs(next - _positionMatchEpsilon) <= 0.0001f) return;
				_positionMatchEpsilon = next;
				SaveSettingsToDisk();
			},
			"0.##");

		AddUnityConfigToggleRow(_unityConfigOverlayContent, "Show Hover Prompt", () => _showHoverPrompt, value => _showHoverPrompt = value);
		AddUnityConfigToggleRow(_unityConfigOverlayContent, "Show Hover Details", () => _showBuildingInfoDetails, value => _showBuildingInfoDetails = value);

		BuildUnityConfigHoverPromptPositionRow(_unityConfigOverlayContent);

		AddUnityConfigToggleRow(_unityConfigOverlayContent, "RGB Border", () => _rgbBorderEnabled, value => _rgbBorderEnabled = value);
		AddUnityConfigFloatRow(
			_unityConfigOverlayContent,
			"RGB Border Width",
			1f,
			8f,
			() => _rgbBorderWidth,
			value =>
			{
				var next = Mathf.Clamp(value, 1f, 8f);
				if (Mathf.Abs(next - _rgbBorderWidth) <= 0.0001f) return;
				_rgbBorderWidth = next;
				SaveSettingsToDisk();
			},
			"0.#");
		AddUnityConfigFloatRow(
			_unityConfigOverlayContent,
			"RGB Border Segment",
			1f,
			24f,
			() => _rgbBorderSegment,
			value =>
			{
				var next = Mathf.Clamp(value, 1f, 24f);
				if (Mathf.Abs(next - _rgbBorderSegment) <= 0.0001f) return;
				_rgbBorderSegment = next;
				SaveSettingsToDisk();
			},
			"0.#");
		AddUnityConfigFloatRow(
			_unityConfigOverlayContent,
			"RGB Border Speed",
			0f,
			5f,
			() => _rgbBorderSpeed,
			value =>
			{
				var next = Mathf.Clamp(value, 0f, 5f);
				if (Mathf.Abs(next - _rgbBorderSpeed) <= 0.0001f) return;
				_rgbBorderSpeed = next;
				SaveSettingsToDisk();
			},
			"0.##");

		AddUnityConfigSectionHeader(_unityConfigOverlayContent, "Group:");
		AddUnityConfigIntRow(
			_unityConfigOverlayContent,
			"Group Count",
			MinGroupCount,
			MaxGroupCount,
			() => _groupCount,
			value =>
			{
				var next = Mathf.Clamp(value, MinGroupCount, MaxGroupCount);
				if (next == _groupCount) return;
				SetGroupCount(next);
				SaveSettingsToDisk();
				RebuildUnityConfigOverlayContent();
			});

		for (var group = 1; group <= _groupCount; group++)
		{
			BuildUnityConfigGroupNameRow(_unityConfigOverlayContent, group);
		}

		AddUnityConfigSectionHeader(_unityConfigOverlayContent, "Presets:");
		AddUnityConfigIntRow(
			_unityConfigOverlayContent,
			"Preset Count",
			MinPresetCount,
			MaxPresetCount,
			() => GetPresetVisibleCount(),
			value =>
			{
				var next = Mathf.Clamp(value, MinPresetCount, MaxPresetCount);
				if (next == GetPresetVisibleCount()) return;
				SetPresetCount(next);
				SaveSettingsToDisk();
				SavePresetsToDisk();
				RebuildUnityPresetEditor();
				RebuildUnityConfigOverlayContent();
			});

		var visiblePresetCount = GetPresetVisibleCount();
		for (var presetIndex = 0; presetIndex < visiblePresetCount; presetIndex++)
		{
			BuildUnityConfigPresetNameRow(_unityConfigOverlayContent, presetIndex);
		}

		AddUnityConfigToggleRow(_unityConfigOverlayContent, "Debug Logging", () => _debugLogging, value => _debugLogging = value);
		AddUnityConfigToggleRow(_unityConfigOverlayContent, "Debug Toasts", () => _debugToasts, value => _debugToasts = value);

		var keybindHint = ShouldUseInlineKeybindUi()
			? "Open/Close key rebinding is currently unavailable in this UI."
			: "Rebind detected. Change Open/Close keys in Settings > Keybinds > MODS > Configurable Miners.";
		var hint = CreateTextLine(
			_unityConfigOverlayContent,
			keybindHint,
			12,
			FontStyle.Normal,
			TextAnchor.UpperLeft,
			46f,
			new Color(0.78f, 0.88f, 0.97f, 0.95f));
		hint.horizontalOverflow = HorizontalWrapMode.Wrap;
		hint.verticalOverflow = VerticalWrapMode.Overflow;
	}

	private void AddUnityConfigFloatRow(
		Transform parent,
		string label,
		float min,
		float max,
		Func<float> getValue,
		Action<float> setValue,
		string format)
	{
		var controls = CreateSliderInputRow(parent, label, min, max, getValue, setValue, format, markEditDirty: false);
		RegisterUnityConfigSlider(controls, getValue, format);
	}

	private void AddUnityConfigSectionHeader(Transform parent, string text)
	{
		var safeName = text.Replace(" ", string.Empty).Replace(":", string.Empty);
		var spacer = CreateRectOnly(parent, safeName + "SectionSpacer");
		var spacerElement = spacer.gameObject.AddComponent<LayoutElement>();
		spacerElement.preferredHeight = 6f;
		spacerElement.minHeight = 6f;

		var heading = CreateTextLine(
			parent,
			text,
			14,
			FontStyle.Bold,
			TextAnchor.MiddleLeft,
			26f,
			new Color(0.88f, 0.95f, 1f, 1f));
		heading.horizontalOverflow = HorizontalWrapMode.Overflow;
	}

	private void AddUnityConfigIntRow(
		Transform parent,
		string label,
		int min,
		int max,
		Func<int> getValue,
		Action<int> setValue)
	{
		var controls = CreateSliderInputRow(
			parent,
			label,
			min,
			max,
			() => getValue(),
			value => setValue(Mathf.RoundToInt(value)),
			"0",
			markEditDirty: false);
		controls.slider.wholeNumbers = true;
		controls.input.contentType = InputField.ContentType.IntegerNumber;
		RegisterUnityConfigSlider(controls, () => getValue(), "0");
	}

	private void RegisterUnityConfigSlider((Slider slider, InputField input) controls, Func<float> getter, string format)
	{
		_unityConfigSliderBindings.Add(new UnityConfigSliderBinding
		{
			slider = controls.slider,
			input = controls.input,
			getter = getter,
			format = format
		});
	}

	private void AddUnityConfigToggleRow(Transform parent, string label, Func<bool> getValue, Action<bool> setValue)
	{
		var controls = CreateStateButtonRow(parent, label, () =>
		{
			if (_unityUiSyncing) return;
			var current = getValue();
			setValue(!current);
			SaveSettingsToDisk();
		});
		_unityConfigToggleBindings.Add(new UnityConfigToggleBinding
		{
			button = controls.button,
			label = controls.label,
			getter = getValue
		});
	}

	private void BuildUnityConfigHoverPromptPositionRow(Transform parent)
	{
		var row = CreateRectOnly(parent, "ConfigHoverPromptPositionRow");
		var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = UiRowSpacing;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 34f;

		var label = CreateTextLine(row, "Hover Prompt Position", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 32f, new Color(0.79f, 0.9f, 0.98f, 1f));
		var labelElement = label.gameObject.GetComponent<LayoutElement>();
		labelElement.preferredWidth = UiLeftLabelWidth;
		labelElement.minWidth = UiLeftLabelWidth;

		_unityConfigHoverCenterButton = CreateActionButton(row, "Center", () => SetUnityHoverPromptPosition(HoverPromptPosition.Center), 70f, 32f);
		_unityConfigHoverTopButton = CreateActionButton(row, "Top", () => SetUnityHoverPromptPosition(HoverPromptPosition.Top), 54f, 32f);
		_unityConfigHoverBottomButton = CreateActionButton(row, "Bottom", () => SetUnityHoverPromptPosition(HoverPromptPosition.Bottom), 70f, 32f);
	}

	private void SetUnityHoverPromptPosition(HoverPromptPosition nextPosition)
	{
		if (_hoverPromptPosition == nextPosition) return;
		_hoverPromptPosition = nextPosition;
		SaveSettingsToDisk();
	}

	private void BuildUnityConfigGroupNameRow(Transform parent, int group)
	{
		var row = CreateRectOnly(parent, $"ConfigGroup{group}NameRow");
		var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = UiRowSpacing;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 34f;

		var label = CreateTextLine(row, $"Group {group} Name", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 32f, new Color(0.79f, 0.9f, 0.98f, 1f));
		var labelElement = label.gameObject.GetComponent<LayoutElement>();
		labelElement.preferredWidth = UiLeftLabelWidth;
		labelElement.minWidth = UiLeftLabelWidth;

		var input = CreateTextInput(row, 180f, value =>
		{
			if (_unityUiSyncing) return;
			EnsureGroupNamesInitialized();
			var index = group - 1;
			if (index < 0 || index >= _groupNames.Count) return;
			var next = string.IsNullOrWhiteSpace(value) ? BuildDefaultGroupName(group) : value.Trim();
			if (string.Equals(next, _groupNames[index], StringComparison.Ordinal)) return;
			_groupNames[index] = next;
			SyncLegacyGroupNameFields();
			SaveSettingsToDisk();
		});
		var inputElement = input.gameObject.GetComponent<LayoutElement>();
		inputElement.preferredWidth = 220f;
		inputElement.minWidth = 140f;
		inputElement.flexibleWidth = 1f;

		_unityConfigGroupNameBindings.Add(new UnityConfigGroupNameBinding
		{
			group = group,
			label = label,
			input = input
		});
	}

	private void BuildUnityConfigPresetNameRow(Transform parent, int presetIndex)
	{
		var row = CreateRectOnly(parent, $"ConfigPreset{presetIndex + 1}NameRow");
		var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = UiRowSpacing;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 34f;

		var label = CreateTextLine(row, $"Preset {presetIndex + 1} Name", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 32f, new Color(0.79f, 0.9f, 0.98f, 1f));
		var labelElement = label.gameObject.GetComponent<LayoutElement>();
		labelElement.preferredWidth = UiLeftLabelWidth;
		labelElement.minWidth = UiLeftLabelWidth;

		var input = CreateTextInput(row, 180f, value =>
		{
			if (_unityUiSyncing) return;
			SetPresetName(presetIndex, value);
		});
		var inputElement = input.gameObject.GetComponent<LayoutElement>();
		inputElement.preferredWidth = 220f;
		inputElement.minWidth = 140f;
		inputElement.flexibleWidth = 1f;

		_unityConfigPresetNameBindings.Add(new UnityConfigPresetNameBinding
		{
			presetIndex = presetIndex,
			label = label,
			input = input
		});
	}

	private void BuildSlotEditor(Transform parent)
	{
		var header = CreateRectOnly(parent, "SlotsHeader");
		var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
		headerLayout.spacing = 8f;
		headerLayout.childAlignment = TextAnchor.MiddleLeft;
		headerLayout.childControlWidth = true;
		headerLayout.childControlHeight = true;
		headerLayout.childForceExpandWidth = false;
		headerLayout.childForceExpandHeight = false;
		var headerElement = header.gameObject.AddComponent<LayoutElement>();
		headerElement.preferredHeight = 34f;

		var title = CreateTextLine(header, "Custom Slots", 14, FontStyle.Bold, TextAnchor.MiddleLeft, 30f, new Color(0.9f, 0.96f, 1f, 1f));
		var titleElement = title.gameObject.GetComponent<LayoutElement>();
		titleElement.flexibleWidth = 1f;

		CreateActionButton(header, "Normalize %", NormalizeSlotPercentages, 118f, 30f);

		for (var i = 0; i < 4; i++)
		{
			BuildSlotRow(parent, i);
		}
	}

	private void BuildSlotRow(Transform parent, int slotIndex)
	{
		var row = CreateRectOnly(parent, $"Slot{slotIndex + 1}Row");
		var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = 6f;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 36f;

		var slotLabel = CreateTextLine(row, $"S{slotIndex + 1}", 12, FontStyle.Bold, TextAnchor.MiddleLeft, 32f, new Color(0.85f, 0.94f, 1f, 1f));
		var slotLabelElement = slotLabel.gameObject.GetComponent<LayoutElement>();
		slotLabelElement.preferredWidth = 24f;
		slotLabelElement.minWidth = 24f;

		var enabledToggle = CreateMiniToggle(row, string.Empty, isOn =>
		{
			if (_unityUiSyncing) return;
			_edit.slots[slotIndex].enabled = isOn;
			_editDirty = true;
		}, 28f);
		_unitySlotEnabledToggles[slotIndex] = enabledToggle;

		var dropdown = CreateDropdown(row, slotIndex);
		_unitySlotDropdowns[slotIndex] = dropdown;

		var percentInput = CreateNumberInput(row, 70f, text =>
		{
			if (_unityUiSyncing) return;
			if (!TryParsePercent(text, out var pct))
			{
				pct = _edit.slots[slotIndex].percent;
			}
			pct = Mathf.Clamp(pct, 0f, 100f);
			_edit.slots[slotIndex].percent = pct;
			_editDirty = true;
			if (_unitySlotPercentInputs[slotIndex] != null)
			{
				_unitySlotPercentInputs[slotIndex]!.SetTextWithoutNotify(pct.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
			}
		});
		_unitySlotPercentInputs[slotIndex] = percentInput;

		var lockToggle = CreateMiniToggle(row, "Lock", isOn =>
		{
			if (_unityUiSyncing) return;
			_edit.slots[slotIndex].locked = isOn;
			_editDirty = true;
		}, 74f);
		_unitySlotLockedToggles[slotIndex] = lockToggle;
	}

	private void BuildUnityPresetEditor(Transform parent, bool compact = false)
	{
		_unityPresetNameLabels.Clear();
		if (!compact)
		{
			var header = CreateRectOnly(parent, "PresetsHeader");
			var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
			headerLayout.spacing = 8f;
			headerLayout.childAlignment = TextAnchor.MiddleLeft;
			headerLayout.childControlWidth = true;
			headerLayout.childControlHeight = true;
			headerLayout.childForceExpandWidth = false;
			headerLayout.childForceExpandHeight = false;
			var headerElement = header.gameObject.AddComponent<LayoutElement>();
			headerElement.preferredHeight = 32f;

			var title = CreateTextLine(
				header,
				"Presets",
				14,
				FontStyle.Bold,
				TextAnchor.MiddleLeft,
				28f,
				new Color(0.9f, 0.96f, 1f, 1f));
			var titleElement = title.gameObject.GetComponent<LayoutElement>();
			titleElement.flexibleWidth = 1f;
		}

		var visiblePresetCount = GetPresetVisibleCount();
		for (var i = 0; i < visiblePresetCount; i++)
		{
			BuildUnityPresetRow(parent, i, compact);
		}
	}

	private void RebuildUnityPresetEditor()
	{
		if (_unityPresetsContent == null) return;
		for (var i = _unityPresetsContent.childCount - 1; i >= 0; i--)
		{
			var child = _unityPresetsContent.GetChild(i);
			Destroy(child.gameObject);
		}
		BuildUnityPresetEditor(_unityPresetsContent, compact: true);
	}

	private void BuildUnityPresetRow(Transform parent, int presetIndex, bool compact)
	{
		var row = CreateRectOnly(parent, $"Preset{presetIndex + 1}Row");
		var rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
		rowLayout.spacing = 6f;
		rowLayout.childAlignment = TextAnchor.UpperLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = true;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = compact ? 54f : 68f;
		rowElement.minHeight = compact ? 54f : 68f;

		var nameRow = CreateRectOnly(row, $"Preset{presetIndex + 1}NameRow");
		var nameRowLayout = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
		nameRowLayout.spacing = compact ? 4f : 6f;
		nameRowLayout.childAlignment = TextAnchor.MiddleLeft;
		nameRowLayout.childControlWidth = true;
		nameRowLayout.childControlHeight = true;
		nameRowLayout.childForceExpandWidth = false;
		nameRowLayout.childForceExpandHeight = false;
		var nameRowElement = nameRow.gameObject.AddComponent<LayoutElement>();
		nameRowElement.preferredHeight = compact ? 22f : 30f;

		var nameLabel = CreateTextLine(
			nameRow,
			$"P{presetIndex + 1}: {GetPresetName(presetIndex)}",
			compact ? 12 : 13,
			FontStyle.Bold,
			TextAnchor.MiddleLeft,
			compact ? 22f : 30f,
			new Color(0.85f, 0.94f, 1f, 1f));
		var nameLabelElement = nameLabel.gameObject.GetComponent<LayoutElement>();
		nameLabelElement.flexibleWidth = 1f;
		_unityPresetNameLabels.Add(nameLabel);

		var buttonRow = CreateRectOnly(row, $"Preset{presetIndex + 1}ButtonRow");
		var buttonRowLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
		buttonRowLayout.spacing = compact ? 4f : 8f;
		buttonRowLayout.childAlignment = TextAnchor.MiddleLeft;
		buttonRowLayout.childControlWidth = true;
		buttonRowLayout.childControlHeight = true;
		buttonRowLayout.childForceExpandWidth = true;
		buttonRowLayout.childForceExpandHeight = false;
		var buttonRowElement = buttonRow.gameObject.AddComponent<LayoutElement>();
		var buttonHeight = compact ? 22f : 30f;
		buttonRowElement.preferredHeight = buttonHeight;

		var saveButton = CreateActionButton(buttonRow, "Save", () => SavePresetFromEditor(presetIndex), height: buttonHeight);
		var applyButton = CreateActionButton(buttonRow, "Apply", () => ApplyPresetToEditor(presetIndex), height: buttonHeight);
		var clearButton = CreateActionButton(buttonRow, "Clear", () => ClearPresetSlot(presetIndex), height: buttonHeight);
		if (!compact) return;
		SetCompactButtonFont(saveButton);
		SetCompactButtonFont(applyButton);
		SetCompactButtonFont(clearButton);
	}

	private static void SetCompactButtonFont(Button button)
	{
		var label = button.GetComponentInChildren<Text>();
		if (label != null)
		{
			label.fontSize = 11;
		}
	}

	private string GetPresetName(int index)
	{
		if (index < 0) return "Preset";
		EnsurePresetStorageCapacity(index + 1);
		if (index >= _presets.Count) return "Preset";
		if (string.IsNullOrWhiteSpace(_presets[index].name))
		{
			_presets[index].name = BuildDefaultPresetName(index);
		}
		return _presets[index].name;
	}

	private static string BuildDefaultPresetName(int index)
	{
		return $"Preset {index + 1}";
	}

	private int GetPresetVisibleCount()
	{
		if (_presetCount < MinPresetCount)
		{
			_presetCount = DefaultPresetCount;
		}
		_presetCount = Mathf.Clamp(_presetCount, MinPresetCount, MaxPresetCount);
		return _presetCount;
	}

	private void EnsurePresetStorageCapacity(int count)
	{
		var target = Mathf.Clamp(count, MinPresetCount, MaxPresetCount);
		while (_presets.Count < target)
		{
			_presets.Add(new PresetEntry
			{
				name = BuildDefaultPresetName(_presets.Count)
			});
		}
		for (var i = 0; i < _presets.Count; i++)
		{
			var entry = _presets[i];
			if (entry == null)
			{
				entry = new PresetEntry();
				_presets[i] = entry;
			}
			entry.name = string.IsNullOrWhiteSpace(entry.name)
				? BuildDefaultPresetName(i)
				: entry.name.Trim();
		}
	}

	private void SetPresetCount(int next)
	{
		_presetCount = Mathf.Clamp(next, MinPresetCount, MaxPresetCount);
		EnsurePresetStorageCapacity(_presetCount);
	}

	private void SetPresetName(int index, string value)
	{
		if (index < 0) return;
		EnsurePresetStorageCapacity(index + 1);
		if (index >= _presets.Count) return;
		var next = string.IsNullOrWhiteSpace(value) ? BuildDefaultPresetName(index) : value.Trim();
		if (string.Equals(_presets[index].name, next, StringComparison.Ordinal)) return;
		_presets[index].name = next;
		SavePresetsToDisk();
	}

	private void SavePresetFromEditor(int index)
	{
		if (index < 0 || index >= GetPresetVisibleCount()) return;
		EnsurePresetStorageCapacity(index + 1);
		_presets[index].name = GetPresetName(index);
		_presets[index].hasValue = true;
		_presets[index].overrideOutputs = _edit.overrideOutputs;
		_presets[index].spawnRate = _edit.spawnRate;
		_presets[index].spawnProbability = _edit.spawnProbability;
		_presets[index].allowGems = _edit.allowGems;
		_presets[index].polishedGems = _edit.polishedGems;
		_presets[index].allowGeodes = _edit.allowGeodes;
		_presets[index].polishedGeodes = _edit.polishedGeodes;
		_presets[index].group = Mathf.Clamp(_edit.group, 0, _groupCount);
		_presets[index].slots = CloneConfig(_edit).slots;
		SavePresetsToDisk();
		PushToast($"Saved {GetPresetName(index)}", ToastType.Success);
	}

	private void ApplyPresetToEditor(int index)
	{
		if (index < 0 || index >= GetPresetVisibleCount()) return;
		EnsurePresetStorageCapacity(index + 1);
		if (!_presets[index].hasValue)
		{
			PushToast($"{GetPresetName(index)} is empty", ToastType.Warning);
			return;
		}

		_edit.overrideOutputs = _presets[index].overrideOutputs;
		_edit.spawnRate = _presets[index].spawnRate;
		_edit.spawnProbability = _presets[index].spawnProbability;
		_edit.allowGems = _presets[index].allowGems;
		_edit.polishedGems = _presets[index].polishedGems;
		_edit.allowGeodes = _presets[index].allowGeodes;
		_edit.polishedGeodes = _presets[index].polishedGeodes;
		_edit.group = Mathf.Clamp(_presets[index].group, 0, _groupCount);
		_edit.slots = CloneConfig(new MinerConfigEntry { slots = _presets[index].slots }).slots;
		RefreshOutputOptionsForCurrentFilter();
		_editDirty = true;
		PushToast($"Applied {GetPresetName(index)}", ToastType.Success);
	}

	private void ClearPresetSlot(int index)
	{
		if (index < 0 || index >= GetPresetVisibleCount()) return;
		EnsurePresetStorageCapacity(index + 1);
		var name = GetPresetName(index);
		_presets[index] = new PresetEntry
		{
			name = name
		};
		SavePresetsToDisk();
		PushToast($"Cleared {name}", ToastType.Info);
	}

	private (Slider slider, InputField input) CreateSliderInputRow(
		Transform parent,
		string label,
		float min,
		float max,
		Func<float> getValue,
		Action<float> setValue,
		string format,
		bool markEditDirty = true)
	{
		var row = CreateRectOnly(parent, label.Replace(" ", string.Empty) + "Row");
		var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = 8f;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 36f;

		var labelText = CreateTextLine(row, label, 13, FontStyle.Normal, TextAnchor.MiddleLeft, 32f, new Color(0.79f, 0.9f, 0.98f, 1f));
		var labelElement = labelText.gameObject.GetComponent<LayoutElement>();
		labelElement.preferredWidth = 170f;
		labelElement.minWidth = 170f;

		var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
		sliderGo.transform.SetParent(row, false);
		var slider = sliderGo.GetComponent<Slider>();
		slider.minValue = min;
		slider.maxValue = max;
		slider.wholeNumbers = false;

		var sliderElement = sliderGo.GetComponent<LayoutElement>() ?? sliderGo.AddComponent<LayoutElement>();
		sliderElement.preferredWidth = 240f;
		sliderElement.minWidth = 180f;
		sliderElement.flexibleWidth = 1f;

		var background = sliderGo.transform.Find("Background")?.GetComponent<Image>();
		if (background != null)
		{
			EnsureUnityUiImageSprite(background);
			background.color = new Color(0.28f, 0.38f, 0.50f, 1f);
			background.raycastTarget = false;
		}
		var fill = sliderGo.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
		if (fill != null)
		{
			EnsureUnityUiImageSprite(fill);
			fill.color = new Color(0.38f, 0.86f, 1f, 1f);
			fill.raycastTarget = false;
		}
		var handle = sliderGo.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
		if (handle != null)
		{
			EnsureUnityUiImageSprite(handle);
			handle.color = new Color(0.95f, 0.99f, 1f, 1f);
			var handleRect = handle.rectTransform;
			handleRect.sizeDelta = new Vector2(14f, 22f);
		}
		EnsureSliderTrackVisual(sliderGo);

		InputField? input = null;
		input = CreateNumberInput(row, 74f, text =>
		{
			if (_unityUiSyncing) return;
			if (!float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
			{
				parsed = getValue();
			}
			parsed = Mathf.Clamp(parsed, min, max);
			setValue(parsed);
			if (markEditDirty)
			{
				_editDirty = true;
			}
			slider.SetValueWithoutNotify(parsed);
			input?.SetTextWithoutNotify(parsed.ToString(format, System.Globalization.CultureInfo.InvariantCulture));
		});

		slider.onValueChanged.AddListener(v =>
		{
			if (_unityUiSyncing) return;
			var clamped = Mathf.Clamp(v, min, max);
			setValue(clamped);
			if (markEditDirty)
			{
				_editDirty = true;
			}
			input?.SetTextWithoutNotify(clamped.ToString(format, System.Globalization.CultureInfo.InvariantCulture));
		});

		var initial = Mathf.Clamp(getValue(), min, max);
		slider.SetValueWithoutNotify(initial);
		input?.SetTextWithoutNotify(initial.ToString(format, System.Globalization.CultureInfo.InvariantCulture));

		return (slider, input!);
	}

	private Dropdown CreateDropdown(Transform parent, int slotIndex)
	{
		var dropdownGo = DefaultControls.CreateDropdown(new DefaultControls.Resources());
		dropdownGo.transform.SetParent(parent, false);
		var dropdown = dropdownGo.GetComponent<Dropdown>();
		dropdown.onValueChanged.AddListener(value => OnUnitySlotOutputSelected(slotIndex, value));

		var label = dropdownGo.transform.Find("Label")?.GetComponent<Text>();
		if (label != null)
		{
			label.font = _unityFont;
			label.fontSize = 12;
			label.color = Color.white;
		}
		dropdown.captionText = label;

		var itemText = dropdownGo.transform.Find("Template/Viewport/Content/Item/Item Label")?.GetComponent<Text>();
		if (itemText != null)
		{
			itemText.font = _unityFont;
			itemText.fontSize = 12;
			itemText.color = Color.white;
		}
		dropdown.itemText = itemText;

		var image = dropdownGo.GetComponent<Image>();
		if (image != null)
		{
			ForceUnityUiSolidImage(image);
			image.color = new Color(0.14f, 0.24f, 0.36f, 0.95f);
		}

		if (label != null)
		{
			var labelRect = label.rectTransform;
			labelRect.anchorMin = new Vector2(0f, 0f);
			labelRect.anchorMax = new Vector2(1f, 1f);
			labelRect.offsetMin = new Vector2(10f, 0f);
			labelRect.offsetMax = new Vector2(-30f, 0f);
			label.alignment = TextAnchor.MiddleLeft;
		}

		var arrow = dropdownGo.transform.Find("Arrow")?.GetComponent<Image>();
		if (arrow != null)
		{
			EnsureUnityUiImageSprite(arrow);
			arrow.color = new Color(0.86f, 0.93f, 1f, 0.98f);
			var arrowRect = arrow.rectTransform;
			arrowRect.anchorMin = new Vector2(1f, 0.5f);
			arrowRect.anchorMax = new Vector2(1f, 0.5f);
			arrowRect.pivot = new Vector2(0.5f, 0.5f);
			arrowRect.sizeDelta = new Vector2(8f, 8f);
			arrowRect.anchoredPosition = new Vector2(-12f, 0f);
		}

		var template = dropdownGo.transform.Find("Template");
		if (template != null)
		{
			var templateImage = template.GetComponent<Image>();
			if (templateImage != null)
			{
				ForceUnityUiSolidImage(templateImage);
				templateImage.color = new Color(0.08f, 0.12f, 0.18f, 0.98f);
			}

			var viewportImage = template.Find("Viewport")?.GetComponent<Image>();
			if (viewportImage != null)
			{
				ForceUnityUiSolidImage(viewportImage);
				viewportImage.color = new Color(0.09f, 0.14f, 0.21f, 0.98f);
			}
			var viewportRect = template.Find("Viewport") as RectTransform;
			if (viewportRect != null)
			{
				viewportRect.offsetMin = new Vector2(viewportRect.offsetMin.x, 0f);
				viewportRect.offsetMax = new Vector2(viewportRect.offsetMax.x, 0f);
			}

			var content = template.Find("Viewport/Content");
			if (content != null)
			{
				var contentBg = content.GetComponent<Image>();
				if (contentBg != null)
				{
					ForceUnityUiSolidImage(contentBg);
					contentBg.color = new Color(0.09f, 0.14f, 0.21f, 0.98f);
				}

				var item = content.Find("Item");
				if (item != null)
				{
					var itemBackground =
						item.Find("Item Background")?.GetComponent<Image>() ??
						item.Find("Background")?.GetComponent<Image>();
					if (itemBackground != null)
					{
						ForceUnityUiSolidImage(itemBackground);
						itemBackground.color = new Color(0.10f, 0.16f, 0.24f, 0.98f);
					}

					var itemCheckmark =
						item.Find("Item Checkmark")?.GetComponent<Image>() ??
						item.Find("Checkmark")?.GetComponent<Image>();
					if (itemCheckmark != null)
					{
						itemCheckmark.sprite = GetUnityUiCircleSprite();
						itemCheckmark.type = Image.Type.Simple;
						itemCheckmark.preserveAspect = true;
						itemCheckmark.color = new Color(0.35f, 0.83f, 1f, 1f);
						itemCheckmark.raycastTarget = false;
						var checkRect = itemCheckmark.rectTransform;
						checkRect.anchorMin = new Vector2(0f, 0.5f);
						checkRect.anchorMax = new Vector2(0f, 0.5f);
						checkRect.pivot = new Vector2(0.5f, 0.5f);
						checkRect.anchoredPosition = new Vector2(12f, 0f);
						checkRect.sizeDelta = new Vector2(11f, 11f);
					}
				}
			}

			var scrollbarImage = template.Find("Scrollbar")?.GetComponent<Image>();
			if (scrollbarImage != null)
			{
				ForceUnityUiSolidImage(scrollbarImage);
				scrollbarImage.color = new Color(0.10f, 0.16f, 0.24f, 0.95f);
			}
			var handleImage = template.Find("Scrollbar/Sliding Area/Handle")?.GetComponent<Image>();
			if (handleImage != null)
			{
				ForceUnityUiSolidImage(handleImage);
				handleImage.color = new Color(0.30f, 0.66f, 0.93f, 0.95f);
			}
		}

		var dropdownElement = dropdownGo.GetComponent<LayoutElement>() ?? dropdownGo.AddComponent<LayoutElement>();
		dropdownElement.preferredWidth = 320f;
		dropdownElement.minWidth = 220f;
		dropdownElement.flexibleWidth = 1f;
		dropdownElement.preferredHeight = 32f;
		dropdownElement.minHeight = 32f;

		return dropdown;
	}

	private Toggle CreateMiniToggle(Transform parent, string label, Action<bool> onChanged, float width)
	{
		var toggleGo = DefaultControls.CreateToggle(new DefaultControls.Resources());
		toggleGo.transform.SetParent(parent, false);
		var toggle = toggleGo.GetComponent<Toggle>();
		toggle.onValueChanged.AddListener(v => onChanged(v));

		var labelText = toggleGo.transform.Find("Label")?.GetComponent<Text>();
		if (labelText != null)
		{
			labelText.font = _unityFont;
			labelText.fontSize = 12;
			labelText.text = label;
			labelText.color = new Color(0.88f, 0.96f, 1f, 1f);
			labelText.alignment = TextAnchor.MiddleLeft;
			var labelRect = labelText.rectTransform;
			labelRect.anchorMin = new Vector2(0f, 0f);
			labelRect.anchorMax = new Vector2(1f, 1f);
			labelRect.offsetMin = new Vector2(24f, 0f);
			labelRect.offsetMax = Vector2.zero;
		}

		var bg = toggleGo.transform.Find("Background")?.GetComponent<Image>();
		if (bg != null)
		{
			EnsureUnityUiImageSprite(bg);
			bg.color = new Color(0.1f, 0.16f, 0.22f, 1f);
			var bgRect = bg.rectTransform;
			bgRect.anchorMin = new Vector2(0f, 0.5f);
			bgRect.anchorMax = new Vector2(0f, 0.5f);
			bgRect.pivot = new Vector2(0.5f, 0.5f);
			bgRect.anchoredPosition = new Vector2(10f, 0f);
			bgRect.sizeDelta = new Vector2(16f, 16f);
		}
		var check = toggleGo.transform.Find("Background/Checkmark")?.GetComponent<Image>();
		if (check != null)
		{
			EnsureUnityUiImageSprite(check);
			check.color = new Color(0.3f, 0.8f, 1f, 1f);
			var checkRect = check.rectTransform;
			checkRect.anchorMin = new Vector2(0.5f, 0.5f);
			checkRect.anchorMax = new Vector2(0.5f, 0.5f);
			checkRect.pivot = new Vector2(0.5f, 0.5f);
			checkRect.anchoredPosition = Vector2.zero;
			checkRect.sizeDelta = new Vector2(10f, 10f);
		}

		var element = toggleGo.GetComponent<LayoutElement>() ?? toggleGo.AddComponent<LayoutElement>();
		element.preferredWidth = width;
		element.minWidth = width;
		element.preferredHeight = 32f;
		element.minHeight = 32f;

		return toggle;
	}

	private InputField CreateNumberInput(Transform parent, float width, Action<string> onEndEdit)
	{
		var inputGo = DefaultControls.CreateInputField(new DefaultControls.Resources());
		inputGo.transform.SetParent(parent, false);
		var input = inputGo.GetComponent<InputField>();
		input.contentType = InputField.ContentType.DecimalNumber;
		input.lineType = InputField.LineType.SingleLine;
		input.customCaretColor = true;
		input.caretColor = new Color(0.95f, 0.99f, 1f, 1f);
		input.selectionColor = new Color(0.35f, 0.65f, 0.95f, 0.45f);
		input.onEndEdit.AddListener(value => onEndEdit(value));

		var text = inputGo.transform.Find("Text")?.GetComponent<Text>();
		if (text != null)
		{
			text.font = _unityFont;
			text.fontSize = 12;
			text.color = Color.white;
			text.alignment = TextAnchor.MiddleRight;
			text.supportRichText = false;
			text.raycastTarget = false;
		}
		var placeholder = inputGo.transform.Find("Placeholder")?.GetComponent<Text>();
		if (placeholder != null)
		{
			placeholder.font = _unityFont;
			placeholder.fontSize = 12;
			placeholder.text = "0";
			placeholder.color = new Color(0.6f, 0.7f, 0.8f, 0.8f);
			placeholder.alignment = TextAnchor.MiddleRight;
			placeholder.raycastTarget = false;
		}
		if (text != null) input.textComponent = text;
		if (placeholder != null) input.placeholder = placeholder;

		var bg = inputGo.GetComponent<Image>();
		if (bg != null) bg.color = new Color(0.12f, 0.19f, 0.28f, 0.95f);

		var element = inputGo.GetComponent<LayoutElement>() ?? inputGo.AddComponent<LayoutElement>();
		element.preferredWidth = width;
		element.minWidth = width;
		element.preferredHeight = 32f;
		element.minHeight = 32f;

		return input;
	}

	private InputField CreateTextInput(Transform parent, float width, Action<string> onEndEdit)
	{
		var input = CreateNumberInput(parent, width, onEndEdit);
		input.contentType = InputField.ContentType.Standard;
		input.characterValidation = InputField.CharacterValidation.None;
		if (input.textComponent != null)
		{
			input.textComponent.alignment = TextAnchor.MiddleLeft;
		}
		if (input.placeholder is Text placeholderText)
		{
			placeholderText.text = string.Empty;
			placeholderText.alignment = TextAnchor.MiddleLeft;
		}
		return input;
	}

	private void RefreshUnityUi()
	{
		if (!UseUnityUi || !_uiOpen) return;
		EnsureUnityUiBuilt();
		if (_unityUiRoot == null || !_unityUiRoot.activeSelf) return;

		if (_selectedMiner != null)
		{
			_selectedMinerPos = ReadMinerPosition(_selectedMiner);
		}

		_unityUiSyncing = true;
		if (_unityTitleText != null)
		{
			_unityTitleText.text = _editDirty ? "Configurable Miners *" : "Configurable Miners";
		}
		var hasCurrentTarget = false;
		var currentTargetLabel = "Current Target: no miner found";
		var currentTargetCoords = "Pos: no miner found";
		if (_activePanel == ActivePanel.MinerConfig && TrySelectMinerForInteraction(out var lookedMiner) && lookedMiner != null)
		{
			var lookedPos = ReadMinerPosition(lookedMiner);
			currentTargetLabel = $"Current Target: {ReadMinerLabel(lookedMiner)}";
			currentTargetCoords = $"Pos: {lookedPos.x:0.##}, {lookedPos.y:0.##}, {lookedPos.z:0.##}";
			hasCurrentTarget = true;
		}
		if (_unityHeaderTargetText != null)
		{
			_unityHeaderTargetText.text = currentTargetLabel;
		}
		if (_unityHeaderCoordsText != null)
		{
			_unityHeaderCoordsText.text = hasCurrentTarget ? currentTargetCoords : "Pos: no miner found";
		}
		SyncSliderInput(_unitySpawnRateSlider, _unitySpawnRateInput, _edit.spawnRate, "0.###");
		SyncSliderInput(_unitySpawnProbabilitySlider, _unitySpawnProbabilityInput, _edit.spawnProbability, "0.##");
		SyncSliderInput(_unityAreaRadiusSlider, _unityAreaRadiusInput, _areaApplyRadius, "0.#");
		EnforceInputTheme(_unitySpawnRateInput);
		EnforceInputTheme(_unitySpawnProbabilityInput);
		EnforceInputTheme(_unityAreaRadiusInput);

		UpdateStateButton(_unityOverrideButton, _unityOverrideButtonText, _edit.overrideOutputs, "Enabled", "Disabled");
		UpdateStateButton(_unityAllowGemsButton, _unityAllowGemsButtonText, _edit.allowGems, "Enabled", "Disabled");
		UpdateStateButton(_unityPolishedGemsButton, _unityPolishedGemsButtonText, _edit.polishedGems, "Enabled", "Disabled");
		UpdateStateButton(_unityAllowGeodesButton, _unityAllowGeodesButtonText, _edit.allowGeodes, "Enabled", "Disabled");
		UpdateStateButton(_unityPolishedGeodesButton, _unityPolishedGeodesButtonText, _edit.polishedGeodes, "Enabled", "Disabled");
		ApplyDependentButtonState(_unityPolishedGemsButton, _unityPolishedGemsButtonText, _edit.allowGems);
		ApplyDependentButtonState(_unityPolishedGeodesButton, _unityPolishedGeodesButtonText, _edit.allowGeodes);

		if (_unityGroupText != null)
		{
			_unityGroupText.text = GetGroupDisplayName(_edit.group);
		}
		RefreshUnityConfigOverlay();

		RefreshUnitySlotRows();
		RefreshUnityPresetRows();
		_unityUiSyncing = false;
	}

	private void UnityUiToggleConfigOverlay()
	{
		SetUnityConfigOpen(!_unityConfigOpen);
	}

	private void UnityUiToggleGroupConfigOverlay()
	{
		SetUnityGroupConfigOpen(!_unityGroupConfigOpen);
	}

	private void SetUnityConfigOpen(bool open)
	{
		_unityConfigOpen = open;
		if (_unityConfigOpen)
		{
			_unityGroupConfigOpen = false;
			RebuildUnityConfigOverlayContent();
		}
		RefreshUnityConfigOverlay();
	}

	private void SetUnityGroupConfigOpen(bool open)
	{
		_unityGroupConfigOpen = open;
		if (_unityGroupConfigOpen)
		{
			_unityConfigOpen = false;
			RebuildUnityGroupConfigOverlayContent();
		}
		RefreshUnityConfigOverlay();
	}

	private void RefreshUnityConfigOverlay()
	{
		var hasOverlayOpen = _unityConfigOpen || _unityGroupConfigOpen;
		if (_unityPresetsSection != null)
		{
			_unityPresetsSection.SetActive(_activePanel == ActivePanel.MinerConfig);
		}
		if (_unityMinerTuningSectionTitle != null)
		{
			_unityMinerTuningSectionTitle.gameObject.SetActive(!hasOverlayOpen);
		}
		if (_unityMinerTuningContent != null)
		{
			_unityMinerTuningContent.gameObject.SetActive(!hasOverlayOpen);
		}
		if (_unityConfigOverlay != null)
		{
			_unityConfigOverlay.SetActive(_unityConfigOpen);
		}
		if (_unityGroupConfigOverlay != null)
		{
			_unityGroupConfigOverlay.SetActive(_unityGroupConfigOpen);
		}
		if (_unityConfigButtonText != null)
		{
			_unityConfigButtonText.text = _unityConfigOpen ? "Close Config" : "Config";
		}
		if (_unityGroupConfigButtonText != null)
		{
			_unityGroupConfigButtonText.text = _unityGroupConfigOpen ? "Close Group Config" : "Group Config";
		}

		if (_unityConfigOpen)
		{
			for (var i = 0; i < _unityConfigSliderBindings.Count; i++)
			{
				var binding = _unityConfigSliderBindings[i];
				var value = binding.getter();
				SyncSliderInput(binding.slider, binding.input, value, binding.format);
				EnforceInputTheme(binding.input);
			}

			for (var i = 0; i < _unityConfigToggleBindings.Count; i++)
			{
				var binding = _unityConfigToggleBindings[i];
				UpdateStateButton(binding.button, binding.label, binding.getter(), "Enabled", "Disabled");
			}

			UpdateChoiceButton(_unityConfigHoverCenterButton, _hoverPromptPosition == HoverPromptPosition.Center);
			UpdateChoiceButton(_unityConfigHoverTopButton, _hoverPromptPosition == HoverPromptPosition.Top);
			UpdateChoiceButton(_unityConfigHoverBottomButton, _hoverPromptPosition == HoverPromptPosition.Bottom);

			EnsureGroupNamesInitialized();
			for (var i = 0; i < _unityConfigGroupNameBindings.Count; i++)
			{
				var binding = _unityConfigGroupNameBindings[i];
				var index = binding.group - 1;
				if (index < 0 || index >= _groupNames.Count) continue;
				EnforceTextInputTheme(binding.input);
				if (!binding.input.isFocused)
				{
					binding.input.SetTextWithoutNotify(_groupNames[index]);
				}
			}

			for (var i = 0; i < _unityConfigPresetNameBindings.Count; i++)
			{
				var binding = _unityConfigPresetNameBindings[i];
				if (binding.presetIndex < 0 || binding.presetIndex >= GetPresetVisibleCount()) continue;
				EnforceTextInputTheme(binding.input);
				if (!binding.input.isFocused)
				{
					binding.input.SetTextWithoutNotify(GetPresetName(binding.presetIndex));
				}
			}
		}

		if (_unityGroupConfigOpen)
		{
			RefreshUnityGroupConfigOverlay();
		}
	}

	private void RefreshUnityGroupConfigOverlay()
	{
		if (!_unityGroupConfigOpen) return;
		if (_unityGroupConfigOverlayContent == null) return;
		if (_unityGroupToggleRows.Count != _groupCount + 1)
		{
			RebuildUnityGroupConfigOverlayContent();
			return;
		}

		for (var i = 0; i < _unityGroupToggleRows.Count; i++)
		{
			var row = _unityGroupToggleRows[i];
			if (row.group < 0)
			{
				row.label.text = "All AutoMiners";
				continue;
			}
			row.label.text = GetGroupLabelWithCount(GetGroupDisplayName(row.group), row.group);
		}
	}

	private void RefreshUnitySlotRows()
	{
		if (_outputOptions.Count == 0)
		{
			RefreshOutputOptionsForCurrentFilter();
		}

		for (var i = 0; i < 4; i++)
		{
			var slot = _edit.slots[i];
			if (_unitySlotEnabledToggles[i] != null)
			{
				_unitySlotEnabledToggles[i]!.SetIsOnWithoutNotify(slot.enabled);
			}
			if (_unitySlotLockedToggles[i] != null)
			{
				_unitySlotLockedToggles[i]!.SetIsOnWithoutNotify(slot.locked);
			}
			if (_unitySlotPercentInputs[i] != null)
			{
				EnforceInputTheme(_unitySlotPercentInputs[i]);
				if (!_unitySlotPercentInputs[i]!.isFocused)
				{
					_unitySlotPercentInputs[i]!.SetTextWithoutNotify(slot.percent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
				}
			}

			var dropdown = _unitySlotDropdowns[i];
			if (dropdown == null) continue;

			var optionMap = _unitySlotOptionMaps[i];
			optionMap.Clear();
			dropdown.options.Clear();
			dropdown.options.Add(new Dropdown.OptionData("Choose output"));

			for (var o = 0; o < _outputOptions.Count; o++)
			{
				optionMap.Add(_outputOptions[o]);
				dropdown.options.Add(new Dropdown.OptionData(StripRichText(_outputOptions[o].displayName)));
			}

			var selectedIndex = 0;
			for (var o = 0; o < optionMap.Count; o++)
			{
				var opt = optionMap[o];
				if (!string.Equals(opt.resourceType, slot.resourceType, StringComparison.OrdinalIgnoreCase)) continue;
				if (!string.Equals(opt.pieceType, slot.pieceType, StringComparison.OrdinalIgnoreCase)) continue;
				if (opt.polished != slot.polished) continue;
				selectedIndex = o + 1;
				break;
			}

			dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, dropdown.options.Count - 1));
			dropdown.RefreshShownValue();
		}
	}

	private void RefreshUnityPresetRows()
	{
		var count = Mathf.Min(GetPresetVisibleCount(), _unityPresetNameLabels.Count);
		for (var i = 0; i < count; i++)
		{
			var label = _unityPresetNameLabels[i];
			label.text = $"P{i + 1}: {GetPresetName(i)}";
		}
	}

	private void OnUnitySlotOutputSelected(int slotIndex, int dropdownValue)
	{
		if (_unityUiSyncing) return;
		if (slotIndex < 0 || slotIndex >= 4) return;

		var slot = _edit.slots[slotIndex];
		if (dropdownValue <= 0)
		{
			slot.resourceType = string.Empty;
			slot.pieceType = string.Empty;
			slot.polished = false;
			slot.enabled = false;
			_editDirty = true;
			return;
		}

		var optionMap = _unitySlotOptionMaps[slotIndex];
		var optionIndex = dropdownValue - 1;
		if (optionIndex < 0 || optionIndex >= optionMap.Count) return;
		var option = optionMap[optionIndex];

		slot.resourceType = option.resourceType;
		slot.pieceType = option.pieceType;
		slot.polished = option.polished;
		slot.enabled = true;
		if (slot.percent <= 0f) slot.percent = 25f;
		_editDirty = true;
	}

	private (Button button, Text label) CreateStateButtonRow(Transform parent, string label, Action onClick)
	{
		var row = CreateRectOnly(parent, label.Replace(" ", string.Empty) + "StateRow");
		var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = UiRowSpacing;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 34f;

		var labelText = CreateTextLine(row, label, 13, FontStyle.Normal, TextAnchor.MiddleLeft, 32f, new Color(0.79f, 0.9f, 0.98f, 1f));
		var labelElement = labelText.gameObject.GetComponent<LayoutElement>();
		labelElement.preferredWidth = UiLeftLabelWidth;
		labelElement.minWidth = UiLeftLabelWidth;

		var button = CreateActionButton(row, "Disabled", onClick, UiStateButtonWidth, 32f);
		var stateText = button.GetComponentInChildren<Text>();
		return (button, stateText);
	}

	private ((Button button, Text label) left, (Button button, Text label) right) CreatePairedStateButtonRow(
		Transform parent,
		string leftLabel,
		Action onLeftClick,
		string rightLabel,
		Action onRightClick)
	{
		var row = CreateRectOnly(parent, (leftLabel + rightLabel).Replace(" ", string.Empty) + "PairedStateRow");
		var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = UiRowSpacing;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 34f;

		var leftLabelText = CreateTextLine(row, leftLabel, 13, FontStyle.Normal, TextAnchor.MiddleLeft, 32f, new Color(0.79f, 0.9f, 0.98f, 1f));
		var leftLabelElement = leftLabelText.gameObject.GetComponent<LayoutElement>();
		leftLabelElement.preferredWidth = UiLeftLabelWidth;
		leftLabelElement.minWidth = UiLeftLabelWidth;

		var leftButton = CreateActionButton(row, "Disabled", onLeftClick, UiStateButtonWidth, 32f);
		var leftButtonLabel = leftButton.GetComponentInChildren<Text>();

		var spacer = CreateRectOnly(row, "PairedStateSpacer");
		var spacerElement = spacer.gameObject.AddComponent<LayoutElement>();
		spacerElement.preferredWidth = 16f;
		spacerElement.minWidth = 16f;

		var rightLabelText = CreateTextLine(row, rightLabel, 13, FontStyle.Normal, TextAnchor.MiddleLeft, 32f, new Color(0.79f, 0.9f, 0.98f, 1f));
		var rightLabelElement = rightLabelText.gameObject.GetComponent<LayoutElement>();
		rightLabelElement.preferredWidth = UiSecondaryLabelWidth;
		rightLabelElement.minWidth = UiSecondaryLabelWidth;

		var rightButton = CreateActionButton(row, "Disabled", onRightClick, UiStateButtonWidth, 32f);
		var rightButtonLabel = rightButton.GetComponentInChildren<Text>();

		return ((leftButton, leftButtonLabel!), (rightButton, rightButtonLabel!));
	}

	private void ToggleOverrideOutputs()
	{
		_edit.overrideOutputs = !_edit.overrideOutputs;
		_editDirty = true;
	}

	private void ToggleAllowGems()
	{
		_edit.allowGems = !_edit.allowGems;
		RefreshOutputOptionsForCurrentFilter();
		_editDirty = true;
	}

	private void TogglePolishedGems()
	{
		_edit.polishedGems = !_edit.polishedGems;
		RefreshOutputOptionsForCurrentFilter();
		_editDirty = true;
	}

	private void ToggleAllowGeodes()
	{
		_edit.allowGeodes = !_edit.allowGeodes;
		RefreshOutputOptionsForCurrentFilter();
		_editDirty = true;
	}

	private void TogglePolishedGeodes()
	{
		_edit.polishedGeodes = !_edit.polishedGeodes;
		RefreshOutputOptionsForCurrentFilter();
		_editDirty = true;
	}

	private void BuildGroupSelector(Transform parent)
	{
		var row = CreateRectOnly(parent, "GroupSelectorRow");
		var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = 8f;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;
		var rowElement = row.gameObject.AddComponent<LayoutElement>();
		rowElement.preferredHeight = 34f;

		var label = CreateTextLine(row, "Group", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 32f, new Color(0.79f, 0.9f, 0.98f, 1f));
		var labelElement = label.gameObject.GetComponent<LayoutElement>();
		labelElement.preferredWidth = UiLeftLabelWidth;
		labelElement.minWidth = UiLeftLabelWidth;

		CreateActionButton(row, "<", () => StepEditGroup(-1), 36f, 32f);
		_unityGroupText = CreateTextLine(row, "None", 13, FontStyle.Bold, TextAnchor.MiddleCenter, 32f, new Color(0.92f, 0.98f, 1f, 1f));
		var groupElement = _unityGroupText.gameObject.GetComponent<LayoutElement>();
		groupElement.preferredWidth = 100f;
		groupElement.minWidth = 100f;
		CreateActionButton(row, ">", () => StepEditGroup(1), 36f, 32f);
	}

	private void StepEditGroup(int delta)
	{
		EnsureGroupNamesInitialized();
		_edit.group = Mathf.Clamp(_edit.group + delta, 0, _groupCount);
		_editDirty = true;
	}

	private void UnityUiSelectMiner()
	{
		if (!TrySelectMinerForInteraction(out var miner) || miner == null)
		{
			PushToast("No miner in view", ToastType.Warning);
			return;
		}

		BindSelectedMiner(miner);
		RefreshOutputOptionsForCurrentFilter();
		PushToast("Miner selected", ToastType.Success);
	}

	private void UnityUiToggleGroup(bool enabled)
	{
		if (_edit.group <= 0)
		{
			PushToast("Select a group first", ToastType.Warning);
			return;
		}
		ToggleGroupMiners(_edit.group, enabled);
	}

	private static void EnsureEventSystem()
	{
		if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
		var eventSystem = new GameObject("ConfigurableMinersEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
		DontDestroyOnLoad(eventSystem);
	}

	private static RectTransform CreateRectOnly(Transform parent, string name)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		return go.GetComponent<RectTransform>();
	}

	private static RectTransform CreateImageRect(Transform parent, string name, Color color)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);
		var image = go.GetComponent<Image>();
		image.color = color;
		return go.GetComponent<RectTransform>();
	}

	private static void StretchToParent(RectTransform rect, float margin)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(margin, margin);
		rect.offsetMax = new Vector2(-margin, -margin);
	}

	private Text CreateTextLine(Transform parent, string text, int fontSize, FontStyle style, TextAnchor anchor, float height, Color color)
	{
		var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
		go.transform.SetParent(parent, false);
		var t = go.GetComponent<Text>();
		t.font = _unityFont;
		t.text = text;
		t.fontSize = fontSize;
		t.fontStyle = style;
		t.alignment = anchor;
		t.color = color;
		t.horizontalOverflow = HorizontalWrapMode.Wrap;
		t.verticalOverflow = VerticalWrapMode.Truncate;

		var le = go.AddComponent<LayoutElement>();
		le.minHeight = height;
		le.preferredHeight = height;
		return t;
	}

	private Button CreateActionButton(Transform parent, string text, Action onClick, float width = 0f, float height = 34f)
	{
		var buttonGo = DefaultControls.CreateButton(new DefaultControls.Resources());
		buttonGo.name = text.Replace(" ", string.Empty) + "Button";
		buttonGo.transform.SetParent(parent, false);
		var button = buttonGo.GetComponent<Button>();
		var image = buttonGo.GetComponent<Image>();
		if (image != null)
		{
			image.color = new Color(0.18f, 0.36f, 0.54f, 0.95f);
		}

		var label = buttonGo.GetComponentInChildren<Text>();
		if (label != null)
		{
			label.font = _unityFont;
			label.text = text;
			label.fontSize = 14;
			label.color = new Color(0.94f, 0.98f, 1f, 1f);
		}

		button.onClick.AddListener(() => onClick());

		var rect = buttonGo.GetComponent<RectTransform>();
		if (rect != null)
		{
			rect.sizeDelta = width > 0f ? new Vector2(width, height) : new Vector2(rect.sizeDelta.x, height);
		}

		var le = buttonGo.GetComponent<LayoutElement>() ?? buttonGo.AddComponent<LayoutElement>();
		if (width > 0f)
		{
			le.preferredWidth = width;
			le.minWidth = width;
			le.flexibleWidth = 0f;
		}
		else
		{
			le.flexibleWidth = 1f;
		}
		le.preferredHeight = height;
		le.minHeight = height;
		return button;
	}

	private static void SyncSliderInput(Slider? slider, InputField? input, float value, string format)
	{
		if (slider != null)
		{
			slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.minValue, slider.maxValue));
		}
		if (input != null)
		{
			if (input.isFocused) return;
			input.SetTextWithoutNotify(value.ToString(format, System.Globalization.CultureInfo.InvariantCulture));
		}
	}

	private static void UpdateStateButton(Button? button, Text? label, bool enabled, string onText, string offText)
	{
		if (label != null)
		{
			label.text = enabled ? onText : offText;
		}
		var image = button?.targetGraphic as Image;
		if (image != null)
		{
			image.color = enabled
				? new Color(0.16f, 0.66f, 0.38f, 0.96f)
				: new Color(0.47f, 0.2f, 0.2f, 0.96f);
		}
	}

	private static void ApplyDependentButtonState(Button? button, Text? label, bool isParentEnabled)
	{
		if (button == null) return;
		button.interactable = isParentEnabled;
		if (button.targetGraphic is Image image && !isParentEnabled)
		{
			EnsureUnityUiImageSprite(image);
			image.color = new Color(0.33f, 0.37f, 0.42f, 0.95f);
		}

		if (label != null)
		{
			label.color = isParentEnabled
				? new Color(0.94f, 0.98f, 1f, 1f)
				: new Color(0.76f, 0.8f, 0.85f, 0.92f);
		}
	}

	private static bool TryParsePercent(string text, out float value)
	{
		value = 0f;
		if (string.IsNullOrWhiteSpace(text)) return false;
		var parseText = text.Trim();
		if (parseText.EndsWith("%", StringComparison.Ordinal))
		{
			parseText = parseText.Substring(0, parseText.Length - 1).TrimEnd();
		}

		return float.TryParse(parseText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
	}

	private static string StripRichText(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		var sb = new StringBuilder(value.Length);
		var inTag = false;
		for (var i = 0; i < value.Length; i++)
		{
			var c = value[i];
			if (c == '<')
			{
				inTag = true;
				continue;
			}
			if (c == '>')
			{
				inTag = false;
				continue;
			}
			if (!inTag) sb.Append(c);
		}

		return sb.ToString();
	}

	private static void EnsureSliderTrackVisual(GameObject sliderGo)
	{
		if (sliderGo.transform.Find("TrackVisual") != null) return;
		var track = new GameObject("TrackVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		track.transform.SetParent(sliderGo.transform, false);
		track.transform.SetAsFirstSibling();

		var rect = track.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 0.5f);
		rect.anchorMax = new Vector2(1f, 0.5f);
		rect.offsetMin = new Vector2(8f, -3f);
		rect.offsetMax = new Vector2(-8f, 3f);

		var image = track.GetComponent<Image>();
		EnsureUnityUiImageSprite(image);
		image.color = new Color(0.36f, 0.46f, 0.58f, 0.95f);
		image.raycastTarget = false;
	}

	private void EnforceInputTheme(InputField? input)
	{
		if (input == null) return;

		input.customCaretColor = true;
		input.caretColor = Color.white;
		input.selectionColor = new Color(0.35f, 0.65f, 0.95f, 0.45f);

		if (input.textComponent != null)
		{
			input.textComponent.font = _unityFont;
			input.textComponent.fontSize = 12;
			input.textComponent.color = Color.white;
			input.textComponent.alignment = TextAnchor.MiddleRight;
			input.textComponent.supportRichText = false;
			input.textComponent.raycastTarget = false;
		}

		if (input.placeholder is Text placeholderText)
		{
			placeholderText.font = _unityFont;
			placeholderText.fontSize = 12;
			placeholderText.color = new Color(0.75f, 0.82f, 0.9f, 0.7f);
			placeholderText.alignment = TextAnchor.MiddleRight;
			placeholderText.raycastTarget = false;
		}

		if (input.targetGraphic is Image bg)
		{
			EnsureUnityUiImageSprite(bg);
			bg.color = new Color(0.12f, 0.19f, 0.28f, 0.95f);
		}
	}

	private void EnforceTextInputTheme(InputField? input)
	{
		EnforceInputTheme(input);
		if (input == null) return;
		if (input.textComponent != null)
		{
			input.textComponent.alignment = TextAnchor.MiddleLeft;
		}
		if (input.placeholder is Text placeholderText)
		{
			placeholderText.alignment = TextAnchor.MiddleLeft;
		}
	}

	private static void UpdateChoiceButton(Button? button, bool selected)
	{
		if (button?.targetGraphic is not Image image) return;
		EnsureUnityUiImageSprite(image);
		image.color = selected
			? new Color(0.23f, 0.56f, 0.82f, 0.98f)
			: new Color(0.18f, 0.36f, 0.54f, 0.95f);
	}

	private static void EnsureUnityUiImageSprite(Image image)
	{
		if (image.sprite != null) return;
		image.sprite = GetUnityUiSolidSprite();
		image.type = Image.Type.Simple;
	}

	private static void ForceUnityUiSolidImage(Image image)
	{
		image.sprite = GetUnityUiSolidSprite();
		image.type = Image.Type.Simple;
		image.preserveAspect = false;
	}

	private static Sprite GetUnityUiSolidSprite()
	{
		if (_unityUiSolidSprite != null) return _unityUiSolidSprite;
		var tex = Texture2D.whiteTexture;
		_unityUiSolidSprite = Sprite.Create(
			tex,
			new Rect(0f, 0f, tex.width, tex.height),
			new Vector2(0.5f, 0.5f));
		return _unityUiSolidSprite;
	}

	private static Sprite GetUnityUiCircleSprite()
	{
		if (_unityUiCircleSprite != null) return _unityUiCircleSprite;
		const int size = 32;
		var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};

		var center = (size - 1) * 0.5f;
		var radius = size * 0.36f;
		var edge = 1.5f;
		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var dx = x - center;
				var dy = y - center;
				var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
				var alpha = Mathf.Clamp01((radius - dist) / edge);
				tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
			}
		}

		tex.Apply();
		_unityUiCircleSprite = Sprite.Create(
			tex,
			new Rect(0f, 0f, size, size),
			new Vector2(0.5f, 0.5f),
			100f);
		return _unityUiCircleSprite;
	}
}
