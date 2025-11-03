// Models/WhitelistEntry.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UsbUygulamasi.Models
{
    // Cihazı Takma Adı ve Seri Numarasıyla tanımlayan tablo
    public class WhitelistEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string TakmaAd { get; set; } // Kullanıcının atadığı isim

        [Required]
        public string CihazSeriNo { get; set; } // Gerçek seri numarası
    }
}