using System.Net;
using System.Net.Sockets;
using Ductus.FluentDocker.Common;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using Polly;

namespace learn_aws;

public static class FixtureUtils
{
    static FixtureUtils()
    {
        var env = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        IsRunningInContainer = env != null && env.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRunningInContainer { get; }

    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    internal static IContainerService TryGetExistingContainerService(string name, int containerPort)
    {
        var hosts  = new Hosts().Discover();
        var docker = hosts.FirstOrDefault(x => x.IsNative) ?? hosts.FirstOrDefault(x => x.Name == "default");

        var waitAndRetry = Policy.Handle<FluentDockerException>()
            .WaitAndRetry(30, _ => TimeSpan.FromMilliseconds(1000));

        return waitAndRetry.Execute(() =>
        {
            var containers = docker!.GetContainers();
            var container  = containers.Single(c => c.Name == name);
            container.WaitForPort($"{containerPort}/tcp", 5000);
            return container;
        });
    }

    internal static UriBuilder GetServiceUrl(IContainerService containerService)
    {
        var config          = containerService.GetConfiguration();
        var networkSettings = config.NetworkSettings;
        /*
         * see: https://docs.localstack.cloud/localstack/external-ports/
         */
        var exposedPort = networkSettings.Ports.First(x => x.Value != null);
        var hostPort    = exposedPort.Value.First().HostPort;

        var serviceUrl = new UriBuilder($"http://localhost:{hostPort}");
        return serviceUrl;
    }
}