using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectUploader.DB;   
using ProjectUploader.Models;
using ProjectUploader.ViewModel;
using System.Linq;
namespace ProjectUploader.Controllers
{
    public class ProjectAuditController : Controller
    {
        private readonly AppDbContext _context;

        public ProjectAuditController(AppDbContext context)
        {
            _context = context;
        }

        // List with paging
        // Inject AppDbContext in the controller constructor

        public IActionResult Index(int pageNumber = 1)
        {
            int pageSize = 10;
            var audits = _context.AUDIT.ToList();
            var totalRecords = _context.PROJECT_AUDIT.Count();
            var projectAudits = _context.PROJECT_AUDIT
                .OrderBy(p => p.AUDIT_ID)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var viewModel = new ProjectAuditListViewModel
            {
                ProjectAudits = projectAudits,
                AuditList = audits,
                PageNumber = pageNumber,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
            };

            return View(viewModel);
        }


        // Save (Insert or Update)
        //[HttpPost]
        //public IActionResult Save(ProjectAudit model)
        //{
        //    var existing = _context.PROJECT_AUDIT
        //        .FirstOrDefault(pa => pa.AUDIT_ID == model.AUDIT_ID && pa.PROJECT_NAME == model.PROJECT_NAME);

        //    if (existing == null)
        //    {
        //        _context.PROJECT_AUDIT.Add(model);
        //        TempData["Success"] = "Project Audit record created successfully.";
        //    }
        //    else
        //    {
        //        existing.ACCOUNT_MANAGER = model.ACCOUNT_MANAGER;
        //        existing.PROJECT_MANAGER = model.PROJECT_MANAGER;
        //        existing.ACCOUNT_NAME = model.ACCOUNT_NAME;
        //        existing.PLAN_AUDIT_DATE = model.PLAN_AUDIT_DATE;
        //        existing.ACTUAL_AUDIT_DATE = model.ACTUAL_AUDIT_DATE;
        //        existing.AUDITOR = model.AUDITOR;
        //        existing.AUDIT = model.AUDIT;

        //        TempData["Success"] = "Project Audit record updated successfully.";
        //    }

        //    _context.SaveChanges();
        //    return RedirectToAction("Index");
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(ProjectAuditListViewModel model)
        {
            ModelState.Remove("ProjectAudits");
            ModelState.Remove("AuditList");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["Errors"] = string.Join("; ", errors);


                int pageSize = 10;
                int totalRecords = _context.PROJECT_AUDIT.Count();
                model.ProjectAudits = _context.PROJECT_AUDIT
                    .OrderBy(p => p.AUDIT_ID)
                    .Take(pageSize)
                    .ToList();

                model.AuditList = _context.AUDIT.ToList();
                model.PageNumber = 1;
                model.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                return View("Index", model);
            }

            var existing = _context.PROJECT_AUDIT.FirstOrDefault(p => p.AUDIT_ID == model.NewProjectAudit.AUDIT_ID && p.PROJECT_NAME == model.NewProjectAudit.PROJECT_NAME);
            if (existing != null)
            {
                existing.ACCOUNT_MANAGER = model.NewProjectAudit.ACCOUNT_MANAGER;
                existing.PROJECT_MANAGER = model.NewProjectAudit.PROJECT_MANAGER;
                existing.ACCOUNT_NAME = model.NewProjectAudit.ACCOUNT_NAME;
                existing.PLAN_AUDIT_DATE = model.NewProjectAudit.PLAN_AUDIT_DATE;
                existing.ACTUAL_AUDIT_DATE = model.NewProjectAudit.ACTUAL_AUDIT_DATE;
                existing.AUDITOR = model.NewProjectAudit.AUDITOR;
                existing.AUDIT = model.NewProjectAudit.AUDIT;
                _context.Update(existing);
            }
            else
            {
                _context.Add(model.NewProjectAudit);
            }
            TempData["SuccessMessage"] = existing != null ? "Project Audit updated successfully." : "Project Audit saved successfully.";
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
        public IActionResult Edit(string auditId, string projectName)
        {
            var audit = _context.PROJECT_AUDIT.FirstOrDefault(p => p.AUDIT_ID == auditId && p.PROJECT_NAME == projectName);
            if (audit == null)
            {
                return NotFound();
            }

            var model = new ProjectAuditListViewModel
            {
                NewProjectAudit = audit,
                AuditList = _context.AUDIT.ToList(),
                ProjectAudits = _context.PROJECT_AUDIT
                                    .OrderBy(p => p.AUDIT_ID)
                                    .Take(10)
                                    .ToList(),
                PageNumber = 1,
                TotalPages = (int)Math.Ceiling(_context.PROJECT_AUDIT.Count() / 10.0)
            };

            return View("Index", model); // Reuse the same view
        }

        [HttpPost]
        public IActionResult Delete(string auditId, string projectName)
        {
            var record = _context.PROJECT_AUDIT.FirstOrDefault(p => p.AUDIT_ID == auditId && p.PROJECT_NAME == projectName);
            if (record != null)
            {
                _context.PROJECT_AUDIT.Remove(record);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Record deleted successfully.";
            }
            return RedirectToAction("Index");
        }

        // Delete action
        //[HttpPost]
        //public IActionResult Delete(string auditId, string projectName)
        //{
        //    var projectAudit = _context.PROJECT_AUDIT
        //        .FirstOrDefault(pa => pa.AUDIT_ID == auditId && pa.PROJECT_NAME == projectName);

        //    if (projectAudit != null)
        //    {
        //        _context.PROJECT_AUDIT.Remove(projectAudit);
        //        _context.SaveChanges();
        //        TempData["Success"] = "Project Audit record deleted successfully.";
        //    }
        //    return RedirectToAction("Index");
        //}
    }
}
