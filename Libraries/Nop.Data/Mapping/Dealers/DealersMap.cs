using System.Data.Entity.ModelConfiguration;
using Nop.Core.Domain.Dealers;

namespace Nop.Data.Mapping.Dealers
{
    public partial class DealersMap : EntityTypeConfiguration<DealerAccounts>
    {
        public DealersMap()
        {
            this.ToTable("Dealers");
            this.HasKey(d => d.Id);
            this.Property(d => d.DealerName);
            this.Property(d => d.DealerAcctNumber);

        }
    }
}
