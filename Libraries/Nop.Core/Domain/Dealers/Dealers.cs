using System;

namespace Nop.Core.Domain.Dealers
{
    public partial class DealerAccounts : BaseEntity
    {
    /// This was added to nopCommerce template.
    /// For table DealerAccount.
    
        /// <summary>
        /// Gets Dealer Account No.
        /// </summary>
        public string DealerAcctNumber { get; set; }

        /// <summary>
        /// Gets Dealer Name
        /// </summary>
        public string DealerName { get; set; }
    }
}
