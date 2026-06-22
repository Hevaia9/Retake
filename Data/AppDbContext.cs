using Microsoft.EntityFrameworkCore;
using RetakeSystem.Models;

namespace RetakeSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<Discipline> Disciplines { get; set; }
        public DbSet<Direction> Directions { get; set; }
        public DbSet<RetakeSystem.Models.RetakeSheet> RetakeSheets { get; set; }
        public DbSet<RetakeSystem.Models.RetakeSheetDiscipline> RetakeSheetDisciplines { get; set; }
    }
}