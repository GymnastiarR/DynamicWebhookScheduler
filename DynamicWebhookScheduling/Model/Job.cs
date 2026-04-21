namespace DynamicWebhookScheduling.Model
{
    public class Job
    {
        public required string Id { get; set; }
        public required DateTime RunAt { get; set; }
        public required Request Request { get; set; }
    }

    public class Request
    {
        public required Applications.Enums.RequestMethod Method { get; set; }
        public required string Url { get; set; }
        public string? Payload { get; set; }
        public Dictionary<string, string>? Queries { get; set; }
    }
}
