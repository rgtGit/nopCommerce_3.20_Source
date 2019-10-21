using System.Collections.Generic;
using System.Web.Mvc;
using FluentValidation.Attributes;
using Nop.Admin.Validators.Shipping;
using Nop.Web.Framework;
using Nop.Web.Framework.Localization;
using Nop.Web.Framework.Mvc;

namespace Nop.Admin.Models.Shipping
{
    public partial class ShippingDropModel : BaseNopEntityModel, ILocalizedModel<ShippingDropLocalizedModel>
    {
        public ShippingDropModel()
        {
            Locales = new List<ShippingDropLocalizedModel>();
        }
        [NopResourceDisplayName("Admin.Configuration.Shipping.DropShip.Fields.MinimumCharge")]
        [AllowHtml]
        public string MinimumCharge { get; set; }

        [NopResourceDisplayName("Admin.Configuration.Shipping.DropShip.Fields.PercentageOfSubTotal")]
        [AllowHtml]
        public string PercentageOfSubTotal { get; set; }
            

        public IList<ShippingDropLocalizedModel> Locales { get; set; }
    }

    public partial class ShippingDropLocalizedModel : ILocalizedModelLocal
    {
        public int LanguageId { get; set; }

        [NopResourceDisplayName("Admin.Configuration.Shipping.DropShip.Fields.MinimumCharge")]
        [AllowHtml]
        public string MinimumCharge { get; set; }

        [NopResourceDisplayName("Admin.Configuration.Shipping.DropShip.Fields.PercentageOfSubTotal")]
        [AllowHtml]
        public string PercentageOfSubTotal { get; set; }
    }
}