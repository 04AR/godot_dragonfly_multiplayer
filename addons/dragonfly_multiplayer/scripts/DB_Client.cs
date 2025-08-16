using Godot;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using System.Linq;

public partial class DB_Client : Node
{
	public static DB_Client Instance { get; private set; }

	private ConnectionMultiplexer redis;
	private ISubscriber subscriber;
	private IDatabase database;

	public override async void _Ready()
	{
		Instance = this;
		redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
		subscriber = redis.GetSubscriber();
		database = redis.GetDatabase();
		GD.Print("RedisClient singleton initialized.");
	}

	// Pub/Sub: Publish to a channel
	public async Task Publish(string channel, string message)
	{
		await subscriber.PublishAsync(channel, message);
	}

	// Pub/Sub: Subscribe to a channel with callback
	public async Task Subscribe(string channel, Action<RedisChannel, RedisValue> callback)
	{
		await subscriber.SubscribeAsync(channel, callback);
	}

	// Sets: Add to set (for persistent lists, e.g., player info)
	public async Task SetAdd(string key, string value)
	{
		await database.SetAddAsync(key, value);
	}

	// Sets: Get all members
	public async Task<string[]> SetGetAll(string key)
	{
		RedisValue[] values = await database.SetMembersAsync(key);
		return values.Select(v => v.ToString()).ToArray();
	}

	// Hashes: Set a field-value (for object properties)
	public async Task HashSet(string key, string field, string value)
	{
		await database.HashSetAsync(key, field, value);
	}

	// Hashes: Get a field value
	public async Task<string> HashGet(string key, string field)
	{
		return await database.HashGetAsync(key, field);
	}

	// Hashes: Set multiple fields
	public async Task HashSetMultiple(string key, HashEntry[] entries)
	{
		await database.HashSetAsync(key, entries);
	}

	// Hashes: Get all fields
	public async Task<HashEntry[]> HashGetAll(string key)
	{
		return await database.HashGetAllAsync(key);
	}
}
