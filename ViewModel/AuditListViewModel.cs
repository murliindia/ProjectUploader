using ProjectUploader.Models;

namespace ProjectUploader.ViewModel
{
    public class AuditListViewModel
    {
        public List<Audit> Audits { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
    }

}
