using Dalamud.Interface.Utility.Raii;

using ImGuiNET;

using Ktisis.Data.Files;
using Ktisis.Editor.Characters;
using Ktisis.Editor.Context;
using Ktisis.Editor.Context.Types;
using Ktisis.Interface.Components.Files;
using Ktisis.Interface.Types;
using Ktisis.Scene.Entities.Game;

namespace Ktisis.Interface.Windows.Import;

public class CharaImportDialog : EntityEditWindow<ActorEntity> {
	private readonly IEditorContext _ctx;
	
	private readonly FileSelect<CharaFile> _select;

	public CharaImportDialog(
		IEditorContext ctx,
		FileSelect<CharaFile> select
	) : base(
		"Import Appearance",
		ctx,
		ImGuiWindowFlags.AlwaysAutoResize
	) {
		this._ctx = ctx;
		this._select = select;
		this._select.OpenDialog = this.OnFileDialogOpen;
	}
	
	private void OnFileDialogOpen(FileSelect<CharaFile> sender) {
		this._ctx.Interface.OpenCharaFile(sender.SetFile);
	}
	
	public override void Draw() {
		ImGui.Text($"Importing appearance for {this.Target.Name}");
		ImGui.Spacing();
		
		this._select.Draw();
		ImGui.Spacing();
		this.DrawCharaApplication();
	}

	private void DrawCharaApplication() {
		using var _ = ImRaii.Disabled(!this._select.IsFileOpened);
		
		this.DrawModesSelect();
		ImGui.Spacing();
		ImGui.Spacing();
		if (ImGui.Button("Apply"))
			this.ApplyCharaFile();
		ImGui.Spacing();
	}

	private void DrawModesSelect() {
		ImGui.Text("Appearance");
		this.DrawModeSwitch("Body", SaveModes.AppearanceBody);
		ImGui.SameLine();
		this.DrawModeSwitch("Face", SaveModes.AppearanceFace);
		ImGui.SameLine();
		this.DrawModeSwitch("Hair", SaveModes.AppearanceHair);
		
		ImGui.Spacing();
		
		ImGui.Text("Equipment");
		this.DrawModeSwitch("Gear", SaveModes.EquipmentGear);
		ImGui.SameLine();
		this.DrawModeSwitch("Accessories", SaveModes.EquipmentAccessories);
		ImGui.SameLine();
		this.DrawModeSwitch("Weapons", SaveModes.EquipmentWeapons);
	}

	private void DrawModeSwitch(string label, SaveModes mode) {
		var enabled = this._ctx.Config.File.ImportCharaModes.HasFlag(mode);
		if (ImGui.Checkbox($"{label}##CharaImportDialog_{mode}", ref enabled))
			this._ctx.Config.File.ImportCharaModes ^= mode;
	}
	
	// Apply chara

	private void ApplyCharaFile() {
		var file = this._select.Selected?.File;
		if (file == null) return;
		this.Context.Characters.ApplyCharaFile(this.Target, file, this._ctx.Config.File.ImportCharaModes);
	}
}
