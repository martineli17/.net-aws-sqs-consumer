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

# resource "aws_sqs_queue_redrive_allow_policy" "queue_redrive_allow_policy" {
#   queue_url = aws_sqs_queue.queue_dl.id

#   redrive_allow_policy = jsonencode({
#     redrivePermission = "byQueue",
#     sourceQueueArns   = [aws_sqs_queue.queue.arn]
#   })
# }
