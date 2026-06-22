using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetakeSystem.Data;
using RetakeSystem.Models;

namespace RetakeSystem.Controllers
{
    public class RetakeSheetController : Controller
    {
        private readonly AppDbContext _db;

        public RetakeSheetController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /RetakeSheet
        public async Task<IActionResult> Index()
        {
            ViewBag.Students = await _db.Students.ToListAsync();
            ViewBag.Specialties = await _db.Disciplines
                .Select(d => d.SpecialtyCode)
                .Distinct()
                .Where(s => !string.IsNullOrEmpty(s))
                .ToListAsync();

            return View();
        }

        // POST: /RetakeSheet/Create
        [HttpPost]
        public async Task<IActionResult> Create(int studentId, string fromSpecialty, string toSpecialty)
        {
            if (string.IsNullOrEmpty(fromSpecialty) || string.IsNullOrEmpty(toSpecialty))
            {
                TempData["Error"] = "Укажите оба направления подготовки";
                return RedirectToAction("Index");
            }

            try
            {
                // Создаём лист перезачёта
                var retakeSheet = new RetakeSheet
                {
                    StudentId = studentId,
                    FromSpecialty = fromSpecialty,
                    ToSpecialty = toSpecialty,
                    CreatedAt = DateTime.UtcNow
                };

                _db.RetakeSheets.Add(retakeSheet);
                await _db.SaveChangesAsync();

                // Получаем дисциплины целевого направления (куда переводится)
                var targetDisciplines = await _db.Disciplines
                    .Where(d => d.SpecialtyCode == toSpecialty)
                    .OrderBy(d => d.Code)
                    .ToListAsync();

                // Получаем дисциплины исходного направления (откуда переводится)
                var sourceDisciplines = await _db.Disciplines
                    .Where(d => d.SpecialtyCode == fromSpecialty)
                    .ToListAsync();

                int totalTransferred = 0;
                int totalDebt = 0;
                int rowNum = 1;

                // Сравниваем дисциплины
                foreach (var targetDisc in targetDisciplines)
                {
                    // Ищем похожую дисциплину в исходном направлении
                    var sourceDisc = sourceDisciplines.FirstOrDefault(d =>
                        d.Title == targetDisc.Title ||
                        d.Title.Contains(targetDisc.Title.Split(' ')[0]) ||
                        targetDisc.Title.Contains(d.Title.Split(' ')[0]));

                    int hoursByPlan = targetDisc.TotalHours;
                    string assessmentTypeByPlan = targetDisc.AssessmentType;
                    int hoursByCertificate = sourceDisc?.TotalHours ?? 0;
                    string assessmentTypeByCertificate = sourceDisc?.AssessmentType ?? "-";
                    string grade = sourceDisc != null ? "зачтено" : "-";

                    int hoursDifference = hoursByPlan - hoursByCertificate;
                    string status;

                    if (sourceDisc == null)
                    {
                        // Дисциплины нет в исходном плане - полная задолженность
                        status = "debt";
                        totalDebt += hoursByPlan;
                    }
                    else if (hoursDifference <= 4 && hoursDifference >= 0)
                    {
                        // Разница 4 часа или меньше - перезачитывается
                        status = "transferred";
                        totalTransferred += hoursByPlan;
                        hoursDifference = 0;
                    }
                    else if (hoursDifference > 4)
                    {
                        // Большая разница - задолженность
                        status = "debt";
                        totalDebt += hoursDifference;
                    }
                    else
                    {
                        // Часов по справке больше - полностью перезачитывается
                        status = "transferred";
                        totalTransferred += hoursByPlan;
                        hoursDifference = 0;
                    }

                    var sheetDiscipline = new RetakeSheetDiscipline
                    {
                        RetakeSheetId = retakeSheet.Id,
                        DisciplineId = targetDisc.Id,
                        HoursByPlan = hoursByPlan,
                        AssessmentTypeByPlan = assessmentTypeByPlan,
                        HoursByCertificate = hoursByCertificate,
                        AssessmentTypeByCertificate = assessmentTypeByCertificate,
                        HoursDifference = hoursDifference,
                        Grade = grade,
                        Status = status,
                        RowNumber = rowNum++
                    };

                    _db.RetakeSheetDisciplines.Add(sheetDiscipline);
                }

                retakeSheet.TotalHoursTransferred = totalTransferred;
                retakeSheet.TotalHoursDebt = totalDebt;

                await _db.SaveChangesAsync();

                TempData["Success"] = $"✅ Лист перезачёта создан!\nПерезачтено: {totalTransferred} часов\nЗадолженность: {totalDebt} часов";

                return RedirectToAction("Print", new { id = retakeSheet.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Ошибка: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // GET: /RetakeSheet/Print/5
        public async Task<IActionResult> Print(int id)
        {
            var retakeSheet = await _db.RetakeSheets
                .Include(r => r.Student)
                .Include(r => r.Disciplines)
                    .ThenInclude(d => d.Discipline)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (retakeSheet == null)
                return NotFound();

            return View(retakeSheet);
        }
    }
}