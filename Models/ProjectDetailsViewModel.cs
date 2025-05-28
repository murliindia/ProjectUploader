namespace ProjectUploader.Models
{
    public class ProjectDetailsViewModel
    {
        public ProjectDetails Project { get; set; }
        public List<ProcessAreaColorSummary> ColorSummaries { get; set; }
        // Add this property
        public List<ProcessAreaComplianceDetails> ComplianceDetails { get; set; }
    }
}
