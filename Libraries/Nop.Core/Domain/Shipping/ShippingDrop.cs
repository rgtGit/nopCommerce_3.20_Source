using System.Collections.Generic;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;

namespace Nop.Core.Domain.Shipping
{
    /// <summary>
    /// Represents the current Drop
    /// </summary>
    public partial class ShippingDrop : BaseEntity, ILocalizedEntity
    {
        

        /// <summary>
        /// Gets or sets the Minimum Drop Charge
        /// </summary>
        public decimal MinimumCharge { get; set; }

        /// <summary>
        /// Gets or sets the description
        /// </summary>
        public int PercentageOfSubTotal  { get; set; }

        
    }
}