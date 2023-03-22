// See https://aka.ms/new-console-template for more information

using System.Runtime.CompilerServices;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using learn_aws;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Starting SQS locally");
using var localSQSFixture = await LocalStackFixture.Create(containerNamePrefix: "learn_aws", services: new[] { "sqs" });

IServiceCollection services = new ServiceCollection();

services.AddSingleton<IAmazonSQS>(sp => new AmazonSQSClient(
    new BasicAWSCredentials("ignored", "ignored"),
    new AmazonSQSConfig
    {
        ServiceURL = localSQSFixture.ServiceUrl.ToString()
    }));

IServiceProvider serviceProvider = services.BuildServiceProvider();

/// after some time

var sqsClient = serviceProvider.GetRequiredService<IAmazonSQS>();

var response = await sqsClient.CreateQueueAsync(new CreateQueueRequest
{
    Attributes =
    {
        { "FifoQueue", "true" },
        { "ContentBasedDeduplication", "true" },
    },
    QueueName = $"MyQueue-{Guid.NewGuid()}.fifo"
});

Console.WriteLine($"Queue created {response.QueueUrl}");