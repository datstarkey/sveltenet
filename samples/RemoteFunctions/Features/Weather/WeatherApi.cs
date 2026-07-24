namespace RemoteFunctions.Features.Weather;

using SvelteNet;

public record WeatherForecast(string Day, int TemperatureC, string Summary);

/// <summary>A second vertical remote service, producing WeatherApi.remote.ts.</summary>
[SvelteRemote]
public class WeatherApi
{
	private static readonly WeatherForecast[] Forecasts =
	[
		new("Today", 18, "Bright"),
		new("Tomorrow", 15, "Cloudy"),
		new("Friday", 12, "Showers")
	];

	[Query]
	public IReadOnlyList<WeatherForecast> GetForecasts() => Forecasts;

	[Command]
	public string RefreshForecasts() => $"Refreshed at {DateTimeOffset.UtcNow:HH:mm:ss}";
}
