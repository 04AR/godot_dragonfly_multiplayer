using Godot;
using System;
using StackExchange.Redis;
using System.Threading.Tasks;
using System.Collections.Concurrent;

public partial class DB_Messaging : Node
{
	[Export] public TextEdit logTextEdit;
	
	private DB_Client client;

	public override async void _Ready()
	{
		try
		{
			client = GetNode<DB_Client>("/root/DbClient");
			await client.Subscribe("game-chats", OnChatEvent);
		}
		catch (System.Exception)
		{

			throw;
		}
	}
	
	private void OnChatEvent(RedisChannel channel, RedisValue message)
	{
		//string[] msg = message.ToString().Split(':');
		
		GD.Print(message);

	}
	
	public void Message(string channel, string message)
	{
		client.Publish("game-events", message);
	}
}
