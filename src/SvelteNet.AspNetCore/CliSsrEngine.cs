namespace SvelteNet.AspNetCore;

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;

/// <summary>Shared process transport for the Node.js and Bun SSR backends.</summary>
public abstract class CliSsrEngine : ISvelteSsrEngine
{
	private const string ResultMarker = "__SVELTENET_RESULT__";
	private const string Runner = """
		import { pathToFileURL } from 'node:url';

		let input = '';
		process.stdin.setEncoding('utf8');
		for await (const chunk of process.stdin) input += chunk;
		const request = JSON.parse(input);

		if (request.baseUrl && globalThis.fetch) {
			const nativeFetch = globalThis.fetch;
			globalThis.fetch = (input, init = {}) => {
				const target = typeof input === 'string' || input instanceof URL
					? new URL(input, request.baseUrl)
					: input;
				const headers = new Headers(input instanceof Request ? input.headers : undefined);
				for (const [name, value] of Object.entries(init.headers ?? {})) headers.set(name, value);
				for (const [name, value] of Object.entries(request.headers ?? {})) {
					if (!headers.has(name)) headers.set(name, value);
				}
				return nativeFetch(target, { ...init, headers });
			};
		}

		const [component, renderer] = await Promise.all([
			import(pathToFileURL(request.componentPath).href),
			import(pathToFileURL(request.renderPath).href)
		]);
		const props = request.propsJson == null ? undefined : JSON.parse(request.propsJson);
		const result = await renderer.renderComponent(component.default, props);
		process.stdout.write('__SVELTENET_RESULT__' + Buffer.from(JSON.stringify(result)).toString('base64'));
		""";

	private readonly SvelteOptions _options;
	private readonly IHttpContextAccessor? _httpContextAccessor;
	private readonly IServer? _server;
	private readonly string _executablePath;
	private readonly TimeSpan _timeout;
	private readonly IReadOnlyList<string> _arguments;
	private readonly string _backendName;
	private readonly Uri? _baseUrl;
	private readonly IReadOnlySet<string> _forwardHeaders;

	protected CliSsrEngine(
		SvelteOptions options,
		IHttpContextAccessor? httpContextAccessor,
		IServer? server,
		string executablePath,
		TimeSpan timeout,
		string backendName,
		Uri? baseUrl,
		IEnumerable<string> forwardHeaders,
		params string[] arguments)
	{
		if (string.IsNullOrWhiteSpace(executablePath))
			throw new ArgumentException("An executable path is required.", nameof(executablePath));
		if (timeout <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(timeout), "The SSR timeout must be greater than zero.");

		_options = options;
		_httpContextAccessor = httpContextAccessor;
		_server = server;
		_executablePath = executablePath;
		_timeout = timeout;
		_backendName = backendName;
		_baseUrl = baseUrl;
		_forwardHeaders = new HashSet<string>(forwardHeaders, StringComparer.OrdinalIgnoreCase);
		_arguments = arguments;

		VerifyCli();
	}

	public SsrResult Render(
		string componentModule,
		string renderModule,
		string? propsJson,
		CancellationToken cancellationToken = default)
	{
		var serverDirectory = Path.GetFullPath(Path.Combine(_options.ContentRoot, _options.ServerOutput));
		var request = new CliRenderRequest
		{
			ComponentPath = ResolveModule(serverDirectory, componentModule),
			RenderPath = ResolveModule(serverDirectory, renderModule),
			PropsJson = propsJson
		};
		AddRequestContext(request);

		using var process = CreateProcess();
		try
		{
			if (!process.Start())
				throw new InvalidOperationException($"{_backendName} SSR failed to start '{_executablePath}'.");
		}
		catch (Win32Exception exception)
		{
			throw MissingCliException(exception);
		}

		var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
		process.StandardInput.Write(JsonSerializer.Serialize(request, SvelteJson.Options));
		process.StandardInput.Close();

		using var timeout = new CancellationTokenSource(_timeout);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
		try
		{
			process.WaitForExitAsync(linked.Token).GetAwaiter().GetResult();
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			TryKill(process);
			throw new TimeoutException($"{_backendName} SSR exceeded its {_timeout.TotalSeconds:g}-second timeout.");
		}
		catch
		{
			TryKill(process);
			throw;
		}

		var output = standardOutput.GetAwaiter().GetResult();
		var error = standardError.GetAwaiter().GetResult();
		if (process.ExitCode != 0)
			throw new InvalidOperationException(
				$"{_backendName} SSR exited with code {process.ExitCode}: {error.Trim()}");

		var marker = output.LastIndexOf(ResultMarker, StringComparison.Ordinal);
		if (marker < 0)
			throw new InvalidOperationException(
				$"{_backendName} SSR returned no render result. Output: {output.Trim()}");

		try
		{
			var base64 = output[(marker + ResultMarker.Length)..].Trim();
			var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
			return JsonSerializer.Deserialize<SsrResult>(json, SvelteJson.Options)
				?? throw new JsonException("The SSR result was empty.");
		}
		catch (Exception exception) when (exception is FormatException or JsonException)
		{
			throw new InvalidOperationException($"{_backendName} SSR returned an invalid render result.", exception);
		}
	}

