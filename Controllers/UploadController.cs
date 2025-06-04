using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using ProjectUploader.Models;

namespace ProjectUploader.Controllers
{
    public class UploadController : Controller
    {
        private readonly IConfiguration _config;

        public UploadController(IConfiguration config)
        {
            _config = config;
        }

        //public IActionResult Index()
        //{
        //    return View(new List<AuditRecord>());


        //}
        public IActionResult Index()
        {
            //if (TempData["FilteredData"] != null)
            //{
            //    var data = JsonConvert.DeserializeObject<List<AuditRecord>>(TempData["FilteredData"].ToString());
            //    return View(data);
            //}

            return View(new List<AuditRecord>());
        }


        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || Path.GetExtension(file.FileName).ToLower() != ".xlsx")
            {
                TempData["ErrorMessage"] = "Only .xlsx files are allowed.";
                return RedirectToAction("Index");

            }

            var AuditRecords = new List<AuditRecord>();
            string currentDeliveryHead = null;

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var auditId = worksheet.Cell("B2").GetString().Trim();
                    int serial = 1;
                    int lastRow = worksheet.LastRowUsed().RowNumber();

                    for (int row = 4; row <= lastRow; row++)
                    {
                        var cellB = worksheet.Cell(row, 2); // DELIVERY_HEAD
                        var cellC = worksheet.Cell(row, 3); // Project

                        if (!cellB.IsEmpty() && cellC.IsEmpty())
                        {
                            currentDeliveryHead = cellB.GetString().Trim();
                            continue;
                        }

                        if (cellC.IsEmpty()) continue;
                        string sapId = GenerateRandomSapId();
                        DateTime auditDate;
                        DateTime? auditDateNullable = worksheet.Cell(row, 6).TryGetValue(out auditDate) ? auditDate : null;

                        AuditRecords.Add(new AuditRecord
                        {
                            SAP_ID = sapId, // Will generate at save
                            AUDIT_ID = auditId,
                            DELIVERY_HEAD = currentDeliveryHead,
                            PROJECT = cellC.GetString().Trim(),
                            AUTITEE = worksheet.Cell(row, 4).GetString().Trim(),
                            AUDITOR = worksheet.Cell(row, 5).GetString().Trim(),
                            AUDIT_DATE = auditDateNullable,
                            SerialNumber = serial++
                        });
                    }
                }
            }

            //TempData["AuditRecords"] = JsonConvert.SerializeObject(AuditRecords);
            //TempData.Keep("AuditRecords");
            TempData["PreviewMode"] = true; // Disable filter in view
            return View("Index", AuditRecords);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAuditRecords(string jsonData)
        {
            var auditRecords = JsonConvert.DeserializeObject<List<AuditRecord>>(jsonData);

            if (auditRecords == null || !auditRecords.Any())
            {
                TempData["ErrorMessage"] = "No data found to save.";
                return RedirectToAction("Index");
            }

            string connStr = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Collect all audit IDs from the uploaded data
                var auditIdsToCheck = auditRecords
                    .Where(x => !string.IsNullOrEmpty(x.AUDIT_ID))
                    .Select(x => x.AUDIT_ID)
                    .Distinct()
                    .ToList();

                // Prepare a single query to check for duplicates in DB
                string auditIdParamList = string.Join(",", auditIdsToCheck.Select((id, index) => $"@id{index}"));
                string checkQuery = $"SELECT AUDIT_ID FROM AUDIT_PLAN WHERE AUDIT_ID IN ({auditIdParamList})";

                using (var checkCmd = new SqlCommand(checkQuery, conn))
                {
                    for (int i = 0; i < auditIdsToCheck.Count; i++)
                    {
                        checkCmd.Parameters.AddWithValue($"@id{i}", auditIdsToCheck[i]);
                    }

                    string existingAuditId = null;
                    using (var reader = await checkCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            existingAuditId = reader.GetString(0);
                        }
                    }

                    if (!string.IsNullOrEmpty(existingAuditId))
                    {
                        TempData["ErrorMessage"] = $"Audit ID already exists: {existingAuditId}. Please use a different Audit ID.";
                        return RedirectToAction("Index");
                    }
                }

                // No duplicates found, proceed with insert
                foreach (var plan in auditRecords)
                {
                    var cmd = new SqlCommand(
                        "INSERT INTO AUDIT_PLAN (SAP_ID, AUDIT_ID, DELIVERY_HEAD, PROJECT, AUTITEE, AUDITOR, AUDIT_DATE) " +
                        "VALUES (@SAP_ID, @AUDIT_ID, @DELIVERY_HEAD, @PROJECT, @AUTITEE, @AUDITOR, @AUDIT_DATE)", conn);

                    cmd.Parameters.AddWithValue("@SAP_ID", plan.SAP_ID);
                    cmd.Parameters.AddWithValue("@AUDIT_ID", plan.AUDIT_ID ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DELIVERY_HEAD", plan.DELIVERY_HEAD ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PROJECT", plan.PROJECT ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AUTITEE", plan.AUTITEE ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AUDITOR", plan.AUDITOR ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AUDIT_DATE", plan.AUDIT_DATE ?? (object)DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Message"] = "Audit plans saved successfully!";
            return RedirectToAction("Index");
        }


        private string GenerateRandomSapId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        }

        [HttpGet]
        public IActionResult FilterData()
        {
            return View(new List<AuditRecord>());
        }

        [HttpPost]
        public async Task<IActionResult> FilterData(DateTime fromDate, DateTime toDate)
        {
            var filteredRecords = new List<AuditRecord>();
            var connStr = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = @"SELECT SAP_ID, AUDIT_ID, DELIVERY_HEAD, PROJECT, AUTITEE, AUDITOR, AUDIT_DATE 
                         FROM AUDIT_PLAN 
                         WHERE AUDIT_DATE >= @FromDate AND AUDIT_DATE < @ToDate
                         ORDER BY AUDIT_DATE";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate.Date);
                    cmd.Parameters.AddWithValue("@ToDate", toDate.Date.AddDays(1)); // Include full day

                    await conn.OpenAsync();
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            filteredRecords.Add(new AuditRecord
                            {
                                SAP_ID = reader["SAP_ID"]?.ToString(),
                                AUDIT_ID = reader["AUDIT_ID"]?.ToString(),
                                DELIVERY_HEAD = reader["DELIVERY_HEAD"]?.ToString(),
                                PROJECT = reader["PROJECT"]?.ToString(),
                                AUTITEE = reader["AUTITEE"]?.ToString(),
                                AUDITOR = reader["AUDITOR"]?.ToString(),
                                AUDIT_DATE = reader["AUDIT_DATE"] == DBNull.Value ? null : (DateTime?)reader["AUDIT_DATE"]
                            });
                        }
                    }
                }
            }

            TempData["FilteredData"] = JsonConvert.SerializeObject(filteredRecords);
            TempData["PreviewMode"] = false;
            if (!filteredRecords.Any())
            {
                TempData["ErrorMessage"] = "No audit records found for the selected date range.";
            }
            return View("Index", filteredRecords);
        }



    }
}