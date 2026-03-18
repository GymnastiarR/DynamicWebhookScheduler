using System.ComponentModel.DataAnnotations;

namespace DynamicWebhookScheduling.Controllers.DTO
{
    public class CreateDateTimeJobRequest {
        [Required]
        public required RequestDTO RequestDTO { get; set; }

        [Required]
        public required DateTime RunAt { get; set; }
    }
}
