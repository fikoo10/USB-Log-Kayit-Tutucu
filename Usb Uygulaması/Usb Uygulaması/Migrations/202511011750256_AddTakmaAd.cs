namespace Usb_Uygulaması.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddTakmaAd : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.UsbLogs", "TakmaAd", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.UsbLogs", "TakmaAd");
        }
    }
}
