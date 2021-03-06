using Cybtans.Entities;
using Cybtans.Entities.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace @{SERVICE}.Data.Repositories
{
    public class @{SERVICE}Context : DbContext, IEntityEventLogContext
    {
        public @{SERVICE}Context()
        {
        }

        public @{SERVICE}Context(DbContextOptions<@{SERVICE}Context> options)
            : base(options)
        {
        }       

        public DbSet<EntityEventLog> EntityEventLogs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                //optionsBuilder.UseSqlite("Data Source=@{SERVICE};Mode=Memory;Cache=Shared");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
        }
    }
}
