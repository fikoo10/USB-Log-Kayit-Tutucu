// Models/UsbLog.cs
using System;
using System.ComponentModel.DataAnnotations; // Key niteliği için

namespace UsbUygulamasi.Models
{
    public class UsbLog
    {
        // EF bu alanı otomatik olarak Primary Key (Birincil Anahtar) yapar
        [Key]
        public int Id { get; set; }

        public DateTime TarihSaat { get; set; }
        public string OlayTipi { get; set; } // Örn: Takıldı / Çıkarıldı
        public string CihazAdi { get; set; }
        public string CihazSeriNo { get; set; }


        public string KullaniciAdi { get; set; }

        public string TakmaAd { get; set; } // Default olarak NULL girilecektir
    }
}