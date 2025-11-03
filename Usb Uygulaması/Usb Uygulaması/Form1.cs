using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management; // WMI için
using System.Runtime.InteropServices; // Windows API için
using UsbUygulamasi.Data;
using UsbUygulamasi.Models;


namespace Usb_Uygulaması
{
    // Form1.cs içeriği

    public partial class Form1 : Form
    {
        private Dictionary<string, string> usbCache = new Dictionary<string, string>();
        // Windows API Sabitleri
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DBT_DEVTYP_VOLUME = 0x00000002;
        private int CurrentSecurityMode = 0;

        private void Form1_Load(object sender, EventArgs e)
        {
            // Uygulama ilk açıldığında logları DataGridView'a yükle
            LoadLogsToGrid();
            LoadTakmaAdlarToComboBox();
            LoadWhitelistGrid();
            
        }
        public Form1()
        {
            InitializeComponent();
            this.Text = "USB Loglama Uygulaması";
        }

        // **********************************************
        // A. USB Olaylarını Yakalama Metodu (WndProc)
        // **********************************************
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_DEVICECHANGE)
            {
                switch ((int)m.WParam)
                {
                    case DBT_DEVICEARRIVAL:
                        // Cihaz takıldı
                        HandleDeviceChange("USB Takıldı", m.LParam);
                        break;

                    case DBT_DEVICEREMOVECOMPLETE:
                        // Cihaz çıkarıldı
                        HandleDeviceChange("USB Çıkarıldı", m.LParam);
                        break;
                }
            }
        }

        // **********************************************
        // B. Olayı İşleme ve Loglama Metodu
        // **********************************************
        // Çıkarılan cihazın seri numarasını bulmak için yeni bir parametre ekledik.
        private void HandleDeviceChange(string logTipi, IntPtr lParam)
        {
            // Sadece USB bellek/disk olaylarını dikkate al
            var devType = Marshal.ReadInt32(lParam, 4);

            if (devType == DBT_DEVTYP_VOLUME)
            {
                string driveLetter = GetDriveLetter(lParam);

                // ✨ ANA İŞ PARÇACIĞINDA GEREKLİ DEĞERLERİ GÜVENLİCE AL
                int currentMode = CurrentSecurityMode; // 0 veya 2

                // Whitelist modu açıksa, DataGridView2'den (dgvWhitelist) seçilen satırın Seri Numarasını al
                // Ancak bu çok karışık: Engelleme kontrolünde Whitelist tablosunu direkt kullanıyoruz. 
                // Burada sadece bir placeholder değer gönderelim, çünkü kontrolü DB'den yapıyoruz.
                string selectedWhitelist = "DB_CONTROL";

                // Arka planda loglama işlemini başlat
                Task.Run(() =>
                {
                    LogUsbEvent(logTipi, driveLetter, currentMode, selectedWhitelist);

                    // UI güncellemeleri
                    this.Invoke((MethodInvoker)delegate
                    {
                        MessageBox.Show(logTipi + " olayı yakalandı ve loglandı. Sürücü Harfi: " + driveLetter, "USB Olayı");
                        LoadLogsToGrid();
                        LoadWhitelistGrid(); // Whitelist Grid'i güncelleyelim
                    });
                });
            }
        }
        // Form1.cs sınıfının içine ekleyin
        private void DisableDevice(string seriNumarasi, string cihazAdi)
        {
            try
            {
                // 1. WMI Sorgusunu Hazırla ve Çalıştır
                string query = $"SELECT * FROM Win32_PnPEntity WHERE PnPDeviceID LIKE '%{seriNumarasi}%' AND Caption LIKE '%USB%'";

                // searcher burada tanımlanır.
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    // 2. WMI Sonuçlarını Döngüye Al
                    foreach (ManagementObject device in searcher.Get())
                    {
                        // Cihazı devre dışı bırakmak için metodu çağır
                        uint result = (uint)device.InvokeMethod("Disable", null);

                        if (result == 0) // 0 başarı anlamına gelir
                        {
                            // Engellenen cihazın loglanması için LogUsbEvent'ı çağıralım:
                            // Loglama işlemini arka planda ayrı bir Task olarak yapalım
                            Task.Run(() =>
                            {
                                // 4 parametreli formatı kullan (Engellenme logu)
                                LogUsbEvent("USB Engellendi", "", 0, "");
                            });

                            // Kullanıcıya bilgi ver (UI Thread'e dönülmeli)
                            this.Invoke((MethodInvoker)delegate
                            {
                                MessageBox.Show($"{cihazAdi} Devre Dışı Bırakıldı (Whitelist Dışı).", "USB ENGELLENDİ", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            });
                            return;
                        }
                    }
                } // using bloğu burada biter
            }
            catch (Exception ex)
            {
                // Hata durumunda UI thread'de mesaj göster
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show("Cihazı Devre Dışı Bırakma Hatası (Yönetici Yetkisi Gerekir): " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }
        private void LoadTakmaAdlarToComboBox()
        {
            try
            {
                using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
                {
                    // Veritabanından benzersiz takma adları çekiyoruz
                    var takmaAdlar = dbContext.UsbLogs
                                               .Where(l => l.TakmaAd != null && l.TakmaAd != "")
                                               .Select(l => l.TakmaAd)
                                               .Distinct()
                                               .OrderBy(a => a)
                                               .ToList();

                    // ComboBox'a yükleme işlemi
                    comboBox1.Items.Clear();
                    comboBox1.Items.Add("-- Tümü --");
                    comboBox1.Items.AddRange(takmaAdlar.ToArray());
                    comboBox1.SelectedIndex = 0;

                    comboBox2.Items.Clear();
                    comboBox2.Items.Add("-- Tümü --");
                    comboBox2.Items.AddRange(takmaAdlar.ToArray());
                    comboBox2.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Takma Adları yüklerken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Windows mesajından sürücü harfini çıkaran yardımcı metot
        private string GetDriveLetter(IntPtr lParam)
        {
            DEV_BROADCAST_VOLUME devBroadcastVolume =
                (DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(lParam, typeof(DEV_BROADCAST_VOLUME));

            // Bit maskesini sürücü harfine dönüştür (0x1 = A, 0x2 = B, 0x4 = C, 0x8 = D...)
            int unit = devBroadcastVolume.dbcv_unitmask;

            for (int i = 0; i < 26; ++i)
            {
                if ((unit & 0x1) == 0x1)
                {
                    return ((char)('A' + i)).ToString() + ":";
                }
                unit >>= 1;
            }
            return string.Empty;
        }
        // Form1.cs içine ekleyin
        private void LoadWhitelistGrid()
        {
            try
            {
                using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
                {
                    var whitelist = dbContext.WhitelistEntries.ToList();
                    dataGridView2.DataSource = whitelist;

                    // Kullanıcı sadece Takma Ad ve Seri No'yu görsün
                    dataGridView2.Columns["Id"].Visible = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Whitelist yüklenemedi: " + ex.Message);
            }
        }
        private void ApplyFilters(
    string takmaAd, 
    string seriNo, 
    bool takildiChecked, 
    bool cikarildiChecked,
    DateTime baslangicTarihi, // ✨ YENİ: Başlangıç Tarihi
    DateTime bitisTarihi     // ✨ YENİ: Bitiş Tarihi
    )
{
    try
    {
        using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
        {
            var query = dbContext.UsbLogs.AsQueryable();

            // 1. TARİH ARALIĞI FİLTRESİ (En kritik filtre)
            
            // Başlangıç tarihi: Seçilen günün EN BAŞI (00:00:00)
            DateTime baslangic = baslangicTarihi.Date;
            
            // Bitiş tarihi: Seçilen günün EN SONU (23:59:59.999...)
            // Bitiş tarihini bir gün ileri alıp sadece o günden küçük olanları almalıyız ki o günün tamamı dahil olsun.
            DateTime bitis = bitisTarihi.Date.AddDays(1); 
            
            query = query.Where(l => l.TarihSaat >= baslangic && l.TarihSaat < bitis);


            // 2. TAKMA AD FİLTRESİ
            if (!string.IsNullOrEmpty(takmaAd) && takmaAd != "-- Tümü --")
            {
                query = query.Where(l => l.TakmaAd == takmaAd);
            }

            // 3. SERİ NO FİLTRESİ
            if (!string.IsNullOrWhiteSpace(seriNo))
            {
                // Seri numarasının içerdiği kayıtları getir
                query = query.Where(l => l.CihazSeriNo.Contains(seriNo));
            }

            // 4. OLAY TİPİ FİLTRESİ
            if (takildiChecked || cikarildiChecked)
            {
                if (takildiChecked && !cikarildiChecked)
                {
                    query = query.Where(l => l.OlayTipi == "USB Takıldı");
                }
                else if (!takildiChecked && cikarildiChecked)
                {
                    query = query.Where(l => l.OlayTipi == "USB Çıkarıldı");
                }
                // Hem takıldı hem çıkarıldı seçiliyse filtre uygulanmaz.
            }
            
            // Sonuçları al
            var filteredLogs = query.OrderByDescending(l => l.TarihSaat).ToList();
            dataGridView1.DataSource = filteredLogs;
            
            MessageBox.Show($"{filteredLogs.Count} adet kayıt bulundu.", "Filtre Sonucu");
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show("Filtreleme Hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

        // Windows API yapısını tanımla (Form1 sınıfı içine)
        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_VOLUME
        {
            public int dbcv_size;
            public int dbcv_devicetype;
            public int dbcv_reserved;
            public int dbcv_unitmask; // Sürücü harfini tutar (Bitmask)
        }

        // Metot imzası değişti! Artık sürücü harfini alıyor.
        // LogUsbEvent metodunuzun içindeki WMI sorgusu ve loglama mantığı:
        private void LogUsbEvent(string logTipi, string driveLetter, int currentMode, string selectedWhitelist)
        {
            // Hata ayıklama yardımcısının çakışmaması için Task.Run içinde olduğunuzu varsayıyoruz.
            try
            {
                string cihazAdi = "Bilinmiyor";
                string seriNumarasi = "Yok";
                string kullaniciAdi = Environment.UserName;
                string mevcutTakmaAd = null;

                // ----------------------------------------------------
                // CİHAZ BİLGİSİNİ ALMA VE ÖN BELLEĞE ALMA MANTIĞI (Aynı kalır)
                // ----------------------------------------------------
                if (logTipi == "USB Takıldı")
                {
                    ManagementObjectSearcher searcher =
                        new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");

                    foreach (ManagementObject drive in searcher.Get())
                    {
                        cihazAdi = drive["Caption"]?.ToString() ?? "Bilinmiyor";
                        seriNumarasi = drive["SerialNumber"]?.ToString() ?? "Yok";

                        if (!string.IsNullOrEmpty(seriNumarasi) && !usbCache.ContainsKey(driveLetter))
                        {
                            usbCache.Add(driveLetter, seriNumarasi);
                        }
                    }
                }
                else if (logTipi == "USB Çıkarıldı" && usbCache.ContainsKey(driveLetter))
                {
                    seriNumarasi = usbCache[driveLetter];

                    using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
                    {
                        var sonLog = dbContext.UsbLogs
                                                .Where(l => l.CihazSeriNo == seriNumarasi)
                                                .OrderByDescending(l => l.TarihSaat)
                                                .FirstOrDefault();

                        if (sonLog != null)
                        {
                            cihazAdi = sonLog.CihazAdi;
                        }
                    }
                    usbCache.Remove(driveLetter);
                }

                // -----------------------------------------------------------------
                // 1. MEVCUT TAKMA ADI VERİTABANINDAN ÇEKME (Aynı kalır)
                // -----------------------------------------------------------------
                if (!string.IsNullOrEmpty(seriNumarasi) && seriNumarasi != "Yok")
                {
                    using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
                    {
                        mevcutTakmaAd = dbContext.UsbLogs
                                                .Where(l => l.CihazSeriNo == seriNumarasi && l.TakmaAd != null)
                                                .OrderByDescending(l => l.TarihSaat)
                                                .Select(l => l.TakmaAd)
                                                .FirstOrDefault();
                    }
                }


                // -----------------------------------------------------------------
                // 2. WHİTELİST ENGELLLEME KONTROLÜ VE EYLEMİ (Düzeltildi)
                // -----------------------------------------------------------------
                if (currentMode == 2 && logTipi == "USB Takıldı" && seriNumarasi != "Yok")
                {
                    bool isWhitelisted = false;

                    try
                    {
                        using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
                        {
                            // KRİTİK KONTROL: Veritabanında eşleşen seri numarası var mı?
                            // Trim() kullanarak boşluklardan kaynaklanabilecek hataları önle
                            isWhitelisted = dbContext.WhitelistEntries
                                                     .Any(w => w.CihazSeriNo.Trim().Equals(seriNumarasi.Trim(), StringComparison.OrdinalIgnoreCase));
                        }

                        // Kontrol: Whitelist'te YOKSA, DEVRE DIŞI BIRAK!
                        if (!isWhitelisted)
                        {
                            // Devre dışı bırakma işlemi (Bu kodun başarılı olduğundan emin olmak için WMI sorgusu içindeki try-catch'i de kontrol ettik)
                            DisableDevice(seriNumarasi, cihazAdi);
                        }
                        else
                        {
                            // Loglama için bilgi
                            this.Invoke((MethodInvoker)delegate
                            {
                                System.Diagnostics.Debug.WriteLine($"Cihaz Whitelist'te, Engellenmedi: {cihazAdi}");
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Whitelist kontrolü sırasında hata olursa (DB bağlantısı gibi),
                        // cihazı engellemek yerine logla ve devam et (Güvenlikten ödün vermemek için).
                        this.Invoke((MethodInvoker)delegate
                        {
                            MessageBox.Show("Whitelist Kontrol Hatası: " + ex.Message, "Güvenlik Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        });
                    }
                }
                // -----------------------------------------------------------------


                // -----------------------------------------------------------------
                // 3. LOG KAYIT İŞLEMİ (Engellense bile loglanır)
                // -----------------------------------------------------------------
                using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
                {
                    var yeniLog = new UsbLog
                    {
                        TarihSaat = DateTime.Now,
                        OlayTipi = logTipi,
                        CihazAdi = cihazAdi,
                        CihazSeriNo = seriNumarasi,
                        KullaniciAdi = kullaniciAdi,
                        TakmaAd = mevcutTakmaAd
                    };

                    dbContext.UsbLogs.Add(yeniLog);
                    dbContext.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                // ... (Hata yönetimi aynı kalır)
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show("Loglama Hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }

        // **********************************************
        // C. Verileri Forms Ekranında Gösterme Metodu
        // **********************************************
        private void LoadLogsToGrid()
        {
            try
            {
                using (var dbContext = new ApplicationDbContext())
                {
                    // Tüm logları al ve TarihSaat'e göre ters kronolojik sırala
                    var logs = dbContext.UsbLogs
                                        .OrderByDescending(l => l.TarihSaat)
                                        .ToList();

                    // Form üzerindeki DataGridView'e verileri bağla
                    // Varsayım: Formunuzda adı 'dataGridView1' olan bir DataGridView var.
                    dataGridView1.DataSource = logs;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Logları yüklerken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Başlık satırına tıklanmadıysa
            if (e.RowIndex >= 0)
            {
                DataGridViewRow row = this.dataGridView1.Rows[e.RowIndex];

                // Seçilen satırdan Seri Numarasını ve Takma Adı alın
                string seriNo = row.Cells["CihazSeriNo"].Value.ToString();
                string cihazadi = row.Cells["CihazAdi"].Value.ToString();
                string takmaAd = row.Cells["TakmaAd"].Value != null ? row.Cells["TakmaAd"].Value.ToString() : "";

                // ✨ Label Güncelleme: (Sizin Label isimlerinizi kullanmalısınız. Örneğin:)
                // lblSecilenSeriNo.Text = "Seçilen Seri No: " + seriNo;
                // lblSecilenCihazAdi.Text = "Cihaz Adı: " + row.Cells["CihazAdi"].Value.ToString();

                // TextBox'a Takma Adı ve Seri Numarasını yerleştirin (Seri No arka planda tutulacak)
                textBox3.Text = takmaAd;

                // Seçilen seri numarayı bir değişkende tutalım (Aşağıdaki Buton için gerekli)
                this.textBox2.Text = seriNo;
                this.textBox1.Text = cihazadi;
                this.textBox5.Text = seriNo;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // 1. Gerekli Kontroller
            if (string.IsNullOrWhiteSpace(this.textBox2.Text))
            {
                MessageBox.Show("Lütfen önce tablodan bir kayıt seçin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string yeniTakmaAd = textBox3.Text.Trim();

            if (string.IsNullOrWhiteSpace(yeniTakmaAd))
            {
                MessageBox.Show("Takma Ad boş bırakılamaz.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
                {
                    // 2. Seri Numarasına Ait Tüm Logları Bul ve Güncelle
                    var logsToUpdate = dbContext.UsbLogs
                                                .Where(l => l.CihazSeriNo == this.textBox2.Text)
                                                .ToList();

                    foreach (var log in logsToUpdate)
                    {
                        log.TakmaAd = yeniTakmaAd;
                    }

                    dbContext.SaveChanges();

                    MessageBox.Show($"'{this.textBox2.Text}' seri numaralı cihazın Takma Adı başarıyla güncellendi.", "Başarılı");

                    // UI'ı yenile
                    LoadLogsToGrid();
                    textBox3.Clear();
                    this.textBox2.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Güncelleme Hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string secilenTakmaAd = comboBox1.SelectedItem?.ToString();
            string filtreSeriNo = textBox5.Text.Trim();
            bool takildi = checkBox1.Checked;
            bool cikarildi = checkBox2.Checked;
            DateTime baslangic = dateTimePicker2.Value;
            DateTime bitis = dateTimePicker1.Value;

            // Filtreleme metodunu çağır
            ApplyFilters(secilenTakmaAd, filtreSeriNo, takildi, cikarildi, baslangic, bitis);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // Filtre alanlarını temizle
            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0; // "-- Tümü --" seç
            }
            textBox5.Clear();
            checkBox1.Checked = false;
            checkBox2.Checked = false;

            // DataGridView'ı sıfırla (tüm logları yeniden yükle)
            LoadLogsToGrid();
            MessageBox.Show("Filtreler sıfırlandı, tüm kayıtlar gösteriliyor.", "Sıfırlama");
        }

        private void tableLayoutPanel5_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            
                // Daha önce CellClick ile doldurduğunuz SecilenSeriNo değişkenini kullanın veya
                // Doğrudan log tablosundan (dataGridView1) seçili satırı alın.
                if (dataGridView1.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Lütfen log listesinden (sol/üst tablo) bir cihaz seçin.", "Uyarı");
                    return;
                }

                // Log listesinden verileri al
                DataGridViewRow selectedRow = dataGridView1.SelectedRows[0];
                string seriNo = selectedRow.Cells["CihazSeriNo"].Value.ToString();
                string takmaAd = selectedRow.Cells["TakmaAd"].Value != null ? selectedRow.Cells["TakmaAd"].Value.ToString() : "";

                if (string.IsNullOrWhiteSpace(takmaAd))
                {
                    MessageBox.Show("Whitelist'e eklemeden önce cihazın Takma Adını tanımlamalısınız!", "Hata");
                    return;
                }

                try
                {
                    using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
                    {
                        // Tekrar eklenmesini önle
                        if (dbContext.WhitelistEntries.Any(w => w.CihazSeriNo == seriNo))
                        {
                            MessageBox.Show("Bu cihaz zaten Whitelist'te!", "Bilgi");
                            return;
                        }

                        var yeniEntry = new WhitelistEntry { TakmaAd = takmaAd, CihazSeriNo = seriNo };
                        dbContext.WhitelistEntries.Add(yeniEntry);
                        dbContext.SaveChanges();

                        LoadWhitelistGrid(); // Whitelist tablosunu yenile
                        MessageBox.Show($"'{takmaAd}' Whitelist'e eklendi.", "Başarılı");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Whitelist Ekleme Hatası: " + ex.Message);
                }
            }

        private void button5_Click(object sender, EventArgs e)
        {
            if (dataGridView2.SelectedRows.Count == 0)
            {
                MessageBox.Show("Lütfen Whitelist tablosundan (sağ/alt tablo) kaldırılacak bir kayıt seçin.", "Uyarı");
                return;
            }

            // Whitelist tablosundan Id'yi al
            int entryId = (int)dataGridView2.SelectedRows[0].Cells["Id"].Value;

            try
            {
                using (var dbContext = new UsbUygulamasi.Data.ApplicationDbContext())
                {
                    var entryToRemove = dbContext.WhitelistEntries.Find(entryId);
                    if (entryToRemove != null)
                    {
                        dbContext.WhitelistEntries.Remove(entryToRemove);
                        dbContext.SaveChanges();

                        LoadWhitelistGrid(); // Whitelist tablosunu yenile
                        MessageBox.Show($"'{entryToRemove.TakmaAd}' Whitelist'ten kaldırıldı.", "Başarılı");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Whitelist Kaldırma Hatası: " + ex.Message);
            }
        }
        private void EnableDevice(string seriNumarasi)
        {
            try
            {
                string query = $"SELECT * FROM Win32_PnPEntity WHERE PnPDeviceID LIKE '%{seriNumarasi}%' AND Caption LIKE '%USB%'";
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject device in searcher.Get())
                    {
                        // Cihazı etkinleştirmek için çağrılan metot
                        uint result = (uint)device.InvokeMethod("Enable", null);

                        if (result == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Cihaz Etkinleştirildi: {device["Caption"]}");
                            // Cihaz adını bulduysak, tek log girişi yeterli.
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Cihaz Etkinleştirme Hatası: " + ex.Message);
            }
        }
        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
            {
                // Whitelist Modu Açık
                CurrentSecurityMode = 2;
                MessageBox.Show("Güvenlik Modu: WHITELIST (Sadece İzin Verilenler Çalışır)", "Mod Değişikliği");
            }
            else
            {
                // Whitelist Modu Kapalı
                CurrentSecurityMode = 0;
                MessageBox.Show("Güvenlik Modu: KAPALI", "Mod Değişikliği");
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Uygulama kapanmadan önce Whitelist modunu otomatik olarak kapatalım
            if (CurrentSecurityMode == 2)
            {
                // Önce modu kapat
                CurrentSecurityMode = 0;

                // Sonra tüm USB'leri tekrar etkinleştirelim
                EnableAllDevices();
            }
        }
        private void EnableAllDevices()
        {
            // Cihaz Yöneticisindeki tüm USB aygıtlarını bul
            string query = "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%USB%'";

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject device in searcher.Get())
                    {
                        // Eğer cihaz durumu 'Devre Dışı' ise etkinleştirmeyi dene
                        uint result = (uint)device.InvokeMethod("Enable", null);

                        if (result == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Otomatik Etkinleştirildi: {device["Caption"]}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tüm USB'leri Etkinleştirme Hatası (Yönetici Yetkisi Gerekir!): " + ex.Message);
            }
        }
    }
}
