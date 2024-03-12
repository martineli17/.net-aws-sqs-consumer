namespace ConsumerQueue.DTO
{
    public record MessageDTO
    {
        public DateTime PublishedAt { get; init; }
        public bool ToDeadLetter { get; init; }
    }
}
