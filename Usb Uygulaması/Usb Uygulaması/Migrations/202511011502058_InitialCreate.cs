namespace Usb_Uygulaması.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.UsbLogs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TarihSaat = c.DateTime(nullable: false),
                        OlayTipi = c.String(),
                        CihazAdi = c.String(),
                        CihazSeriNo = c.String(),
                        KullaniciAdi = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.UsbLogs");
        }
    }
}
