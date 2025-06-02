using System.ComponentModel.DataAnnotations;

namespace ProjectUploader.Models
{
    public class ProjectAudit
    {
        [Required(ErrorMessage = "Audit ID is required")]
        [Display(Name = "Audit ID")]
        public string AUDIT_ID { get; set; }

        [Required(ErrorMessage = "Project Name is required")]
        [Display(Name = "Project Name")]
        public string PROJECT_NAME { get; set; }

        [Required(ErrorMessage = "Account Manager is required")]
        [Display(Name = "Account Manager")]
        public string ACCOUNT_MANAGER { get; set; }

        [Required(ErrorMessage = "Project Manager is required")]
        [Display(Name = "Project Manager")]
        public string PROJECT_MANAGER { get; set; }

        [Required(ErrorMessage = "Account Name is required")]
        [Display(Name = "Account Name")]
        public string ACCOUNT_NAME { get; set; }

        [Required(ErrorMessage = "Planned Audit Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Planned Audit Date")]
        public DateTime? PLAN_AUDIT_DATE { get; set; }

        [Required(ErrorMessage = "Actual Audit Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Actual Audit Date")]
        public DateTime? ACTUAL_AUDIT_DATE { get; set; }

        [Required(ErrorMessage = "Auditor is required")]
        [Display(Name = "Auditor")]
        public string AUDITOR { get; set; }

        [Required(ErrorMessage = "Audit is required")]
        [Display(Name = "Audit")]
        public string AUDIT { get; set; }
    }
}
