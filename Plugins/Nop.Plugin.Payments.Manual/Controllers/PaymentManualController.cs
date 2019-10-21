using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Data;
using Nop.Data;
using System.Data;
using System.Data.SqlClient;
using Nop.Plugin.Payments.Manual.Models;
using Nop.Plugin.Payments.Manual.Validators;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.Manual.Controllers
{
    public class PaymentManualController : BaseNopPaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly IDataProvider _dataProvider;
        private readonly IDbContext _dbContext;

        public PaymentManualController(IWorkContext workContext,

            IStoreService storeService, 
            ISettingService settingService, 
            ILocalizationService localizationService,
            IDataProvider dataProvider, 
            IDbContext dbContext)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._localizationService = localizationService;
            this._dataProvider = dataProvider;
            this._dbContext = dbContext;

                      
        }

        private class LastCCUsed  // RGT
        {
            public string CardType { get; set; }
            public string CardCvv2  { get; set; }
        }



        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var manualPaymentSettings = _settingService.LoadSetting<ManualPaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.TransactModeId = Convert.ToInt32(manualPaymentSettings.TransactMode);
            model.AdditionalFee = manualPaymentSettings.AdditionalFee;
            model.AdditionalFeePercentage = manualPaymentSettings.AdditionalFeePercentage;
            model.TransactModeValues = manualPaymentSettings.TransactMode.ToSelectList();

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.TransactModeId_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.TransactMode, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(manualPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            }

            return View("Nop.Plugin.Payments.Manual.Views.PaymentManual.Configure", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var manualPaymentSettings = _settingService.LoadSetting<ManualPaymentSettings>(storeScope);

            //save settings
            manualPaymentSettings.TransactMode = (TransactMode)model.TransactModeId;
            manualPaymentSettings.AdditionalFee = model.AdditionalFee;
            manualPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            if (model.TransactModeId_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(manualPaymentSettings, x => x.TransactMode, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(manualPaymentSettings, x => x.TransactMode, storeScope);

            if (model.AdditionalFee_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(manualPaymentSettings, x => x.AdditionalFee, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(manualPaymentSettings, x => x.AdditionalFee, storeScope);

            if (model.AdditionalFeePercentage_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(manualPaymentSettings, x => x.AdditionalFeePercentage, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(manualPaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            //now clear settings cache
            _settingService.ClearCache();

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();
            string CardType = "None";
            string CardCV2 = "";
                       


            //  Retrieve Last Credit Card Used...
            SqlParameter pCustomerID = new SqlParameter();
                pCustomerID.ParameterName = "@CustomerID";
                pCustomerID.SqlDbType = SqlDbType.Int;
                pCustomerID.Value = _workContext.CurrentCustomer.Id;
                

                //throw new Exception(pRetail.Value.ToString());
 
                var result = _dbContext.SqlQuery<LastCCUsed>
                    ("EXEC Checkout_LastCCUsed_Select @CustomerID", pCustomerID);
                               
                               
            foreach (var item in result)
            {
                CardType = item.CardType;
                CardCV2 = item.CardCvv2;
            }

            //   throw new Exception("Last CC Used:  " + CardType);

            Boolean Visa = false;
            Boolean MC = false;
            Boolean Discover = false;
            Boolean Amex = false;
            
            //  Determine Last CC Used..
            switch(CardType)
            {
                case "Visa":
                    Visa = true;
                    break;

                case "MasterCard":
                    MC = true;
                    break;

                case "Discover":
                    Discover = true;
                    break;
                
                case "Amex":
                    Amex = true;
                    break;

                default:
                    Visa = true;
                    break;
            }
                        
            model.CreditCardTypes.Add(new SelectListItem()
                {
                    Text = "Visa",
                    Value = "Visa",
                    Selected = Visa ? true : false,
                });
            model.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "MasterCard",
                Value = "MasterCard",
                Selected = MC ? true : false,
            });
            model.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "Discover",
                Value = "Discover",
                Selected = Discover ? true : false
            });
            model.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "Amex",
                Value = "Amex",
                Selected = Amex ? true : false,
             
            });

            model.CCLastFour = CardCV2;
            

            //years
            for (int i = 0; i < 15; i++)
            {
                string year = Convert.ToString(DateTime.Now.Year + i);
                model.ExpireYears.Add(new SelectListItem()
                {
                    Text = year,
                    Value = year,
                });
            }
      
            //months
            for (int i = 1; i <= 12; i++)
            {
                string text = (i < 10) ? "0" + i.ToString() : i.ToString();
                model.ExpireMonths.Add(new SelectListItem()
                {
                    Text = text,
                    Value = i.ToString(),
                });
            }
          
            //set postback values
            var form = this.Request.Form;
         //   model.CardholderName = form["CardholderName"];
         //   model.CardNumber = form["CardNumber"];
            model.CardCode = form["CardCode"];
            var selectedCcType = model.CreditCardTypes.FirstOrDefault(x => x.Value.Equals(form["CreditCardType"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedCcType != null)
                selectedCcType.Selected = true;
            var selectedMonth = model.ExpireMonths.FirstOrDefault(x => x.Value.Equals(form["ExpireMonth"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedMonth != null)
                selectedMonth.Selected = true;
            var selectedYear = model.ExpireYears.FirstOrDefault(x => x.Value.Equals(form["ExpireYear"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedYear != null)
                selectedYear.Selected = true;

            return View("Nop.Plugin.Payments.Manual.Views.PaymentManual.PaymentInfo", model);
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel()
            {
              //  CardholderName = form["CardholderName"],
              //  CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
             //   ExpireMonth = form["ExpireMonth"],
             //   ExpireYear = form["ExpireYear"]
            };
            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    warnings.Add(error.ErrorMessage);
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            paymentInfo.CreditCardType = form["CreditCardType"];
          /* --- RGT  paymentInfo.CreditCardName = form["CardholderName"];
            paymentInfo.CreditCardNumber = form["CardNumber"];
            paymentInfo.CreditCardExpireMonth = int.Parse(form["ExpireMonth"]);
            paymentInfo.CreditCardExpireYear = int.Parse(form["ExpireYear"]); --- */
            paymentInfo.CreditCardCvv2 = form["CardCode"];
            return paymentInfo;
        }
    }
}