namespace ProjectUploader.Models
{
    public class ProjectDetails
    {
        public int Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectManager { get; set; }
        public string AccountManager { get; set; }
        public string CurrentStage { get; set; }
        public string AuditorName { get; set; }
        public string ProjectType { get; set; }
        public DateTime? AuditDate { get; set; }
        public string TeamsAudited { get; set; }
        public string ForthcomingDeliveryDate { get; set; }
        public string ReviewCollaborationTool { get; set; }
        public string AuditID { get; set; }
        public string AuditCoverage { get; set; }

    }

}
