using Amazon.SQS;
using Amazon.SQS.Model;
using ConsumerQueue.DTO;
using ProcessQueue.DTO;
using System.Text.Json;

namespace ConsumerQueue.BackgroundServices
{
    public class ConsumerBackgroundService : BackgroundService
    {
        private readonly AmazonSQSClient _sqsClient;
        private readonly MessageProcessedDTO _messageProcessedDTO;
        private readonly AwsSettingsDTO _awsSettings;

        public ConsumerBackgroundService(MessageProcessedDTO messageProcessedDTO, AwsSettingsDTO awsSettings)
        {
            var awsCredentials = new AmazonSQSConfig
            {
                ServiceURL = _awsSettings.ServerEndpoint,
            };
            _sqsClient = new AmazonSQSClient(awsCredentials);
            _messageProcessedDTO = messageProcessedDTO;
            _awsSettings = awsSettings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ConsumerQueue(stoppingToken);
            await ConsumerDeadLetter(stoppingToken);
        }

        #region Private Methods

        private Task ConsumerQueue(CancellationToken stoppingToken)
        {
            string queueUrl = $"{_awsSettings.QueueEndpoint}/queue";
            return Task.Factory.StartNew(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var request = new ReceiveMessageRequest()
                    {
                        QueueUrl = queueUrl,
                    };
                    var result = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);
                    if (result.Messages.Any())
                    {
                        await Task.Factory.StartNew(async () =>
                        {
                            var message = result.Messages.Single();
                            var content = JsonSerializer.Deserialize<MessageDTO>(message.Body);

                            if (content.ToDeadLetter)
                                throw new Exception("Sending to DL...");
                            else
                            {
                                _messageProcessedDTO.LastMessageReceived = content;
                                await _sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, stoppingToken);
                            }
                        });
                    }
                }
            }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default); ;
        }

        private Task ConsumerDeadLetter(CancellationToken stoppingToken)
        {
            string queueUrl = $"{_awsSettings.QueueEndpoint}/queue-dl";

            return Task.Factory.StartNew(async () =>
            {
                await _sqsClient.PurgeQueueAsync(queueUrl, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var request = new ReceiveMessageRequest()
                    {
                        QueueUrl = queueUrl,
                    };
                    var result = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);
                    if (result.Messages.Any())
                    {
                        var message = result.Messages.Single();
                        var content = JsonSerializer.Deserialize<MessageDTO>(message.Body);
                        _messageProcessedDTO.LastMessageDeadLetter = content;
                        
                        await _sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, stoppingToken);
                    }
                }
            }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        #endregion
    }
}
