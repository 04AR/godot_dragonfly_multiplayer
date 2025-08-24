using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;

[GlobalClass]
//[Tool]
public partial class DB_Sychronizer : Node
{
	public enum SyncMode
	{
		Process,
		PhysicsProcess
	}

	[Export(PropertyHint.Range, "0,5")]
	public float syncInterval = 0.1f; // Sync every 0.1s
	[Export(PropertyHint.Range, "0,5")]
	public float replicateInterval = 0.1f;
	[Export]
	public SyncMode syncMode = SyncMode.Process; // Selectable in inspector
	[Export]
	public Godot.Collections.Dictionary<NodePath, string[]> syncNodes = new Godot.Collections.Dictionary<NodePath, string[]>();
		
	private ConcurrentDictionary<string, Variant> lastSynced = new ConcurrentDictionary<string, Variant>();

	private DB_Client client;
	private string playerId;
	private float syncTimeAccumulator = 0f;
	private float replicateTimeAccumulator = 0f;

	public override async void _Ready()
	{
		try
		{
			client = GetNode<DB_Client>("/root/DbClient");
			playerId = client.playerId;
			//await client.Subscribe($"sync:{playerId}", OnRemoteUpdate);
		}
		catch (System.Exception)
		{
			throw;
		}
	}

	public override void _Process(double delta)
	{
		if (syncMode != SyncMode.Process)
			return;
		
		SyncStep((float)delta);
		ReplicateStep((float) delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (syncMode != SyncMode.PhysicsProcess)
			return;
		
		SyncStep((float)delta);
		ReplicateStep((float) delta);
	}
	
	public override void _ExitTree()
	{
		client.KeyDelete($"player:{playerId}:data");
	}
	
	private void ReplicateStep(float delta)
	{
		replicateTimeAccumulator += delta;
		if (replicateTimeAccumulator >= replicateInterval)
		{
			replicateTimeAccumulator = 0f;
			ReplicateProperties();
		}
	}

	private void SyncStep(float delta)
	{
		syncTimeAccumulator += delta;
		if (syncTimeAccumulator >= syncInterval)
		{
			syncTimeAccumulator = 0f;
			SyncLocalProperties();
		}
	}

	private void SyncLocalProperties()
	{
		foreach (var kv in syncNodes)
		{
			Node node = GetNode(kv.Key);
			foreach (string prop in kv.Value)
			{
				if (!node.HasMethod("get")) continue; 
				Variant value = node.Get(prop);
				string key = $"{kv.Key}:{prop}";
				string strValue = value.ToString();
				if (!lastSynced.TryGetValue(key, out Variant oldValue) || oldValue.ToString() != strValue)
				{
					lastSynced[key] = strValue;
					client.HashSet($"player:{playerId}:data", key, strValue);
				}
			}
		}
		client.SetExpireTime($"player:{playerId}:data", 10);
	}
	
	private async void ReplicateProperties()
	{
		HashEntry[] data = await client.HashGetAll($"player:{playerId}:data");
		
		foreach (var entry in data)
		{
			string key = entry.Name;
			string val = entry.Value;
			
			GD.Print(entry);
			// Key format: "NodePath:Property"
			var parts = key.Split(':');
			if (parts.Length < 2) continue;

			string path = parts[0];
			string prop = parts[1];

			if (!HasNode(path)) continue;

			Node node = GetNode(path);
			//if (!node.Get(prop)) continue;

			// Convert string back to Variant if needed
			node.Set(prop, val);
		}
	}

	private void OnRemoteUpdate(RedisChannel channel, RedisValue message)
	{
		// Example: object:123:Player:position:10,20,0
		GD.Print(message);
		
	}
}
