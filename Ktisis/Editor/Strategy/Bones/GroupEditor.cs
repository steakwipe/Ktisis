using System.Collections.Generic;
using System.Linq;

using Ktisis.Editor.Strategy.Types;
using Ktisis.Scene.Entities.Skeleton;

namespace Ktisis.Editor.Strategy.Bones;

public class GroupEditor : BaseEditor, IVisibility {
	private readonly SkeletonGroup Group;
	
	public bool Visible {
		get => this.RecurseVisible().All(vis => vis.Visible);
		set {
			foreach (var child in this.RecurseVisible())
				child.Visible = value;
		}
	}

	public GroupEditor(
		SkeletonGroup group
	) {
		this.Group = group;
	}

	private IEnumerable<IVisibility> RecurseVisible()
		=> this.Group.Children.Select(child => child.GetEdit())
			.Where(child => child is IVisibility)
			.Cast<IVisibility>();
}