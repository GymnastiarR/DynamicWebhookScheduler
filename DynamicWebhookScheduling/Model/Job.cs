namespace DynamicWebhookScheduling.Model
{
    public class Job
    {
        public int? Id { get; set; }
        public required DateTime RunAt { get; set; }
        public required Request Request { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; } = new();
    }

    public class Request
    {
        public required Applications.Enums.RequestMethod Method { get; set; }
        public required string Url { get; set; }
        public string? Payload { get; set; }
        public Dictionary<string, string>? Queries { get; set; }
    }
}
