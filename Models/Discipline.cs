using System.Collections.Generic;

namespace RetakeSystem.Models
{
    public class Discipline
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AssessmentType { get; set; } = string.Empty;
        public int TotalHours { get; set; }
        public string SpecialtyCode { get; set; } = string.Empty;

        public ICollection<Direction> Directions { get; set; } = new List<Direction>();
    }
}