	private Process CreateProcess()
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = _executablePath,
			UseShellExecute = false,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		foreach (var argument in _arguments) startInfo.ArgumentList.Add(argument);
		startInfo.ArgumentList.Add(Runner);
		return new Process { StartInfo = startInfo };
	}

	private void VerifyCli()
	{
		using var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = _executablePath,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				ArgumentList = { "--version" }
			}
		};

		try
		{
			if (!process.Start()) throw MissingCliException();
			if (!process.WaitForExit(5_000))
			{
				TryKill(process);
				throw new InvalidOperationException(
					$"{_backendName} SSR could not verify '{_executablePath}' because its version check timed out.");
			}
			if (process.ExitCode != 0) throw MissingCliException();
		}
		catch (Win32Exception exception)
		{
			throw MissingCliException(exception);
		}
	}

	private InvalidOperationException MissingCliException(Exception? innerException = null)
	{
		return new InvalidOperationException(
			$"{_backendName} SSR requires the '{_executablePath}' CLI. Install it and ensure it is available on PATH, or select a different renderer.",
			innerException);
	}

	private void AddRequestContext(CliRenderRequest request)
	{
		var context = _httpContextAccessor?.HttpContext;
		request.BaseUrl = ResolveBaseUrl()?.ToString();
		if (context is null || request.BaseUrl is null) return;

		var httpRequest = context.Request;
		foreach (var header in _forwardHeaders)
		{
			if (httpRequest.Headers.TryGetValue(header, out var value))
				request.Headers[header] = value.ToString();
		}
	}

	private Uri? ResolveBaseUrl() => ResolveBaseUrl(_baseUrl, _server);

	internal static Uri? ResolveBaseUrl(Uri? configuredBaseUrl, IServer? server)
	{
		if (configuredBaseUrl is not null) return EnsureTrailingSlash(configuredBaseUrl);

		var addresses = server?.Features.Get<IServerAddressesFeature>()?.Addresses;
		if (addresses is null) return null;
		foreach (var address in addresses)
		{
			if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) continue;
			var builder = new UriBuilder(uri);
			if (builder.Host is "0.0.0.0" or "[::]" or "::" or "*" or "+")
				builder.Host = "127.0.0.1";
			return EnsureTrailingSlash(builder.Uri);
		}
		return null;
	}

	private static Uri EnsureTrailingSlash(Uri uri)
	{
		if (!uri.IsAbsoluteUri)
			throw new InvalidOperationException("The CLI SSR BaseUrl must be an absolute URI.");
		if (uri.Scheme is not ("http" or "https"))
			throw new InvalidOperationException("The CLI SSR BaseUrl must use HTTP or HTTPS.");
		return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
			? uri
			: new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);
	}

	private static string ResolveModule(string serverDirectory, string module)
	{
		var path = Path.GetFullPath(Path.Combine(serverDirectory, module));
		var relative = Path.GetRelativePath(serverDirectory, path);
		if (Path.IsPathRooted(relative)
			|| relative.Equals("..", StringComparison.Ordinal)
			|| relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			throw new InvalidOperationException($"SSR module '{module}' resolves outside the server output directory.");
		return path;
	}

	private static void TryKill(Process process)
	{
		try
		{
			if (!process.HasExited) process.Kill(entireProcessTree: true);
		}
		catch (InvalidOperationException)
		{
			// The process exited between the HasExited check and Kill.
		}
	}

	private sealed class CliRenderRequest
	{
		public required string ComponentPath { get; init; }
		public required string RenderPath { get; init; }
		public string? PropsJson { get; init; }
		public string? BaseUrl { get; set; }
		public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
	}
}
