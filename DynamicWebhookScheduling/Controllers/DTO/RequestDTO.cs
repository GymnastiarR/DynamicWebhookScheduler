using System.ComponentModel.DataAnnotations;

namespace DynamicWebhookScheduling.Controllers.DTO
{
    public class RequestDTO
    {
        [Required]
        public required string Url { get; set; }

        [Required]
        public required Applications.Enums.RequestMethod Method { get; set; }

        public Dictionary<string, string>? Queries { get; set; }

        public string? Payload { get; set; }
    }
}
