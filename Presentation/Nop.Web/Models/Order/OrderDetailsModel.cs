﻿using System;
using System.Collections.Generic;
using Nop.Core.Domain.Catalog;
using Nop.Web.Framework.Mvc;
using Nop.Web.Models.Common;

namespace Nop.Web.Models.Order
{
    public partial class OrderDetailsModel : BaseNopEntityModel
    {
        public OrderDetailsModel()
        {
            TaxRates = new List<TaxRate>();
            GiftCards = new List<GiftCard>();
            Items = new List<OrderItemModel>();
            OrderNotes = new List<OrderNote>();
            Invoices = new List<InvoiceModel>();
            Shipments = new List<ShipmentBriefModel>();

            BillingAddress = new AddressModel();
            ShippingAddress = new AddressModel();
        }

        public string ImagePath { get; set; }

        public bool PrintMode { get; set; }

        public DateTime CreatedOn { get; set; }

        public string OrderStatus { get; set; }

        public bool IsReOrderAllowed { get; set; }

        public bool IsReturnRequestAllowed { get; set; }

        public bool IsShippable { get; set; }
        public string ShippingStatus { get; set; }
        
        public string ShippingMethod { get; set; }
        public string ShipVia { get; set; }
        public string DesiredShipDate { get; set; }
        public IList<ShipmentBriefModel> Shipments { get; set; }

        public AddressModel BillingAddress { get; set; }
        public AddressModel ShippingAddress { get; set; }
        
        public string VatNumber { get; set; }
        public string PONumber { get; set; }


        public string PaymentMethod { get; set; }
        public bool CanRePostProcessPayment { get; set; }
        public bool DisplayPurchaseOrderNumber { get; set; }
        public string PurchaseOrderNumber { get; set; }

        public string OrderSubtotal { get; set; }
        public string OrderSubTotalDiscount { get; set; }
        public string OrderShipping { get; set; }
        public string DropShipCharge { get; set; }
        public string PaymentMethodAdditionalFee { get; set; }
        public string CheckoutAttributeInfo { get; set; }
        public string Tax { get; set; }
        public IList<TaxRate> TaxRates { get; set; }
        public bool DisplayTax { get; set; }
        public bool DisplayTaxRates { get; set; }
        public string OrderTotalDiscount { get; set; }
        public int RedeemedRewardPoints { get; set; }
        public string RedeemedRewardPointsAmount { get; set; }
        public string OrderTotal { get; set; }
        
        public IList<GiftCard> GiftCards { get; set; }

        public bool ShowSku { get; set; }

        public virtual Product Product { get; set; }
        public IList<OrderItemModel> Items { get; set; }
        
        public IList<OrderNote> OrderNotes { get; set; }

        public IList<InvoiceModel> Invoices { get; set; }
        #region Nested Classes

        public partial class OrderItemModel : BaseNopEntityModel
        {
            public string Sku { get; set; }
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public string FullDescription { get; set; }
            public string UnitPrice { get; set; }
            public string ItemDiscountTotal { get; set; }
            public int Quantity { get; set; }
            public string SubTotal { get; set; }
            public string AttributeInfo { get; set; }

        }

        public partial class TaxRate : BaseNopModel
        {
            public string Rate { get; set; }
            public string Value { get; set; }
        }

        public partial class GiftCard : BaseNopModel
        {
            public string CouponCode { get; set; }
            public string Amount { get; set; }
        }

        public partial class OrderNote : BaseNopModel
        {
            public string Note { get; set; }
            public DateTime CreatedOn { get; set; }
        }

        public partial class ShipmentBriefModel : BaseNopEntityModel
        {
            public string TrackingNumber { get; set; }
            public DateTime? ShippedDate { get; set; }
            public DateTime? DeliveryDate { get; set; }
        }

        public partial class InvoiceModel :  BaseNopEntityModel
        {
            public string InvoiceNo { get; set; }
            public string InvoiceDate { get; set; }
            public string ShipDate { get; set; }
            public string ShipVia { get; set; }
            public string TrackingNo { get; set; }
        }
		#endregion
    }
}