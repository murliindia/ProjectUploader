namespace ProjectUploader.Models
{
    public class ProcessAreaComplianceDetails
    {
        public int Id { get; set; }
        public string ProjectId { get; set; }
        public string ProcessArea { get; set; }
        public string QuestionText { get; set; }
        public string Compliance { get; set; }
        public string Remarks { get; set; }
        public string ActionItem { get; set; }
        public DateTime? ClosedDate { get; set; }
    }

}
