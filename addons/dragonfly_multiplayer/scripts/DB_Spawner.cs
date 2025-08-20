using Godot;
using System;
using StackExchange.Redis;
using System.Threading.Tasks;
using System.Collections.Concurrent;

public partial class DB_Spawner : Node
{
	[Export] public PackedScene scene;
	[Export] public Node rootNode;
	[Export] public TextEdit logTextEdit;

	private DB_Client client;
	private string playerId;

	private string[] activePlayer;

	private ConcurrentDictionary<string, Node> spawnedPlayers = new ConcurrentDictionary<string, Node>();

	[Signal] public delegate void PlayerJoinedEventHandler(string playerId);
	[Signal] public delegate void PlayerLeftEventHandler(string playerId);

	public override async void _Ready()
	{
		try
		{
			client = GetNode<DB_Client>("/root/DbClient");
			playerId = client.playerId;
			logTextEdit.Text = playerId;
			await client.Subscribe("game-events", OnGameEvent);
			AddPlayerToActiveSet();
			await GetActivePlayers();

		}
		catch (System.Exception)
		{

			throw;
		}

	}

	private void OnGameEvent(RedisChannel channel, RedisValue message)
	{
		// Example: messages like "join:42" or "leave:42"
		string[] msg = message.ToString().Split(':');

		switch (msg[0])
		{
			case "player_joined":
				SpawnPlayer(msg[1]);
				GD.Print($"[DB_Spawner] Player {msg[1]} joined.");
				// EmitSignal(nameof(PlayerJoinedEventHandler), msg[1]);
				break;
			case "player_left":
				DepspawnPlayer(msg[1]);
				GD.Print($"[DB_Spawner] Player {msg[1]} left.");
				// EmitSignal(nameof(PlayerLeftEventHandler), msg[1]);
				break;
		}

	}

	private void SpawnPlayer(string peerId)
	{
		if (scene == null || rootNode == null || peerId == playerId)
			return;

		Node playerInstance = scene.Instantiate();
		playerInstance.Name = $"Player_{peerId}";

		// Thread-safe add to dictionary
		if (spawnedPlayers.TryAdd(peerId, playerInstance))
		{
			rootNode.CallDeferred(Node.MethodName.AddChild, playerInstance);
			GD.Print($"Spawned player instance with ID: {peerId}");
		}
		else
		{
			GD.PrintErr($"Player {peerId} is already spawned!");
		}
	}

	private void DepspawnPlayer(string peerId)
	{
		if (spawnedPlayers.TryRemove(peerId, out Node playerNode))
		{
			if (IsInstanceValid(playerNode))
			{
				playerNode.QueueFree();
				GD.Print($"Despawned player instance with ID: {peerId}");
			}
		}
		else
		{
			GD.PrintErr($"No player instance found with ID: {peerId}");
		}
	}

	public override void _ExitTree()
	{
		RemovePlayerFromActiveSet();
	}

	private async Task GetActivePlayers()
	{
		activePlayer = await client.SetGetAll("players:active");
		GD.Print($"Active players: {string.Join(", ", activePlayer)}");

		foreach (var players in activePlayer)
		{
			if (players == playerId)
			{
				GD.Print($"Skipping self: {players}");
				continue;
			}
			SpawnPlayer(players);
			GD.Print($"Spawned existing player: {players}");
		}
	}

	private void AddPlayerToActiveSet()
	{
		client.SetAdd("players:active", playerId);
		BroadcastPlayerJoined();
		// GD.Print($"🧑‍🚀 Added {playerId} to active players");
	}

	private void RemovePlayerFromActiveSet()
	{
		client.SetRemove("players:active", playerId);
		BroadcastPlayerLeft();
		// GD.Print($"👋 Removed {playerId} from active players");
	}

	private void BroadcastPlayerJoined()
	{
		string message = $"player_joined:{playerId}";
		client.Publish("game-events", message);
		// GD.Print($"📣 Published event: {message}");
	}

	private void BroadcastPlayerLeft()
	{
		string message = $"player_left:{playerId}";
		client.Publish("game-events", message);
		// GD.Print($"📣 Published event: {message}");
	}
}
