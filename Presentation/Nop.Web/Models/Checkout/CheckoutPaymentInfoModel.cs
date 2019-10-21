using System.Web.Routing;
using Nop.Web.Framework.Mvc;
using System.Collections.Generic;
using System;

namespace Nop.Web.Models.Checkout
{
    public partial class CheckoutPaymentInfoModel : BaseNopModel
    {
        public string PaymentInfoActionName { get; set; }
        public string PaymentInfoControllerName { get; set; }
        //public RouteValueDictionary PaymentInfoRouteValues { get; set; }

        /// <summary>
        /// Used on one-page checkout page
        /// </summary>
        public bool DisplayOrderTotals { get; set; }

        /// <summary>
        /// Gets or Sets OrderTotal
        /// </summary>

        public string OrderTotal { get; set; }

        public List<CreditCard> CreditCards { get; set; } = new List<CreditCard>();

        public class CreditCard 
        {
            public Guid CardToken { get; set; }
            public string CardType { get; set; }
            public string CardNumber { get; set; }
            public string CardExpirationMonth { get; set; }
            public string CardExpirationYear { get; set; }

        }


    }
}