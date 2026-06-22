using System;
using System.Collections.Generic;

namespace RetakeSystem.Models
{
    public class RetakeSheet
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public string FromSpecialty { get; set; } = string.Empty;
        public string ToSpecialty { get; set; } = string.Empty;

        public int TotalHoursTransferred { get; set; }
        public int TotalHoursDebt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<RetakeSheetDiscipline> Disciplines { get; set; } = new List<RetakeSheetDiscipline>();
    }

    public class RetakeSheetDiscipline
    {
        public int Id { get; set; }

        public int RetakeSheetId { get; set; }
        public RetakeSheet RetakeSheet { get; set; }

        public int DisciplineId { get; set; }
        public Discipline Discipline { get; set; }

        // Часы по учебному плану (целевое направление)
        public int HoursByPlan { get; set; }

        // Вид аттестации по учебному плану
        public string AssessmentTypeByPlan { get; set; } = string.Empty;

        // Часы по академической справке (исходное направление)
        public int HoursByCertificate { get; set; }

        // Вид аттестации по справке
        public string AssessmentTypeByCertificate { get; set; } = string.Empty;

        // Разница часов
        public int HoursDifference { get; set; }

        // Оценка по справке
        public string Grade { get; set; } = string.Empty;

        // Статус: "transferred" или "debt"
        public string Status { get; set; } = string.Empty;

        // Порядковый номер в таблице
        public int RowNumber { get; set; }
    }
}