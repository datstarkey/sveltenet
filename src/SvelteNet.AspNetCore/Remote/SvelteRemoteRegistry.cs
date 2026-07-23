namespace SvelteNet.AspNetCore.Remote;

using SvelteNet.Remote;

/// <summary>
/// Maps service route names to their [SvelteRemote] descriptors — source-generated
/// where available, reflection fallback otherwise.
/// </summary>
public sealed class SvelteRemoteRegistry
{
	private readonly Dictionary<string, RemoteServiceDescriptor> _services;

	public SvelteRemoteRegistry(IEnumerable<RemoteServiceDescriptor> descriptors)
	{
		_services = descriptors.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
	}

	public SvelteRemoteRegistry(IEnumerable<Type> serviceTypes)
		: this(serviceTypes.Select(SvelteRemoteDescriptors.For))
	{
	}

	public IReadOnlyCollection<RemoteServiceDescriptor> Services => _services.Values;

	public bool TryGet(string service, string method, out RemoteServiceDescriptor serviceDescriptor, out RemoteMethodDescriptor methodDescriptor)
	{
		methodDescriptor = null!;
		if (!_services.TryGetValue(service, out serviceDescriptor!)) return false;
		var found = serviceDescriptor.Methods.FirstOrDefault(m => m.Name.Equals(method, StringComparison.OrdinalIgnoreCase));
		if (found is null) return false;
		methodDescriptor = found;
		return true;
	}
}
