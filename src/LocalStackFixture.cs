using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Common;
using Ductus.FluentDocker.Services;

namespace learn_aws;

public class LocalStackFixture: IDisposable
{
    private const    int               ContainerPort = 4566;
    private readonly IContainerService _containerService;
    
    private LocalStackFixture(IContainerService containerService, Uri serviceUrl)
    {
        _containerService = containerService;
        ServiceUrl = serviceUrl;
    }

    public static Task<LocalStackFixture> Create(string containerNamePrefix,
        string[] services,
        string imageTag = "latest",
        int port = 0)
    {
        return Task.Run(() =>
        {
            IContainerService containerService;
            var               name       = $"{containerNamePrefix}-localstack";
            var               serviceEnv = string.Join(',', services.Select(s => s.ToString()));
            try
            {
                containerService = new Builder()
                    .UseContainer()
                    .WithName(name)
                    .UseImage($"localstack/localstack:{imageTag}")
                    .ReuseIfExists()
                    .ExposePort(port, ContainerPort)
                    .WithEnvironment("LS_LOG=debug")
                    .WithEnvironment($"SERVICES={serviceEnv}")
                    .WaitForPort($"{ContainerPort}/tcp", TimeSpan.FromSeconds(10))
                    .Build();
                containerService.Start();
            }
            catch (FluentDockerException ex) when (ex.Message.Contains("Error response from daemon: Conflict"))
            {
                // This can happen in a container startup race condition and parallel tests.
                // Assume the container is already running.
                containerService = FixtureUtils.TryGetExistingContainerService(name, ContainerPort);
            }

            var serviceUrl = FixtureUtils.GetServiceUrl(containerService);

            if (!FixtureUtils.IsRunningInContainer)
            {
                return new LocalStackFixture(containerService, serviceUrl.Uri);
            }
            // When tests are running in container, the networking setup is different.
            // Instead of host -> container, we have container -> container so
            // localhost won't work as host networking does not apply
            var host = containerService
                .GetConfiguration()
                .NetworkSettings
                .IPAddress;

            serviceUrl.Host = host;
            serviceUrl.Port = ContainerPort;

            return new LocalStackFixture(containerService, serviceUrl.Uri);
        });
    }
    
    public Uri ServiceUrl { get; }
    
    public void Dispose() => _containerService.Dispose();
}