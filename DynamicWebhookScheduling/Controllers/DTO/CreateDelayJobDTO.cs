using System.ComponentModel.DataAnnotations;

namespace DynamicWebhookScheduling.Controllers.DTO
{
    public class CreateDelayJobDTO
    {
        [Required]
        public required RequestDTO RequestDTO { get; set; }

        [Required]
        public required TimeSpan RunAfter { get; set; }

        [Required]
        public required DateTime Timestamp { get; set; }
    }
}
