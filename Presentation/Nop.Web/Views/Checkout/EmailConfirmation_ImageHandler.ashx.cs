using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Nop.Web.Views.Checkout
{
    /// <summary>
    /// Summary description for EmailConfirmation_ImageHandler
    /// </summary>
    public class EmailConfirmation_ImageHandler : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            HttpResponse r = context.Response;

            r.ContentType = "image/jpg";
            //
            // Write the requested image
            //
            string partImage = context.Request.QueryString["part"];
            r.WriteFile(partImage);
            
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}