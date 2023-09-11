using System.Numerics;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;

using ImGuiNET;

using Ktisis.Scene;
using Ktisis.Scene.Impl;
using Ktisis.Editing;
using Ktisis.Common.Extensions;
using Ktisis.Common.Utility;
using Ktisis.Data.Config;
using Ktisis.Interface.Overlay;
using Ktisis.Interface.Components;
using Ktisis.Interface.Widgets;
using Ktisis.Core.Services;
using Ktisis.ImGuizmo;
using Ktisis.Localization;

namespace Ktisis.Interface.Windows;

public class TransformWindow : Window {
	// Constructor

	private readonly ConfigService _cfg;
	private readonly LocaleService _locale;
	private readonly SceneManager _scene;
	private readonly SceneEditor _editor;
	private readonly CameraService _camera;

	private readonly Gizmo2D? Gizmo;
	private readonly TransformTable Table;

	private ConfigFile Config => this._cfg.Config;

	public TransformWindow(
		ConfigService _cfg,
		LocaleService _locale,
		SceneManager _scene,
		SceneEditor _editor,
		CameraService _camera
	) : base(
		"##__TransformEditor__",
		ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize
	) {
		this._cfg = _cfg;
		this._locale = _locale;
		this._scene = _scene;
		this._editor = _editor;
		this._camera = _camera;

		this.Gizmo = Gizmo2D.Create(GizmoID.TransformEditor);
		this.Table = new TransformTable("##Ktisis_TransformTable", _locale);
		this.Table.OnClickOperation += OnClickOperation;

		RespectCloseHotkey = false;
	}

	// Events

	private void OnClickOperation(Operation op) {
		this.Config.Gizmo_Op = op;
	}

	// UI draw

	public override void PreDraw() {
		this.WindowName = this._locale.Translate("transform_edit.title");
	}

	public override void Draw() {
		if (!this._scene.IsActive) {
			this.Close();
			return;
		}

		// Toggles

		var spacing = ImGui.GetStyle().ItemInnerSpacing.X;

		var iconSize = UiBuilder.IconFont.FontSize * 2;
		var iconBtnSize =new Vector2(iconSize, iconSize);

		var mode = this.Config.Gizmo_Mode;
		var modeIcon = mode == Mode.World ? FontAwesomeIcon.Globe : FontAwesomeIcon.Home;
		var modeKey = mode == Mode.World ? "world" : "local";
		var modeHint = this._locale.Translate($"transform_edit.mode.{modeKey}");
		if (Buttons.DrawIconButtonHint(modeIcon, modeHint, iconBtnSize))
			this.Config.Gizmo_Mode = mode == Mode.World ? Mode.Local : Mode.World;

		ImGui.SameLine(0, spacing);

		var flags = this.Config.Editor_Flags;

		var isMirror = flags.HasFlag(EditFlags.Mirror);
		var flagIcon = isMirror ? FontAwesomeIcon.ArrowDownUpAcrossLine : FontAwesomeIcon.GripLines;
		var flagKey = isMirror ? "mirror" : "parallel";
		var flagHint = this._locale.Translate($"transform_edit.flags.{flagKey}");
		if (Buttons.DrawIconButtonHint(flagIcon, flagHint, iconBtnSize))
			this.Config.Editor_Flags ^= EditFlags.Mirror;

		ImGui.SameLine(0, spacing);
		var avail = ImGui.GetContentRegionAvail().X;
		if (avail > iconSize)
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - iconSize);

		var show = this.Config.Editor_Gizmo;
		var gizmoIcon = show ? FontAwesomeIcon.CaretUp : FontAwesomeIcon.CaretDown;
		var gizmoKey = show ? "hide" : "show";
		var gizmoHint = this._locale.Translate($"transform_edit.gizmo.{gizmoKey}");
		if (Buttons.DrawIconButtonHint(gizmoIcon, gizmoHint, iconBtnSize))
			this.Config.Editor_Gizmo = !show;

		// Transforms
		
		var target = this._editor.GetTransformTarget();
		ImGui.BeginDisabled(target is null);

		// Gizmo

		if (show) {
			var width = TransformTable.CalcWidth();
			DrawGizmo(target, width);
			ImGui.Spacing();
		} ImGui.Spacing();

		// Table
		// TODO: World/Local switch

		var local = target as ITransformLocal;

		var trans = local?.GetLocalTransform() ?? target?.GetTransform() ?? new Transform();

		this.Table.Operation = this.Config.Gizmo_Op;
		if (this.Table.Draw(ref trans)) {
			if (local is not null)
				local.SetLocalTransform(trans);
			else
				target?.SetTransform(trans);
		}

		// End

		ImGui.EndDisabled();
	}

	private unsafe void DrawGizmo(ITransform? world, float width) {
		if (this.Gizmo is null)
			return;

		var camera = this._camera.GetGameCamera();
		var cameraFov = camera != null ? camera->FoV : 1f;
		var cameraPos = camera != null ? (Vector3)camera->CameraBase.SceneCamera.Object.Position : Vector3.Zero;

		var pos = ImGui.GetCursorScreenPos();
		var size = new Vector2(width, width);

		this.Gizmo.Begin(size);
		this.Gizmo.Mode = this.Config.Gizmo_Mode;

		if (world?.GetMatrix() is Matrix4x4 matrix) {
			ImGui.GetWindowDrawList().AddCircleFilled(pos + size / 2, (width * Gizmo2D.ScaleFactor) / 2.05f, 0xCF202020);

			this.Gizmo.SetLookAt(cameraPos, matrix.Translation, cameraFov);
			if (this.Gizmo.Manipulate(ref matrix, out var delta))
				this._editor.Manipulate(world, matrix, delta);
		}

		this.Gizmo.End();
	}
}