using Nop.Web.Framework.Mvc;
using System.Collections.Generic;
namespace Nop.Web.Models.Checkout
{
    public class ViewCCVaultModel
    {
        public List<CreditCard> CreditCards { get; set; }
    }

    public class CreditCard
    {
        public string CCNumber { get; set; }
        public string ExpMonth { get; set; }
        public string ExpYear { get; set; }

    }
}