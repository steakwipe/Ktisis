﻿using System;
using System.Numerics;

using ImGuiNET;

using Dalamud.Interface;

using Ktisis.Interop;
using Ktisis.Structs.FFXIV;

namespace Ktisis.Overlay {
	public static class OverlayWindow {
		// Rendering

		public unsafe static WorldMatrix* WorldMatrix;

		public static ImGuiIOPtr Io;
		public static Vector2 Wp;

		// hacky solution until we push these to clientstructs

		public unsafe static Matrix4x4 GetProjectionMatrix() {
			var camera = (IntPtr)Services.Camera->Camera;
			return *(Matrix4x4*)(*(IntPtr*)(camera + 240) + 80);
		}
		public unsafe static Matrix4x4 GetViewMatrix() {
			var camera = (IntPtr)Services.Camera->Camera;
			var view = *(Matrix4x4*)(camera + 0xB0);
			view.M44 = 1;
			return view;
		}

		// Gizmo

		public static bool HasBegun = false;

		public static Gizmo Gizmo = new();
		public static string? GizmoOwner = null;

		public static bool IsGizmoVisible => Gizmo != null && GizmoOwner != null;

		public static bool IsGizmoOwner(string? id) => GizmoOwner == id;
		public static Gizmo? SetGizmoOwner(string? id) {
			GizmoOwner = id;
			return id == null ? null : Gizmo;
		}
		public static Gizmo? GetGizmo(string? id) => IsGizmoOwner(id) ? Gizmo : null;

		public static void Begin() {
			if (HasBegun) return;

			ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(0, 0));

			ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
			ImGui.Begin("Ktisis Overlay", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs);

			Io = ImGui.GetIO();
			ImGui.SetWindowSize(Io.DisplaySize);

			Wp = ImGui.GetWindowPos();
			Gizmo.BeginFrame(Wp, Io);

			HasBegun = true;

			if (Selection.DrawQueue.Count >= 1000) // something *probably* fucked up (thrown error in Selection.Draw?)
				Selection.DrawQueue.Clear(); // don't let it get worse
		}

		public static void End() {
			if (!HasBegun) return;
			ImGui.End();
			ImGui.PopStyleVar();
			HasBegun = false;
		}

		public unsafe static void Draw() {
			if (WorldMatrix == null)
				WorldMatrix = Methods.GetMatrix!();

			// Might need a different name for Begin?

			if (IsGizmoVisible)
				Begin();

			Skeleton.BoneSelect.Active = false;
			Skeleton.BoneSelect.Update = false;
			if (Ktisis.Configuration.ShowSkeleton) {
				Begin();
				Skeleton.Draw();
			}

			if (Selection.DrawQueue.Count > 0) {
				Begin();
				Selection.Draw();
			}

			End();
		}
	}
}