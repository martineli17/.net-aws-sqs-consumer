using Amazon.SQS;
using Amazon.SQS.Model;
using ConsumerQueue.DTO;
using Microsoft.AspNetCore.Mvc;
using ProcessQueue.DTO;
using System.Text.Json;

namespace ConsumerQueue.Controllers
{
    [ApiController]
    [Route("queue")]
    public class QueueController : ControllerBase
    {
        private readonly MessageProcessedDTO _messageProcessedDTO;
        private readonly AwsSettingsDTO _awsSettings;

        public QueueController(MessageProcessedDTO messageProcessedDTO, AwsSettingsDTO awsSettings)
        {
            _messageProcessedDTO = messageProcessedDTO;
            _awsSettings = awsSettings;
        }

        [HttpPost]
        public async Task<IActionResult> AddMessage([FromQuery] bool toDeadLetter)
        {
            var awsCredentials = new AmazonSQSConfig
            {
                ServiceURL = _awsSettings.ServerEndpoint
            };
            var sqsClient = new AmazonSQSClient(awsCredentials);
            var content = new MessageDTO { PublishedAt = DateTime.Now, ToDeadLetter = toDeadLetter };
            var messageRequest = new SendMessageRequest()
            {
                MessageBody = JsonSerializer.Serialize(content),
                QueueUrl = $"{_awsSettings.QueueEndpoint}/queue"
            };
            await sqsClient.SendMessageAsync(messageRequest);

            return Ok();    
        }

        [HttpGet]
        public IActionResult GetMessagesProcessed()
        {
            return Ok(_messageProcessedDTO);
        }
    }
}
