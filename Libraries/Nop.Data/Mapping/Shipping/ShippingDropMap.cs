using System.Data.Entity.ModelConfiguration;
using Nop.Core.Domain.Shipping;

namespace Nop.Data.Mapping.Shipping
{
    public class ShippingDropMap : EntityTypeConfiguration<ShippingDrop>
    {
        public ShippingDropMap()
        {
            this.ToTable("ShippingDrop");
            this.HasKey(sd => sd.Id);
            this.Property(sd => sd.MinimumCharge).IsRequired().HasPrecision(4,2);
            this.Property(sd => sd.PercentageOfSubTotal).IsRequired();

        }
    }
}
