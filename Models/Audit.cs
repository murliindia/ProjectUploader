using System.ComponentModel.DataAnnotations;

namespace ProjectUploader.Models
{
    public class Audit
    {
        [Key]
        public string Audit_ID { get; set; }
        public string Name { get; set; }
    }
}
