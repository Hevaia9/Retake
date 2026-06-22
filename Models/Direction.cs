namespace RetakeSystem.Models
{
    public class Direction
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;

        // Изменил тип на DateTime с указанием UTC
        public DateTime DateStart { get; set; } = DateTime.UtcNow;
        public DateTime DateEnd { get; set; } = DateTime.UtcNow.AddDays(14);

        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public int DisciplineId { get; set; }
        public Discipline? Discipline { get; set; }

        public string? TicketNumber { get; set; }
        public int? Estimate { get; set; }
        public string Status { get; set; } = "active";
    }
}