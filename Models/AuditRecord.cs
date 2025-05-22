namespace ProjectUploader.Models
{
    public class AuditRecord
    {
        public string? SAP_ID { get; set; }
        public string? AUDIT_ID { get; set; }
        public string? DELIVERY_HEAD { get; set; }
        public string? PROJECT { get; set; }
        public string? AUTITEE { get; set; }
        public string? AUDITOR { get; set; }
        public DateTime? AUDIT_DATE { get; set; }
        public int SerialNumber { get; set; } // For display only
    }

}
