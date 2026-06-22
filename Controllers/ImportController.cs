using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using RetakeSystem.Data;
using RetakeSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace RetakeSystem.Controllers
{
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

        // POST: /Import/Results
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

                // === Извлечение группы и семестра ===
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

                // === Поиск заголовков ===
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

                // === Получение названий дисциплин из заголовка ===
                // ВАЖНО: Берём из строки ПОСЛЕ headerRow (где цифры 1,2,3...)
                var disciplineNames = new List<string>();
                int lastCol = worksheet.LastColumnUsed().ColumnNumber();
                int skipColumnsFromEnd = 9; // Пропускаем последние 9 колонок (статистика)

                for (int col = studentCol + 1; col <= lastCol - skipColumnsFromEnd; col++)
                {
                    try
                    {
                        // Берём из СЛЕДУЮЩЕЙ строки после заголовка с цифрами
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
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Пропущена колонка {col}: {ex.Message}");
                        continue;
                    }
                }

                if (disciplineNames.Count == 0)
                {
                    TempData["Error"] = "Не найдены названия дисциплин в файле";
                    return RedirectToAction("Index");
                }

                // === Чтение данных студентов ===
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

                        // === Чтение оценок ===
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

                            // Ищем задолженности
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
                string errorMsg = $"❌ Ошибка при импорте: {ex.Message}";

                if (ex.InnerException != null)
                {
                    errorMsg += $"\nВнутренняя ошибка: {ex.InnerException.Message}";
                }

                TempData["Error"] = errorMsg;
            }

            return RedirectToAction("Index");
        }

        // POST: /Import/Curriculum
        [HttpPost]
        public async Task<IActionResult> ImportCurriculum(IFormFile file, string specialtyCode)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Файл не выбран";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrEmpty(specialtyCode))
            {
                TempData["Error"] = "Не указан код специальности";
                return RedirectToAction("Index");
            }

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                int importedCount = 0;
                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
                var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

                // 1. Ищем строку-заголовок таблицы дисциплин
                // Ключевые слова: "Индекс" и "Наименование"
                int headerRow = 0;
                int indexCol = 0;
                int nameCol = 0;
                int hoursCol = 0;
                int assessmentCol = 0;

                for (int row = 1; row <= lastRow; row++)
                {
                    // Проверяем ячейки в строке, ищем заголовки
                    for (int col = 1; col <= Math.Min(20, lastCol); col++)
                    {
                        try
                        {
                            var cellValue = worksheet.Cell(row, col).GetString().Trim().ToLower();

                            // Ищем колонку "Индекс"
                            if (cellValue.Contains("индекс") && indexCol == 0)
                            {
                                headerRow = row;
                                indexCol = col;
                            }

                            // Ищем колонку "Наименование" (или похожее)
                            if (cellValue.Contains("наименование") && nameCol == 0)
                            {
                                nameCol = col;
                            }

                            // Ищем колонку "объём образовательной нагрузки" (или похожее)
                            // Важно: в сводной таблице тоже есть "объем", но там нет "индекса" в соседних колонках
                            if ((cellValue.Contains("объём образовательной нагрузки") ||
                                 cellValue.Contains("объем образовательной нагрузки") ||
                                 (cellValue.Contains("всего") && cellValue.Contains("час"))) && hoursCol == 0)
                            {
                                hoursCol = col;
                            }
                        }
                        catch { continue; }
                    }

                    // Если нашли и Индекс, и Наименование - значит заголовок найден
                    if (headerRow > 0 && nameCol > 0)
                    {
                        // Если не нашли колонку часов жестко по названию, ищем её логически (обычно правее названий)
                        if (hoursCol == 0)
                        {
                            // Ищем колонку справа от названий, которая содержит числа
                            for (int col = nameCol + 5; col <= Math.Min(lastCol, nameCol + 20); col++)
                            {
                                var headerCell = worksheet.Cell(row, col).GetString().Trim().ToLower();
                                if (headerCell.Contains("объём") || headerCell.Contains("всего") || headerCell.Contains("нагрузка"))
                                {
                                    hoursCol = col;
                                    break;
                                }
                            }
                        }

                        // Ищем колонку с аттестацией (обычно это колонки семестров с буквами Э, З, ДЗ)
                        for (int col = indexCol + 2; col <= Math.Min(lastCol, indexCol + 10); col++)
                        {
                            var headerCell = worksheet.Cell(row, col).GetString().Trim().ToLower();
                            if (headerCell.Contains("семестр") || headerCell.Contains("аттест"))
                            {
                                assessmentCol = col; // Берем первый семестр как пример, логику уточним в цикле
                                break;
                            }
                        }

                        break; // Заголовок найден, выходим из поиска
                    }
                }

                if (headerRow == 0 || nameCol == 0)
                {
                    TempData["Error"] = "Не удалось найти таблицу дисциплин. Убедитесь, что файл содержит колонки 'Индекс' и 'Наименование'.";
                    return RedirectToAction("Index");
                }

                // Удаляем старые дисциплины этой специальности перед импортом
                var oldDisciplines = await _db.Disciplines
                    .Where(d => d.SpecialtyCode == specialtyCode)
                    .ToListAsync();
                _db.Disciplines.RemoveRange(oldDisciplines);
                await _db.SaveChangesAsync();

                // 2. Читаем строки с дисциплинами
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

                        // --- ФИЛЬТРАЦИЯ ---

                        // 1. Пропускаем заголовки циклов (ОУП.00, ОГСЭ.00 и т.д.)
                        // Обычно у них нет точки с цифрой после, или название содержит "цикл"
                        if (title.Contains("цикл") || title.Contains("ПРОМЕЖУТОЧНАЯ") || title.Contains("ГИА"))
                            continue;

                        // 2. Проверяем, что индекс похож на код дисциплины (содержит точку)
                        // Например: ОУП.01, ОП.01, МДК.01
                        if (!index.Contains("."))
                            continue;

                        // 3. Игнорируем строки "Общие учебные предметы" и подобные (у них нет индекса с точкой)
                        if (string.IsNullOrEmpty(index) || index.Length < 3)
                            continue;

                        // --- ИЗВЛЕЧЕНИЕ ДАННЫХ ---

                        // Часы
                        int totalHours = 0;
                        if (hoursCol > 0)
                        {
                            var hoursCell = worksheet.Cell(row, hoursCol);
                            if (hoursCell.DataType == XLDataType.Number)
                            {
                                totalHours = (int)hoursCell.GetDouble();
                            }
                            else
                            {
                                int.TryParse(hoursCell.GetString().Trim(), out totalHours);
                            }
                        }

                        // Если часов 0, пропускаем (возможно это заголовок раздела)
                        if (totalHours == 0) continue;

                        // Вид аттестации
                        string assessmentType = "ДЗ"; // По умолчанию
                                                      // Ищем в колонках семестров (обычно справа от названия) символы Э, З, ДЗ
                                                      // Проверяем диапазон колонок семестров (обычно это колонки 3-8 относительно начала таблицы)
                        for (int col = indexCol + 2; col <= Math.Min(lastCol, indexCol + 15); col++)
                        {
                            try
                            {
                                var val = worksheet.Cell(row, col).GetString().Trim();
                                if (val == "Э" || val == "З" || val == "ДЗ" || val == "Экв" || val == "ДЭ")
                                {
                                    assessmentType = val;
                                    // Не break, потому что нам нужно последнее значение (итоговая аттестация), 
                                    // но часто достаточно первого найденного. Оставим последнее.
                                }
                            }
                            catch { }
                        }

                        // Сохраняем в БД
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
                        Console.WriteLine($"Ошибка при обработке строки {row}: {ex.Message}");
                        continue;
                    }
                }

                await _db.SaveChangesAsync();

                TempData["Success"] = $"✅ Успешно импортировано {importedCount} дисциплин для {specialtyCode}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Ошибка: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}