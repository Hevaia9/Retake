using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetakeSystem.Data;
using RetakeSystem.Models;

namespace RetakeSystem.Controllers
{
    public class DirectionsController : Controller
    {
        private readonly AppDbContext _db;

        public DirectionsController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /Directions - список всех направлений с фильтрацией
        public async Task<IActionResult> Index(string studentFilter, string groupFilter,
                                              string disciplineFilter, string statusFilter)
        {
            var query = _db.Directions
                .Include(d => d.Student)
                .Include(d => d.Discipline)
                .AsQueryable();

            // Применяем фильтры (регистронезависимые)
            if (!string.IsNullOrEmpty(studentFilter))
            {
                query = query.Where(d => d.Student.FullName.ToLower().Contains(studentFilter.ToLower()));
            }

            if (!string.IsNullOrEmpty(groupFilter))
            {
                query = query.Where(d => d.Student.Group.ToLower().Contains(groupFilter.ToLower()));
            }

            if (!string.IsNullOrEmpty(disciplineFilter))
            {
                query = query.Where(d => d.Discipline.Title.ToLower().Contains(disciplineFilter.ToLower()));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(d => d.Status == statusFilter);
            }

            var directions = await query
                .OrderByDescending(d => d.DateStart)
                .ToListAsync();

            // Сохраняем значения фильтров для отображения в форме
            ViewBag.StudentFilter = studentFilter;
            ViewBag.GroupFilter = groupFilter;
            ViewBag.DisciplineFilter = disciplineFilter;
            ViewBag.StatusFilter = statusFilter;

            return View(directions);
        }

        // POST: /Directions/Delete/5 - удаление одного направления
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var direction = await _db.Directions.FindAsync(id);
            if (direction == null)
                return NotFound();

            _db.Directions.Remove(direction);
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Направление удалено!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Directions/DeleteAll - удаление всех направлений
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAll()
        {
            var directions = await _db.Directions.ToListAsync();
            _db.Directions.RemoveRange(directions);
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Все направления удалены!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Directions/Print/5 - печатная форма
        public async Task<IActionResult> Print(int id)
        {
            var direction = await _db.Directions
                .Include(d => d.Student)
                .Include(d => d.Discipline)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (direction == null)
                return NotFound();

            return View(direction);
        }

        // POST: /Directions/SetGrade - выставление оценки
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetGrade(int id, int estimate)
        {
            var direction = await _db.Directions.FindAsync(id);
            if (direction == null)
                return NotFound();

            direction.Estimate = estimate;
            direction.Status = "closed";
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Оценка выставлена!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Directions/UpdateDateEnd - обновление срока действия
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDateEnd(int id, DateTime dateEnd)
        {
            var direction = await _db.Directions.FindAsync(id);
            if (direction == null)
                return NotFound();

            // Конвертируем в UTC для PostgreSQL
            if (dateEnd.Kind != DateTimeKind.Utc)
            {
                dateEnd = DateTime.SpecifyKind(dateEnd, DateTimeKind.Utc);
            }

            direction.DateEnd = dateEnd;
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Срок действия обновлен!";
            return RedirectToAction(nameof(Index));
        }
    }
}