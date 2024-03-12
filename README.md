# Publicando e consumindo mensagens utilizando SQS, .NET e terraform
#### Neste exemplo, foi utilizado o serviço SQS da AWS Cloud para simular o consumo e adição de mensagens. 
Além disso, foi utilizado o LcoalStack, uma ferramente que simula os serviços da AWS na sua máquina local. Para mais informações, consulte o site oficial: https://www.localstack.cloud/


## Terraform
#### Neste repositório, se encontra alguns arquivos terraform responsáveis pela configuração do ambiente AWS e também pela configuração das filas SQS utilizadas para este exemplo.
Inicialmente, no arquivo [main.tf](https://github.com/martineli17/.net-aws-sqs-consumer/blob/master/Sqs/main.tf), é definido as informações sobre o provider utilizado:
```hcl
terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "5.35.0"
    }
  }

  backend "local" {
    path = "./state.tfstate"
  }
}
```
Logo abaixo, há as configurações referente a este provider. Como foi utilizado o LocalStack, existe algumas definições mais específicas para o correto funcionamento, como a definição dos endpoints de acesso para cada serviço.
```hcl
provider "aws" {
  region = "us-east-1"
  access_key = "default"
  secret_key = "default"
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true
  
  endpoints {
    apigateway     = "http://localhost:4566"
    apigatewayv2   = "http://localhost:4566"
    cloudformation = "http://localhost:4566"
    cloudwatch     = "http://localhost:4566"
    dynamodb       = "http://localhost:4566"
    ec2            = "http://localhost:4566"
    es             = "http://localhost:4566"
    elasticache    = "http://localhost:4566"
    firehose       = "http://localhost:4566"
    iam            = "http://localhost:4566"
    kinesis        = "http://localhost:4566"
    lambda         = "http://localhost:4566"
    rds            = "http://localhost:4566"
    redshift       = "http://localhost:4566"
    route53        = "http://localhost:4566"
    s3             = "http://s3.localhost.localstack.cloud:4566"
    secretsmanager = "http://localhost:4566"
    ses            = "http://localhost:4566"
    sns            = "http://localhost:4566"
    sqs            = "http://localhost:4566"
    ssm            = "http://localhost:4566"
    stepfunctions  = "http://localhost:4566"
    sts            = "http://localhost:4566"
  }
}
```
No outro arquivo terraform, o [resource.tf](https://github.com/martineli17/.net-aws-sqs-consumer/blob/master/Sqs/resource.tf), é encontrado a configuração relacionada a queue:
```hcl
resource "aws_sqs_queue" "queue" {
  name                        = "queue"
  delay_seconds               = 1 // tempo que a mensagem irá aguardar para ser disponibilizada para o processamento (S)
  max_message_size            = 2048
  message_retention_seconds   = 60 // tempo que a mensagem irá ficar oculta para um novo processamento (S)
  receive_wait_time_seconds   = 10 // tempo que o consumer irá aguardar para solicitar uma nova buscar por mensagens (S)

  redrive_policy = jsonencode({ // configuração de política para Dead Letter
    deadLetterTargetArn = aws_sqs_queue.queue_dl.arn
    maxReceiveCount     = 1 // quantidade de tentativas antes de enviar para a Dead Letter
  })


  tags = {
    Environment = "localstack"
  }
}

resource "aws_sqs_queue" "queue_dl" {
  name = "queue-dl"

  tags = {
    Environment = "localstack"
  }
}
```
Feito isso, basta aplicar essas definições com os comandos:
```terraform init```
```terraform validate```
```terraform apply -auto-approve```

Assim, finalizando o processo, conseguimos simular corretamente o ambiente AWS e processar as mensagens no serviço SQS.

Obs: Para mais informações, basta acessar a documentação do terraform referente a AWS.

## Código .NET
### Para publicar as mensagens, foi adicionado um [controller](https://github.com/martineli17/.net-aws-sqs-consumer/blob/master/ConsumerQueue/ConsumerQueue/Controllers/QueueController.cs) que contém um endpoint específico para isso.
Neste controller, há a configuração do client AWS, informando as configurações necessárias. Como estamos simulando localmente o serviço AWS, é necessário informação qual é a URL de acesso ao serviço principal>
```csharp
 public QueueController(MessageProcessedDTO messageProcessedDTO, AwsSettingsDTO awsSettings)
 {
     _messageProcessedDTO = messageProcessedDTO;
     _awsSettings = awsSettings;

     var awsCredentials = new AmazonSQSConfig
     {
         ServiceURL = _awsSettings.ServerEndpoint
     };
     _sqsClient = new AmazonSQSClient(awsCredentials);
 }
```
Após a configuração ser realizada, no endpoint é possível publicar a mensagem na fila desejada. para isso, no objeto do request, é necessário informar a URL da fila (juntamente com o nome) e o conteúdo desejado.
```csharp
[HttpPost]
public async Task<IActionResult> AddMessage([FromQuery] bool toDeadLetter)
{
    var content = new MessageDTO { PublishedAt = DateTime.Now, ToDeadLetter = toDeadLetter };
    var messageRequest = new SendMessageRequest()
    {
        MessageBody = JsonSerializer.Serialize(content),
        QueueUrl = $"{_awsSettings.QueueEndpoint}/queue"
    };
    await _sqsClient.SendMessageAsync(messageRequest);

    return Ok();    
}
```
### Com o processo de publicação definido, é necessário criar o consumer para que as mensagens sejam processadas. Para este exemplo, foi adicionado um [background service](https://github.com/martineli17/.net-aws-sqs-consumer/blob/master/ConsumerQueue/ConsumerQueue/BackgroundServices/ConsumerBackgroundService.cs) que fica responsável por realizar este processamento.
Neste background service, foi adicionado dois processamentos: um para consumir a fila e outro para consumir a dead-letter dessa fila.
```csharp
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ConsumerQueue(stoppingToken);
            await ConsumerDeadLetter(stoppingToken);
        }
```

Processamento da fila
```csharp
        private Task ConsumerQueue(CancellationToken stoppingToken)
        {
            string queueUrl = $"{_awsSettings.QueueEndpoint}/queue";
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
```
Processamento da dead-letter
```csharp
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
```
Estes processamentos foram inseridos em uma nova Task, com o objetivo de não interferir no fluxo principal caso ocorra alguma exceção.

### As configurações iniciais ao iniciar o projeto, como as definições de Dependency Injection, estão sendo realizadas na class [Program](https://github.com/martineli17/.net-aws-sqs-consumer/blob/master/ConsumerQueue/ConsumerQueue/Program.cs)
```csharp
var awsSettings = new AwsSettingsDTO();
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.Bind("AwsConfiguration", awsSettings);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<ConsumerBackgroundService>();
builder.Services.AddSingleton<MessageProcessedDTO>();
builder.Services.AddSingleton(awsSettings);
```

### Finalizando todo esse processo, existe um endpoint para visualizar as mensagens processadas, tanto na fila quanto da dead-letter
```csharp
        [HttpGet]
        public IActionResult GetMessagesProcessed()
        {
            return Ok(_messageProcessedDTO);
        }
```
