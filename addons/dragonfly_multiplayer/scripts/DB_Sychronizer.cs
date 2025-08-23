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
	[Export]
	public SyncMode syncMode = SyncMode.Process; // Selectable in inspector
	[Export]
	public Godot.Collections.Dictionary<Node, string[]> syncNodes = new Godot.Collections.Dictionary<Node, string[]>();
		
	private ConcurrentDictionary<string, Variant> lastSynced = new ConcurrentDictionary<string, Variant>();

	private DB_Client client;
	private string playerId;
	private float timeAccumulator = 0f;

	public override async void _Ready()
	{
		try
		{
			client = GetNode<DB_Client>("/root/DbClient");
			playerId = client.playerId;
			await client.Subscribe($"sync:{playerId}", OnRemoteUpdate);
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
			
		Step((float)delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (syncMode != SyncMode.PhysicsProcess)
			return;
			
		Step((float)delta);
	}
	
	public override void _ExitTree()
	{
		client.KeyDelete($"player:{playerId}:data");
	}

	private void Step(float delta)
	{
		timeAccumulator += delta;
		if (timeAccumulator >= syncInterval)
		{
			timeAccumulator = 0f;
			SyncLocalProperties();
		}
	}

	private void SyncLocalProperties()
	{
		foreach (var kv in syncNodes)
		{
			Node node = kv.Key;

			foreach (string prop in kv.Value)
			{
				if (!node.HasMethod("get")) continue; 
				Variant value = node.Get(prop);
				string key = $"{node.Name}:{prop}";
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

	private void OnRemoteUpdate(RedisChannel channel, RedisValue message)
	{
		// Example: object:123:Player:position:10,20,0
		GD.Print(message);
		
	}
}
