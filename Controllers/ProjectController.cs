using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ProjectUploader.Models;
using System.Net.Mail;
using System.Net;
using DocumentFormat.OpenXml.EMMA;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectUploader.Controllers
{
    public class ProjectController : Controller
    {
        // private readonly string _connectionString = "YourConnectionStringHere";

        private readonly IConfiguration _config;
        private readonly IViewRenderService _viewRenderService;
        public ProjectController(IConfiguration config, IViewRenderService viewRenderService)
        {
            _config = config;
            _viewRenderService = viewRenderService;
        }



        // GET: /Project/UploadProjectDetails
        [HttpGet]
        public async Task<IActionResult> UploadProjectDetails()
        {
            var auditList = new List<SelectListItem>();
            var projectList = new List<SelectListItem>();
            var connStr = _config.GetConnectionString("DefaultConnection");

            using (var connection = new SqlConnection(connStr))
            {
                await connection.OpenAsync();

                // --- Get distinct AUDIT_IDs ---
                var auditCommand = new SqlCommand("SELECT DISTINCT AUDIT_ID FROM AUDIT_PLAN ORDER BY AUDIT_ID", connection);
                var auditReader = await auditCommand.ExecuteReaderAsync();

                while (await auditReader.ReadAsync())
                {
                    auditList.Add(new SelectListItem
                    {
                        Value = auditReader["AUDIT_ID"].ToString(),
                        Text = auditReader["AUDIT_ID"].ToString()
                    });
                }

                auditReader.Close(); // Important to close before new reader

                // --- Get distinct PROJECT values ---
                var projectCommand = new SqlCommand("SELECT DISTINCT PROJECT FROM AUDIT_PLAN WHERE PROJECT IS NOT NULL ORDER BY PROJECT", connection);
                var projectReader = await projectCommand.ExecuteReaderAsync();

                while (await projectReader.ReadAsync())
                {
                    projectList.Add(new SelectListItem
                    {
                        Value = projectReader["PROJECT"].ToString(),
                        Text = projectReader["PROJECT"].ToString()
                    });
                }

                projectReader.Close();
            }

            ViewBag.AuditList = auditList;
            ViewBag.ProjectList = projectList;

            return View();
        }





        [HttpPost]
        public async Task<IActionResult> UploadProjectDetails(IFormFile file, string AuditID,string Project)
        {
            Project = Regex.Replace(Project.Trim(), @"\s+", " ");
            if (string.IsNullOrEmpty(AuditID))
            {
                return Json(new { success = false, error = "Please select an Audit ID." });
            }

            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, error = "Please upload a valid Excel file." });
            }

            var details = new ProjectDetails();
            var sectionData = new Dictionary<string, List<List<string>>>();
            var connStr = _config.GetConnectionString("DefaultConnection");


            using var memory = new MemoryStream();
            await file.CopyToAsync(memory);
            memory.Position = 0;
            

            using (var stream = file.OpenReadStream())
            using (var workbook = new XLWorkbook(stream))
            {
                var worksheet = workbook.Worksheet("C3 Checklist");

                // ====== STEP 1: Read ProjectDetails Info ======
                int startRow = 12;

                var labelMap = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Project Name", val => details.ProjectName = val },
            { "Project Manager", val => details.ProjectManager = val },
            { "Account Manager", val => details.AccountManager = val },
            { "Current Stage of Project", val => details.CurrentStage = val },
            { "Auditor's Name", val => details.AuditorName = val },
            { "Project Type", val => details.ProjectType = val },
            { "Audit Date", val =>
                {
                    if (DateTime.TryParse(val, out DateTime dt))
                        details.AuditDate = dt;
                }
            },
            { "Team's audited", val => details.TeamsAudited = val },
            { "Forthcoming Delivery date", val => details.ForthcomingDeliveryDate = val },
            { "Review collaboration tool", val => details.ReviewCollaborationTool = val },
            { "Audit ID", val => details.AuditID = val },
        };

                int row = startRow;
                while (!string.IsNullOrWhiteSpace(worksheet.Cell(row, 2).GetString()))
                {
                    var label = worksheet.Cell(row, 2).GetString().Trim(); // Column B
                    var value = worksheet.Cell(row, 4).GetString().Trim(); // Column D

                    if (labelMap.ContainsKey(label))
                    {
                        labelMap[label](value);
                    }

                    row++;
                }

                // ====== STEP 2: Extract Section Data ======
                // ====== STEP 2: Extract Section Data ======
                var allRows = worksheet.RangeUsed().RowsUsed().ToList();
                string currentSection = null;

                for (int i = 0; i < allRows.Count; i++)
                {
                    var bVal = allRows[i].Cell(2).GetString().Trim(); // Column B

                    // Identify section headers
                    if (!string.IsNullOrEmpty(bVal) && IsSectionHeader(bVal))
                    {
                        currentSection = bVal;
                        sectionData[currentSection] = new List<List<string>>();
                        continue;
                    }

                    if (!string.IsNullOrEmpty(currentSection))
                    {
                        var question = allRows[i].Cell(3).GetString().Trim(); // Column C (C&D merged)
                        var compliance = allRows[i].Cell(5).GetString().Trim(); // Column E
                        var remarks = allRows[i].Cell(6).GetString().Trim();    // Column F

                        if (!string.IsNullOrEmpty(question))
                        {
                            var rowData = new List<string>
            {
                question,
                compliance,
                remarks
            };
                            sectionData[currentSection].Add(rowData);
                        }
                    }
                }


                // STEP 3: Extract E12 to E21 Color Ratings
                var colorRatings = new List<(string AreaName, string ColorValue)>();

                for (int row2 = 12; row2 <= 21; row2++)
                {
                    var cell = worksheet.Cell(row2, 5); // Column E
                    var cellColr = worksheet.Cell(row2, 6); // Column F
                    var areaName = cell.GetString().Trim();

                    if (string.IsNullOrWhiteSpace(areaName))
                        continue;

                    var bgColor = cellColr.Style.Fill.BackgroundColor;
                    string colorName = "Unknown";

                    var fill = cellColr.Style.Fill;


                    if (bgColor.ColorType == XLColorType.Color)
                    {
                        var clr = bgColor.Color;
                        if (clr.R == 255 && clr.G == 255 && clr.B == 0)
                            colorName = "Yellow";
                        else if (clr.R == 0 && clr.G == 255 && clr.B == 0)
                            colorName = "Green";
                        else if (clr.R == 255 && clr.G == 0 && clr.B == 0)
                            colorName = "Red";
                        else
                            colorName = $"RGB({clr.R},{clr.G},{clr.B})";
                    }
                    else if (bgColor.ColorType == XLColorType.Theme)
                    {
                        colorName = bgColor.ThemeColor.ToString();
                    }

                    colorRatings.Add((areaName, colorName));
                }







                // ====== STEP 3: Save to DB ======
                using (var connection = new SqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    // Save ProjectDetails and get ProjectId
                    var command = new SqlCommand(@"
            INSERT INTO ProjectDetails (ProjectId,
                ProjectName, ProjectManager, AccountManager, CurrentStage,
                AuditorName, ProjectType, AuditDate, TeamsAudited,
                ForthcomingDeliveryDate, ReviewCollaborationTool, AuditID
            ) OUTPUT INSERTED.Id
            VALUES (@ProjectId,
                @ProjectName, @ProjectManager, @AccountManager, @CurrentStage,
                @AuditorName, @ProjectType, @AuditDate, @TeamsAudited,
                @ForthcomingDeliveryDate, @ReviewCollaborationTool, @AuditID
            )", connection);

                    // Defensive parsing
                    DateTime? auditDate = DateTime.TryParse(details.AuditDate?.ToString(), out var parsedAuditDate)
                        ? parsedAuditDate
                        : (DateTime?)null;

                    DateTime? deliveryDate = DateTime.TryParse(details.ForthcomingDeliveryDate?.ToString(), out var parsedDeliveryDate)
                        ? parsedDeliveryDate
                        : (DateTime?)null;


                    command.Parameters.AddWithValue("@ProjectId", AuditID);
                    command.Parameters.AddWithValue("@ProjectName", Project);
                    command.Parameters.AddWithValue("@ProjectManager", (object?)details.ProjectManager ?? DBNull.Value);
                    command.Parameters.AddWithValue("@AccountManager", (object?)details.AccountManager ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CurrentStage", (object?)details.CurrentStage ?? DBNull.Value);
                    command.Parameters.AddWithValue("@AuditorName", (object?)details.AuditorName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ProjectType", (object?)details.ProjectType ?? DBNull.Value);
                    
                    command.Parameters.AddWithValue("@TeamsAudited", (object?)details.TeamsAudited ?? DBNull.Value);
                    command.Parameters.AddWithValue("@AuditDate", (object?)auditDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ForthcomingDeliveryDate", (object?)deliveryDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ReviewCollaborationTool", (object?)details.ReviewCollaborationTool ?? DBNull.Value);
                    command.Parameters.AddWithValue("@AuditID", (object?)details.AuditID ?? DBNull.Value);

                    try
                    {
                        int projectId = (int)await command.ExecuteScalarAsync();
                    }
                    catch (Exception ex)
                    {

                        throw ex;
                    }
                  

                    // Save ProcessAreaComplianceDetails
                    foreach (var section in sectionData)
                    {
                        string processArea = section.Key;
                        var rows = section.Value;

                        foreach (var item in rows)
                        {
                            var insertCmd = new SqlCommand(@"
                    INSERT INTO ProcessAreaComplianceDetails (
                        ProjectId,ProjectName, ProcessArea, QuestionText, Compliance, Remarks
                    ) VALUES (
                        @ProjectId,@ProjectName, @ProcessArea, @QuestionText, @Compliance, @Remarks
                    )", connection);

                            insertCmd.Parameters.AddWithValue("@ProjectId", AuditID);
                            insertCmd.Parameters.AddWithValue("@ProjectName", Project);
                            insertCmd.Parameters.AddWithValue("@ProcessArea", processArea);
                            insertCmd.Parameters.AddWithValue("@QuestionText", item.ElementAtOrDefault(0) ?? (object)DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@Compliance", item.ElementAtOrDefault(1) ?? (object)DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@Remarks", item.ElementAtOrDefault(2) ?? (object)DBNull.Value);

                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    foreach (var item in colorRatings)
                    {
                        var insertColor = new SqlCommand(@"
        INSERT INTO ProcessAreaColorSummary (ProjectId,ProjectName, AreaName, ColorValue)
        VALUES (@ProjectId,@ProjectName, @AreaName, @ColorValue)", connection);

                        insertColor.Parameters.AddWithValue("@ProjectId", AuditID);
                        insertColor.Parameters.AddWithValue("@ProjectName", Project);
                        insertColor.Parameters.AddWithValue("@AreaName", item.AreaName);
                        insertColor.Parameters.AddWithValue("@ColorValue", item.ColorValue);

                        await insertColor.ExecuteNonQueryAsync();
                    }

                }

                return Json(new { success = true, auditId = AuditID, project= Project });

            }
        }


        // Helper to detect section headers – customize as needed
        private bool IsSectionHeader(string value)
        {
            var knownHeaders = new[]
            {
        "Project Estimation", "Project Planning", "Requirement Management", "Design Related (Not Applicable for Testing projects)",
        "Coding Related (Not Applicable for Testing projects)", "CHANGE MANAGEMENT", "Project management and tracking", "Goal Tracking", "Review Process","Testing Process",
        "Configuration Management","Defect Prevention (Not Applicable for Testing Projects)","Release & Project Phase Closure",
    };

            return knownHeaders.Any(h => value.Equals(h, StringComparison.OrdinalIgnoreCase));
        }

        //[HttpGet("project/view/{projectId}")]
        //public async Task<IActionResult> ViewProjectDetails(string projectId)
        //{
        //    var connStr = _config.GetConnectionString("DefaultConnection");

        //    var projectDetails = new ProjectDetails();
        //    var colorSummaries = new List<ProcessAreaColorSummary>();
        //    var complianceDetails = new List<ProcessAreaComplianceDetails>();

        //    using (var connection = new SqlConnection(connStr))
        //    {
        //        await connection.OpenAsync();

        //        // Get ProjectDetails
        //        using (var cmd = new SqlCommand("SELECT TOP 1 * FROM ProjectDetails WHERE ProjectId = @ProjectId", connection))
        //        {
        //            cmd.Parameters.AddWithValue("@ProjectId", projectId);

        //            using (var reader = await cmd.ExecuteReaderAsync())
        //            {
        //                if (await reader.ReadAsync())
        //                {
        //                    projectDetails = new ProjectDetails
        //                    {
        //                        Id = Convert.ToInt32(reader["Id"]),
        //                        ProjectId = reader["ProjectId"].ToString(),
        //                        ProjectName = reader["ProjectName"].ToString(),
        //                        ProjectManager = reader["ProjectManager"].ToString(),
        //                        AccountManager = reader["AccountManager"].ToString(),
        //                        CurrentStage = reader["CurrentStage"].ToString(),
        //                        AuditorName = reader["AuditorName"].ToString(),
        //                        ProjectType = reader["ProjectType"].ToString(),
        //                        AuditDate = reader["AuditDate"] as DateTime?,
        //                        TeamsAudited = reader["TeamsAudited"].ToString(),
        //                        ForthcomingDeliveryDate = reader["ForthcomingDeliveryDate"].ToString(),
        //                        ReviewCollaborationTool = reader["ReviewCollaborationTool"].ToString(),
        //                        AuditID = reader["AuditID"].ToString()
        //                    };
        //                }
        //            }
        //        }

        //        // Get Color Summary
        //        using (var cmd = new SqlCommand("SELECT * FROM ProcessAreaColorSummary WHERE ProjectId = @ProjectId", connection))
        //        {
        //            cmd.Parameters.AddWithValue("@ProjectId", projectId);

        //            using (var reader = await cmd.ExecuteReaderAsync())
        //            {
        //                while (await reader.ReadAsync())
        //                {
        //                    colorSummaries.Add(new ProcessAreaColorSummary
        //                    {
        //                        Id = Convert.ToInt32(reader["Id"]),
        //                        ProjectId = reader["ProjectId"].ToString(),
        //                        AreaName = reader["AreaName"].ToString(),
        //                        ColorValue = reader["ColorValue"].ToString()
        //                    });
        //                }
        //            }
        //        }

        //        // Get ProcessAreaComplianceDetails
        //        using (var cmd = new SqlCommand("SELECT * FROM ProcessAreaComplianceDetails WHERE ProjectId = @ProjectId", connection))
        //        {
        //            cmd.Parameters.AddWithValue("@ProjectId", projectId);

        //            using (var reader = await cmd.ExecuteReaderAsync())
        //            {
        //                while (await reader.ReadAsync())
        //                {
        //                    complianceDetails.Add(new ProcessAreaComplianceDetails
        //                    {
        //                        Id = Convert.ToInt32(reader["Id"]),
        //                        ProjectId = reader["ProjectId"].ToString(),
        //                        ProcessArea = reader["ProcessArea"].ToString(),
        //                        QuestionText = reader["QuestionText"].ToString(),
        //                        Compliance = reader["Compliance"]?.ToString(),
        //                        Remarks = reader["Remarks"]?.ToString(),
        //                        ActionItem= reader["ActionItem"]?.ToString()
        //                    });
        //                }
        //            }
        //        }
        //    }

        //    var viewModel = new ProjectDetailsViewModel
        //    {
        //        Project = projectDetails,
        //        ColorSummaries = colorSummaries,
        //        ComplianceDetails = complianceDetails  // assign loaded compliance details here
        //    };

        //    return View(viewModel);
        //}
        [HttpGet("project/view/{auditId}/{projectName}")]
        public async Task<IActionResult> ViewProjectDetails(string auditId, string projectName)
        {
            projectName = Regex.Replace(projectName.Trim(), @"\s+", " ");
            if (string.IsNullOrWhiteSpace(auditId) || string.IsNullOrWhiteSpace(projectName))
                return BadRequest("Audit ID and Project Name are required.");

            var connStr = _config.GetConnectionString("DefaultConnection");

            var projectDetails = new ProjectDetails();
            var colorSummaries = new List<ProcessAreaColorSummary>();
            var complianceDetails = new List<ProcessAreaComplianceDetails>();

            using (var connection = new SqlConnection(connStr))
            {
                await connection.OpenAsync();

                // Get ProjectDetails
                using (var cmd = new SqlCommand("SELECT TOP 1 * FROM ProjectDetails WHERE ProjectId = @AuditID AND ProjectName = @ProjectName", connection))
                {
                    cmd.Parameters.AddWithValue("@AuditID", auditId);
                    cmd.Parameters.AddWithValue("@ProjectName", projectName);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            projectDetails = new ProjectDetails
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                ProjectId = reader["ProjectId"].ToString(),
                                ProjectName = reader["ProjectName"].ToString(),
                                ProjectManager = reader["ProjectManager"].ToString(),
                                AccountManager = reader["AccountManager"].ToString(),
                                CurrentStage = reader["CurrentStage"].ToString(),
                                AuditorName = reader["AuditorName"].ToString(),
                                ProjectType = reader["ProjectType"].ToString(),
                                AuditDate = reader["AuditDate"] as DateTime?,
                                TeamsAudited = reader["TeamsAudited"].ToString(),
                                ForthcomingDeliveryDate = reader["ForthcomingDeliveryDate"].ToString(),
                                ReviewCollaborationTool = reader["ReviewCollaborationTool"].ToString(),
                                AuditID = reader["AuditID"].ToString()
                            };
                        }
                    }
                }

                // Get Color Summary
                using (var cmd = new SqlCommand("SELECT * FROM ProcessAreaColorSummary WHERE ProjectId = @AuditID  AND ProjectName = @ProjectName", connection))
                {
                    cmd.Parameters.AddWithValue("@AuditID", auditId);
                    cmd.Parameters.AddWithValue("@ProjectName", projectName);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            colorSummaries.Add(new ProcessAreaColorSummary
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                ProjectId = reader["ProjectId"].ToString(),
                                AreaName = reader["AreaName"].ToString(),
                                ColorValue = reader["ColorValue"].ToString()
                            });
                        }
                    }
                }

                // Get ProcessAreaComplianceDetails
                using (var cmd = new SqlCommand("SELECT * FROM ProcessAreaComplianceDetails WHERE ProjectId = @AuditID  AND ProjectName = @ProjectName", connection))
                {
                    cmd.Parameters.AddWithValue("@AuditID", auditId);
                    cmd.Parameters.AddWithValue("@ProjectName", projectName);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            complianceDetails.Add(new ProcessAreaComplianceDetails
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                ProjectId = reader["ProjectId"].ToString(),
                                ProcessArea = reader["ProcessArea"].ToString(),
                                QuestionText = reader["QuestionText"].ToString(),
                                Compliance = reader["Compliance"]?.ToString(),
                                Remarks = reader["Remarks"]?.ToString(),
                                ActionItem = reader["ActionItem"]?.ToString()
                            });
                        }
                    }
                }
            }

            var viewModel = new ProjectDetailsViewModel
            {
                Project = projectDetails,
                ColorSummaries = colorSummaries,
                ComplianceDetails = complianceDetails
            };

            return View(viewModel);
        }


        [HttpPost]
        public async Task<IActionResult> SaveCompliance(string projectId, string projectName, List<ProcessAreaComplianceDetails> ComplianceDetails)
        {
            if (ComplianceDetails == null)
                return BadRequest();
            var connStr = _config.GetConnectionString("DefaultConnection");

            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                foreach (var item in ComplianceDetails)
                {
                    var cmd = new SqlCommand(@"
            UPDATE ProcessAreaComplianceDetails
            SET QuestionText = @QuestionText,
                Compliance = @Compliance,
                Remarks = @Remarks,
                ActionItem = @ActionItem
            WHERE Id = @Id", conn);

                    cmd.Parameters.AddWithValue("@QuestionText", item.QuestionText ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Compliance", item.Compliance ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remarks", item.Remarks ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ActionItem", item.ActionItem ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Id", item.Id);

                    await cmd.ExecuteNonQueryAsync();

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Reload updated data for showing again, or redirect
            TempData["SuccessMessage"] = "Updated successfully";

            // Redirect back to same URL
            return RedirectToAction("ViewProjectDetails", "Project", new { auditId = projectId, projectName = projectName });
        }

        //[HttpGet]
        //public IActionResult CheckProjectExists(string projectId)
        //{
        //    bool exists = false;
        //    var connStr = _config.GetConnectionString("DefaultConnection");
        //    using (SqlConnection conn = new SqlConnection(connStr))
        //    {
        //        string query = "SELECT COUNT(1) FROM ProjectDetails WHERE ProjectId = @ProjectId";
        //        SqlCommand cmd = new SqlCommand(query, conn);
        //        cmd.Parameters.AddWithValue("@ProjectId", projectId);
        //        conn.Open();

        //        int count = (int)cmd.ExecuteScalar();
        //        exists = count > 0;
        //    }

        //    return Json(new { exists });
        //}

        [HttpGet]
        public IActionResult CheckAuditProjectExists(string auditId, string projectName)
        {
            projectName = Regex.Replace(projectName.Trim(), @"\s+", " ");
            if (string.IsNullOrWhiteSpace(auditId) || string.IsNullOrWhiteSpace(projectName))
                return Json(new { exists = false });

            bool exists;
            var connStr = _config.GetConnectionString("DefaultConnection");

            using var conn = new SqlConnection(connStr);
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM ProjectDetails WHERE ProjectId = @AuditId AND ProjectName = @ProjectName", conn);

            cmd.Parameters.AddWithValue("@AuditId", auditId);
            cmd.Parameters.AddWithValue("@ProjectName", projectName);

            conn.Open();
            var count = (int)cmd.ExecuteScalar();
            exists = count > 0;

            return Json(new { exists });
        }



        [HttpPost]
        public async Task<IActionResult> EmailProjectDetails(string auditId)
        { 

            var emailHtml = await _viewRenderService.RenderToStringAsync("Project/View", auditId); // Or manually build HTML string

            var message = new MailMessage
            {
                From = new MailAddress("your@email.com"),
                Subject = $"Project Report - {auditId}",
                Body = emailHtml,
                IsBodyHtml = true
            };
            message.To.Add("recipient@email.com");

            using var smtp = new SmtpClient("smtp.yourdomain.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("your@email.com", "yourpassword"),
                EnableSsl = true
            };

            await smtp.SendMailAsync(message);
            return Json(new { success = true });
        }
        [HttpGet]
        public IActionResult ExportEmailScript(string auditId)
        {
            // Ideally render the full HTML using _viewRenderService or hardcoded demo for now
            string htmlContent = "<h2>Audit Report for " + auditId + "</h2><p>...</p>";

            var script = $@"
$htmlBody = @""{htmlContent.Replace("\"", "\"\"")}""
$outlook = New-Object -ComObject Outlook.Application
$mail = $outlook.CreateItem(0)
$mail.Subject = 'Project Audit Report - {auditId}'
$mail.HTMLBody = $htmlBody
$mail.Display()
";

            var bytes = Encoding.UTF8.GetBytes(script);
            return File(bytes, "application/octet-stream", "SendAuditEmail.ps1");
        }



    }
}
