using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using RetakeSystem.Data;
using RetakeSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace RetakeSystem.Controllers
{
    [Authorize]
    public class ImportController : Controller
    {
        private readonly AppDbContext _db;

        public ImportController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /Import
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Import/Results - импорт ведомостей (СУЩЕСТВУЮЩИЙ МЕТОД, НЕ ТРОГАЕМ)
        [HttpPost]
        public async Task<IActionResult> ImportResults(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Файл не выбран";
                return RedirectToAction("Index");
            }

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                string group = "";
                int semester = 0;
                int course = 0;

                for (int row = 1; row <= 10; row++)
                {
                    var cellValue = worksheet.Cell(row, 1).GetString();
                    if (string.IsNullOrEmpty(cellValue)) continue;

                    var groupMatch = Regex.Match(cellValue, @"ИСП[-–\s]?(\d+)", RegexOptions.IgnoreCase);
                    if (groupMatch.Success)
                    {
                        group = $"ИСП-{groupMatch.Groups[1].Value}";
                        int groupNum = int.Parse(groupMatch.Groups[1].Value);
                        course = groupNum / 10;
                    }

                    var semMatch = Regex.Match(cellValue, @"(\d+|[IVX]+)\s*семестр", RegexOptions.IgnoreCase);
                    if (semMatch.Success)
                    {
                        string sem = semMatch.Groups[1].Value;
                        if (sem == "III") semester = 3;
                        else if (sem == "IV") semester = 4;
                        else if (sem == "V") semester = 5;
                        else if (sem == "VI") semester = 6;
                        else if (int.TryParse(sem, out int s)) semester = s;
                    }
                }

                int headerRow = 0;
                int studentCol = 0;

                for (int row = 1; row <= 20; row++)
                {
                    for (int col = 1; col <= 5; col++)
                    {
                        var cellValue = worksheet.Cell(row, col).GetString().ToLower();
                        if (cellValue.Contains("имя") || cellValue.Contains("фамилия") ||
                            cellValue.Contains("студента"))
                        {
                            headerRow = row;
                            studentCol = col;
                            break;
                        }
                    }
                    if (headerRow > 0) break;
                }

                if (headerRow == 0)
                {
                    TempData["Error"] = "Не удалось найти заголовок с ФИО студентов";
                    return RedirectToAction("Index");
                }

                var disciplineNames = new List<string>();
                int lastCol = worksheet.LastColumnUsed().ColumnNumber();
                int skipColumnsFromEnd = 9;

                for (int col = studentCol + 1; col <= lastCol - skipColumnsFromEnd; col++)
                {
                    try
                    {
                        var discName = worksheet.Cell(headerRow + 1, col).GetString().Trim();

                        if (string.IsNullOrEmpty(discName) ||
                            discName.Contains("средний балл", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("всего", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("КЗ", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("УСП", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("не аттестованы", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("хорошисты", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("отличники", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("неуспевающие", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("качество", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("успеваемость", StringComparison.OrdinalIgnoreCase) ||
                            discName.Contains("н/а", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        disciplineNames.Add(discName);
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (disciplineNames.Count == 0)
                {
                    TempData["Error"] = "Не найдены названия дисциплин в файле";
                    return RedirectToAction("Index");
                }

                int importedCount = 0;
                int debtCount = 0;

                for (int row = headerRow + 2; row <= worksheet.LastRowUsed().RowNumber(); row++)
                {
                    try
                    {
                        var studentName = worksheet.Cell(row, studentCol).GetString().Trim();

                        if (string.IsNullOrEmpty(studentName) ||
                            studentName.Length < 5 ||
                            studentName.Contains("средний балл") ||
                            studentName.Contains("всего") ||
                            studentName.Contains("не аттестованы"))
                        {
                            continue;
                        }

                        var existingStudent = await _db.Students
                            .FirstOrDefaultAsync(s => s.FullName == studentName && s.Group == group);

                        if (existingStudent == null)
                        {
                            existingStudent = new Student
                            {
                                FullName = studentName,
                                Group = group,
                                Course = course,
                                Semester = semester
                            };
                            _db.Students.Add(existingStudent);
                            await _db.SaveChangesAsync();
                        }

                        importedCount++;

                        for (int i = 0; i < disciplineNames.Count; i++)
                        {
                            int col = studentCol + 1 + i;
                            if (col > lastCol) break;

                            string grade = "";
                            try
                            {
                                var gradeCell = worksheet.Cell(row, col);

                                if (gradeCell.DataType == XLDataType.Text)
                                {
                                    grade = gradeCell.GetString().Trim().ToLower();
                                }
                                else if (gradeCell.DataType == XLDataType.Number)
                                {
                                    grade = gradeCell.GetDouble().ToString("F0").Trim().ToLower();
                                }
                                else
                                {
                                    grade = gradeCell.GetValue<string>().Trim().ToLower();
                                }
                            }
                            catch
                            {
                                continue;
                            }

                            if (string.IsNullOrEmpty(grade)) continue;

                            if (grade == "2" || grade.Contains("не яв") || grade == "неяв")
                            {
                                string disciplineTitle = disciplineNames[i];

                                var discipline = await _db.Disciplines
                                    .FirstOrDefaultAsync(d => d.Title == disciplineTitle);

                                if (discipline == null)
                                {
                                    discipline = new Discipline
                                    {
                                        Title = disciplineTitle,
                                        Code = "",
                                        AssessmentType = "Э",
                                        TotalHours = 0
                                    };
                                    _db.Disciplines.Add(discipline);
                                    await _db.SaveChangesAsync();
                                }

                                var existingDirection = await _db.Directions
                                    .FirstOrDefaultAsync(d => d.StudentId == existingStudent.Id &&
                                                             d.DisciplineId == discipline.Id &&
                                                             d.Status == "active");

                                if (existingDirection == null)
                                {
                                    var direction = new Direction
                                    {
                                        Number = $"П-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                                        DateStart = DateTime.UtcNow,
                                        DateEnd = DateTime.UtcNow.AddDays(14),
                                        StudentId = existingStudent.Id,
                                        DisciplineId = discipline.Id,
                                        Status = "active"
                                    };

                                    _db.Directions.Add(direction);
                                    debtCount++;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при обработке строки {row}: {ex.Message}");
                    }
                }

                await _db.SaveChangesAsync();

                TempData["Success"] = $"✅ Загружено: {importedCount} студентов\n" +
                                     $"📋 Создано направлений: {debtCount}\n" +
                                     $"Группа: {group}, {semester} семестр";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Ошибка: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // POST: /Import/Curriculum - НОВЫЙ ПРАВИЛЬНЫЙ КОД ДЛЯ УЧЕБНЫХ ПЛАНОВ
        [HttpPost]
        public async Task<IActionResult> ImportCurriculum(IFormFile file, string specialtyCode)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Файл не выбран";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(specialtyCode))
            {
                TempData["Error"] = "Не указан код специальности";
                return RedirectToAction("Index");
            }

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);

                // Ищем лист "уч_план" - именно там находится таблица с дисциплинами
                IXLWorksheet worksheet;
                var planSheet = workbook.Worksheets.FirstOrDefault(w =>
                    w.Name.ToLower().Contains("уч_план") ||
                    w.Name.ToLower().Contains("уч_plan") ||
                    w.Name.ToLower().Contains("план"));

                if (planSheet != null)
                {
                    worksheet = planSheet;
                }
                else
                {
                    worksheet = workbook.Worksheet(1);
                }

                int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
                int lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

                // ШАГ 1: Ищем строку с заголовками "Индекс" и "Наименование"
                int headerRow = 0;
                int indexCol = 0;
                int nameCol = 0;
                int hoursCol = 0;

                for (int row = 1; row <= Math.Min(50, lastRow); row++)
                {
                    for (int col = 1; col <= Math.Min(20, lastCol); col++)
                    {
                        try
                        {
                            var cellValue = worksheet.Cell(row, col).GetString().Trim().ToLower();

                            if (cellValue.Contains("индекс") && indexCol == 0)
                            {
                                headerRow = row;
                                indexCol = col;
                            }
                            else if (cellValue.Contains("наименование") && nameCol == 0 && headerRow > 0)
                            {
                                nameCol = col;
                            }
                        }
                        catch { continue; }
                    }

                    if (headerRow > 0 && nameCol > 0) break;
                }

                if (headerRow == 0 || nameCol == 0)
                {
                    TempData["Error"] = $"Не удалось найти заголовки 'Индекс' и 'Наименование'. Найдено: Индекс={indexCol}, Наименование={nameCol}";
                    return RedirectToAction("Index");
                }

                // ШАГ 2: Ищем колонку "объём образовательной нагрузки" в строке заголовков
                for (int col = nameCol + 1; col <= Math.Min(lastCol, nameCol + 20); col++)
                {
                    try
                    {
                        // Проверяем несколько строк заголовков
                        for (int r = headerRow; r <= headerRow + 5; r++)
                        {
                            var cellValue = worksheet.Cell(r, col).GetString().Trim().ToLower();
                            if (cellValue.Contains("объём образовательной нагрузки") ||
                                cellValue.Contains("объем образовательной нагрузки") ||
                                (cellValue.Contains("образовательной нагрузки") && cellValue.Contains("объём")))
                            {
                                hoursCol = col;
                                break;
                            }
                        }
                        if (hoursCol > 0) break;
                    }
                    catch { continue; }
                }

                // Если не нашли по названию, ищем по позиции (обычно это колонка после семестров)
                if (hoursCol == 0)
                {
                    // Считаем количество колонок с семестрами
                    int semestersCount = 0;
                    for (int col = nameCol + 1; col <= Math.Min(lastCol, nameCol + 15); col++)
                    {
                        try
                        {
                            for (int r = headerRow; r <= headerRow + 3; r++)
                            {
                                var cellValue = worksheet.Cell(r, col).GetString().Trim().ToLower();
                                if (cellValue.Contains("семестр"))
                                {
                                    semestersCount++;
                                    break;
                                }
                            }
                        }
                        catch { continue; }
                    }

                    // Колонка с часами обычно идёт сразу после семестров
                    hoursCol = nameCol + semestersCount + 1;
                }

                // ШАГ 3: Определяем диапазон колонок с формами аттестации
                int assessmentStartCol = nameCol + 1;
                int assessmentEndCol = hoursCol - 1;

                // ШАГ 4: Удаляем старые дисциплины этой специальности
                var oldDisciplines = await _db.Disciplines
                    .Where(d => d.SpecialtyCode == specialtyCode)
                    .ToListAsync();
                _db.Disciplines.RemoveRange(oldDisciplines);
                await _db.SaveChangesAsync();

                // ШАГ 5: Читаем дисциплины
                int importedCount = 0;
                int skippedCount = 0;

                for (int row = headerRow + 1; row <= lastRow; row++)
                {
                    try
                    {
                        var indexCell = worksheet.Cell(row, indexCol);
                        var nameCell = worksheet.Cell(row, nameCol);

                        // Пропускаем пустые строки
                        if (indexCell.IsEmpty() && nameCell.IsEmpty())
                            continue;

                        var index = indexCell.GetString().Trim();
                        var title = nameCell.GetString().Trim();

                        if (string.IsNullOrEmpty(index) || string.IsNullOrEmpty(title))
                            continue;

                        // Пропускаем заголовки циклов (ОУП.00, СГ.00, ОП.00, ПМ.00 и т.д.)
                        if (Regex.IsMatch(index, @"\.\s*00\s*$") ||
                            title.Contains("цикл", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("Всего", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("ГИА", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("промежуточная аттестация", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("Общие учебные предметы", StringComparison.OrdinalIgnoreCase))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Пропускаем строки без точки в индексе (это не дисциплины)
                        if (!index.Contains(".") && !index.Contains("МДК") && !index.Contains("УП") && !index.Contains("ПП"))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Получаем часы из колонки "объём образовательной нагрузки"
                        int totalHours = 0;
                        if (hoursCol > 0 && hoursCol <= lastCol)
                        {
                            var hoursCell = worksheet.Cell(row, hoursCol);
                            if (!hoursCell.IsEmpty())
                            {
                                if (hoursCell.DataType == XLDataType.Number)
                                {
                                    totalHours = (int)hoursCell.GetDouble();
                                }
                                else if (hoursCell.DataType == XLDataType.Text)
                                {
                                    var hoursText = hoursCell.GetString().Trim();
                                    int.TryParse(Regex.Replace(hoursText, @"[^\d]", ""), out totalHours);
                                }
                            }
                        }

                        // Если не нашли часы, ищем в соседних колонках
                        if (totalHours == 0)
                        {
                            for (int col = hoursCol - 2; col <= hoursCol + 3; col++)
                            {
                                if (col < 1 || col > lastCol) continue;
                                try
                                {
                                    var cell = worksheet.Cell(row, col);
                                    if (cell.DataType == XLDataType.Number)
                                    {
                                        int hours = (int)cell.GetDouble();
                                        if (hours > 10 && hours < 2000)
                                        {
                                            totalHours = hours;
                                            break;
                                        }
                                    }
                                }
                                catch { continue; }
                            }
                        }

                        if (totalHours == 0)
                        {
                            skippedCount++;
                            continue;
                        }

                        // Определяем вид аттестации (берём последнюю непустую в диапазоне семестров)
                        string assessmentType = "";
                        for (int col = assessmentEndCol; col >= assessmentStartCol; col--)
                        {
                            try
                            {
                                var cellValue = worksheet.Cell(row, col).GetString().Trim();

                                // Проверяем на формы аттестации
                                if (cellValue == "Э" || cellValue == "Экв" || cellValue == "ДЭ")
                                {
                                    assessmentType = cellValue;
                                    break; // Экзамен имеет приоритет
                                }
                                else if (cellValue == "З" && string.IsNullOrEmpty(assessmentType))
                                {
                                    assessmentType = "З";
                                }
                                else if ((cellValue == "ДЗ" || cellValue.StartsWith("ДЗ")) && string.IsNullOrEmpty(assessmentType))
                                {
                                    assessmentType = "ДЗ";
                                }
                                else if (Regex.IsMatch(cellValue, @"^(Э|З|ДЗ|Экв|ДЭ)\d*$"))
                                {
                                    // Обрабатываем варианты типа "ДЗ1", "ДЗ2", "ДЗ3"
                                    var match = Regex.Match(cellValue, @"^(Э|З|ДЗ|Экв|ДЭ)");
                                    if (match.Success)
                                    {
                                        assessmentType = match.Groups[1].Value;
                                    }
                                }
                            }
                            catch { continue; }
                        }

                        if (string.IsNullOrEmpty(assessmentType))
                        {
                            assessmentType = "ДЗ"; // По умолчанию - диф. зачёт
                        }

                        // Сохраняем дисциплину
                        var discipline = new Discipline
                        {
                            Code = index,
                            Title = title,
                            AssessmentType = assessmentType,
                            TotalHours = totalHours,
                            SpecialtyCode = specialtyCode
                        };

                        _db.Disciplines.Add(discipline);
                        importedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка строки {row}: {ex.Message}");
                        continue;
                    }
                }

                await _db.SaveChangesAsync();

                TempData["Success"] = $"✅ Импортировано дисциплин для {specialtyCode}: {importedCount}\n" +
                                     $"⚠️ Пропущено строк: {skippedCount}\n" +
                                     $"📊 Колонок: Индекс={indexCol}, Название={nameCol}, Часы={hoursCol}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Ошибка: {ex.Message}\n\nStack: {ex.StackTrace}";
            }

            return RedirectToAction("Index");
        }
    }
}