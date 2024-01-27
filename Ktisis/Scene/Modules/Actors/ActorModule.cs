using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Dalamud.Plugin.Services;
using GameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;

using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

using Ktisis.Services;
using Ktisis.Structs.GPose;
using Ktisis.Interop.Hooking;
using Ktisis.Scene.Entities.Game;
using Ktisis.Common.Extensions;
using Ktisis.Scene.Types;
using Ktisis.Services.Game;

namespace Ktisis.Scene.Modules.Actors;

public class ActorModule : SceneModule {
	private readonly ActorService _actors;
	private readonly IClientState _clientState;
	private readonly IFramework _framework;
	private readonly GroupPoseModule _gpose;
	
	private readonly ActorSpawner _spawner;
	
	public ActorModule(
		IHookMediator hook,
		ISceneManager scene,
		ActorService actors,
		IClientState clientState,
		IFramework framework,
		GroupPoseModule gpose
	) : base(hook, scene) {
		this._actors = actors;
		this._clientState = clientState;
		this._framework = framework;
		this._gpose = gpose;
		this._spawner = hook.Create<ActorSpawner>();
	}

	public override void Setup() {
		foreach (var actor in this._actors.GetGPoseActors())
			this.AddActor(actor, false);
		this.EnableAll();
		this._spawner.TryInitialize();
	}
	
	// Spawning

	public Task<ActorEntity> Spawn() {
		var index = this._spawner.CalculateNextIndex();
		return this.Spawn($"Actor #{index}");
	}

	public async Task<ActorEntity> Spawn(string name) {
		var localPlayer = this._clientState.LocalPlayer;
		if (localPlayer == null)
			throw new Exception("Local player not found.");
		
		var address = await this._spawner.CreateActor(localPlayer);
		var entity = this.AddSpawnedActor(address);
		this.SetActorName(entity.Actor, name);
		entity.Actor.SetWorld((ushort)localPlayer.CurrentWorld.Id);
		this.ReassignParentIndex(entity.Actor);
		return entity;
	}

	public async Task<ActorEntity> AddFromOverworld(GameObject actor) {
		if (!this._spawner.IsInit)
			throw new Exception("Actor spawn manager is uninitialized.");
		var address = await this._spawner.CreateActor(actor);
		var entity = this.AddSpawnedActor(address);
		entity.Actor.SetTargetable(true);
		return entity;
	}

	private ActorEntity AddSpawnedActor(nint address) {
		var entity = this.AddActor(address, false);
		if (entity == null)
			throw new Exception("Failed to create entity for spawned actor.");
		entity.IsManaged = true;
		return entity;
	}
	
	// Deletion
	
	public unsafe void Delete(ActorEntity actor) {
		if (this._gpose.IsPrimaryActor(actor)) {
			Ktisis.Log.Warning("Refusing to delete primary actor.");
			return;
		}
		
		var gpose = this._gpose.GetGPoseState();
		if (gpose == null) return;

		var gameObject = (CSGameObject*)actor.Actor.Address;
		this._framework.RunOnFrameworkThread(() => {
			var mgr = ClientObjectManager.Instance();
			var index = (ushort)mgr->GetIndexByObject(gameObject);
			this._removeCharacter(gpose, gameObject);
			if (index != ushort.MaxValue)
				mgr->DeleteObjectByIndex(index, 1);
		});
		
		actor.Remove();
	}
	
	// Actor state
	
	public unsafe void SetActorName(GameObject gameObject, string name) {
		var gameObjectPtr = (CSGameObject*)gameObject.Address;
		if (gameObjectPtr == null) return;

		var setName = name;
		
		var dupeCt = 0;
		var isNameDupe = true;
		while (isNameDupe) {
			isNameDupe = false;
			foreach (var actor in this._actors.GetGPoseActors()) {
				if (actor.GetNameOrFallback() != setName) continue;
				setName = $"{name} {++dupeCt + 1}";
				isNameDupe = true;
			}
		}

		var bytes = Encoding.UTF8.GetBytes(setName).Append((byte)0).ToArray();
		for (var i = 0; i < bytes.Length; i++)
			gameObjectPtr->Name[i] = bytes[i];
	}

	private void ReassignParentIndex(GameObject gameObject) {
		var ipcMgr = this.Scene.Context.Plugin.Ipc;
		if (!ipcMgr.IsPenumbraActive) return;

		var ipc = ipcMgr.GetPenumbraIpc();
		ipc.SetAssignedParentIndex(gameObject, gameObject.ObjectIndex);
	}	
	
	// Entities

	private ActorEntity? AddActor(nint address, bool addCompanion) {
		var actor = this._actors.GetAddress(address);
		if (actor is { ObjectIndex: not 200 })
			return this.AddActor(actor, addCompanion);
		Ktisis.Log.Warning($"Actor address at 0x{address:X} is invalid.");
		return null;
	}

	private ActorEntity? AddActor(GameObject actor, bool addCompanion) {
		if (!actor.IsValid()) {
			Ktisis.Log.Warning($"Actor address at 0x{actor.Address:X} is invalid.");
			return null;
		}
		
		var result = this.Scene.Factory.BuildActor(actor).Add();
		if (addCompanion)
			this.AddCompanion(actor);
		return result;
	}

	private unsafe void AddCompanion(GameObject owner) {
		var chara = (Character*)owner.Address;
		if (chara == null || chara->CompanionObject == null) return;
		
		var actor = this._actors.GetAddress((nint)chara->CompanionObject);
		if (actor is null or { ObjectIndex: 0 } || !actor.IsValid()) return;
		
		this.Scene.Factory.BuildActor(actor).Add();
	}
	
	// Hooks
	
	[Signature("E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 80 BE ?? ?? ?? ?? ??", DetourName = nameof(AddCharacterDetour))]
	private Hook<AddCharacterDelegate>? AddCharacterHook = null!;
	private delegate void AddCharacterDelegate(nint a1, nint a2, ulong a3);

	private void AddCharacterDetour(nint gpose, nint address, ulong id) {
		this.AddCharacterHook!.Original(gpose, address, id);
		if (!this.CheckValid()) return;
		
		try {
			if (id != 0xE0000000)
				this.AddActor(address, true);
		} catch (Exception err) {
			Ktisis.Log.Error($"Failed to handle character add for 0x{address:X}:\n{err}");
		}
	}

	[Signature("45 33 D2 4C 8D 81 ?? ?? ?? ?? 41 8B C2 4C 8B C9 49 3B 10")]
	private RemoveCharacterDelegate _removeCharacter = null!;
	private unsafe delegate nint RemoveCharacterDelegate(GPoseState* gpose, CSGameObject* gameObject);
	
	// Disposal

	public override void Dispose() {
		base.Dispose();
		this._spawner.Dispose();
		GC.SuppressFinalize(this);
	}
}
