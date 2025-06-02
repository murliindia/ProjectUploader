using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProjectUploader.Models;

namespace ProjectUploader.ViewModel
{
    public class ProjectAuditListViewModel
    {
        public ProjectAudit NewProjectAudit { get; set; }

        [BindNever]  // Prevent model binding/validation on these
        public List<ProjectAudit> ProjectAudits { get; set; }

        [BindNever]
        public List<Audit> AuditList { get; set; }

        [BindNever]
        public int PageNumber { get; set; }

        [BindNever]
        public int TotalPages { get; set; }
    }

}
