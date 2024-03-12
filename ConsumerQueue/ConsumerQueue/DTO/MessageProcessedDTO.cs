namespace ConsumerQueue.DTO
{
    public record MessageProcessedDTO
    {
        public MessageDTO LastMessageReceived { get; set; }
        public MessageDTO LastMessageDeadLetter { get; set; }
    }
}
