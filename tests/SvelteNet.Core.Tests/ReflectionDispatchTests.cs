namespace SvelteNet.Core.Tests;

using SvelteNet.Remote;

/// <summary>
/// The reflection fallback must behave identically to the generated dispatchers, so its
/// return-value unwrapping is pinned here across every shape a handler can declare.
/// </summary>
public class ReflectionDispatchTests
{
	private sealed class Api
	{
		public bool Ran;

		[Command]
		public void Sync() => Ran = true;

		[Query]
		public int SyncValue() => 7;

		[Command]
		public async Task AsyncVoid()
		{
			await Task.Yield();
			Ran = true;
		}

		[Query]
		public async Task<int> AsyncValue()
		{
			await Task.Yield();
			return 11;
		}

		[Command]
		public ValueTask ValueTaskVoid()
		{
			Ran = true;
			return ValueTask.CompletedTask;
		}

		[Query]
		public ValueTask<string> ValueTaskValue() => ValueTask.FromResult("ok");
	}

	private static async Task<object?> Invoke(Api api, string method)
	{
		var descriptor = SvelteRemoteDescriptors.FromReflection(typeof(Api));
		var target = descriptor.Methods.Single(m => m.Name == method);
		return await target.Invoke(api, new RemoteArguments());
	}

	/// <summary>
	/// An `async Task` method hands back a Task&lt;VoidTaskResult&gt; at runtime, so
	/// unwrapping by runtime type would surface that internal struct as the payload
	/// instead of the 204 the client expects.
	/// </summary>
	[Theory]
	[InlineData("Sync")]
	[InlineData("AsyncVoid")]
	[InlineData("ValueTaskVoid")]
	public async Task Void_handlers_produce_no_payload(string method)
	{
		var api = new Api();

		Assert.Null(await Invoke(api, method));
		Assert.True(api.Ran);
	}

	[Theory]
	[InlineData("SyncValue", 7)]
	[InlineData("AsyncValue", 11)]
	public async Task Value_handlers_return_their_result(string method, int expected)
	{
		Assert.Equal(expected, await Invoke(new Api(), method));
	}

	[Fact]
	public async Task Value_task_handlers_return_their_result()
	{
		Assert.Equal("ok", await Invoke(new Api(), "ValueTaskValue"));
	}
}
