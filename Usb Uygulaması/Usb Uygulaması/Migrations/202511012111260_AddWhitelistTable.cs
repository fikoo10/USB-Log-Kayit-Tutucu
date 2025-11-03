namespace Usb_Uygulaması.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddWhitelistTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.WhitelistEntries",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TakmaAd = c.String(nullable: false),
                        CihazSeriNo = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.WhitelistEntries");
        }
    }
}
