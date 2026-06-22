namespace RetakeSystem.Models
{
    public class Student
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public int Course { get; set; }
        public int Semester { get; set; }

        public ICollection<Direction> Directions { get; set; } = new List<Direction>();
    }
}