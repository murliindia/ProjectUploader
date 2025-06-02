using Microsoft.AspNetCore.Mvc;
using ProjectUploader.DB;
using ProjectUploader.Models;
using ProjectUploader.ViewModel;

namespace ProjectUploader.Controllers
{
    public class AuditController : Controller
    {
        private readonly AppDbContext _context;

        public AuditController(AppDbContext context)
        {
            _context = context;
        }

        // List with paging
        public IActionResult Index(int pageNumber = 1, int pageSize = 10)
        {
            var totalRecords = _context.AUDIT.Count();

            var audits = _context.AUDIT
                .OrderBy(a => a.Audit_ID)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new AuditListViewModel
            {
                Audits = audits,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords
            };

            return View(model);
        }
        [HttpPost]
        public IActionResult Save(Audit model)
        {
            var existing = _context.AUDIT.FirstOrDefault(a => a.Audit_ID == model.Audit_ID);

            if (existing == null)
            {
                // Create new record
                _context.AUDIT.Add(model);
                TempData["Success"] = "Audit record created successfully.";
            }
            else
            {
                // Update existing record's Name
                existing.Name = model.Name;
                TempData["Success"] = "Audit record updated successfully.";
            }

            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        // Delete action
        [HttpPost]
        public IActionResult Delete(string id)
        {
            var audit = _context.AUDIT.Find(id);
            if (audit != null)
            {
                _context.AUDIT.Remove(audit);
                _context.SaveChanges();
                TempData["Success"] = "Audit record deleted successfully.";
            }
            return RedirectToAction("Index");
        }
    }
}
