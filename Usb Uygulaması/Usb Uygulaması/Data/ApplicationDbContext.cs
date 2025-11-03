// Data/ApplicationDbContext.cs
using System.Data.Entity;
using UsbUygulamasi.Models;

namespace UsbUygulamasi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<UsbLog> UsbLogs { get; set; }
        public DbSet<WhitelistEntry> WhitelistEntries { get; set; }
        // Bağlantı dizisinin adını belirtiyoruz. Bu ad, App.config'de tanımlı olmalı.
        public ApplicationDbContext()
            : base("name=UsbLogConnectionString")
        {
        }
    }
}
