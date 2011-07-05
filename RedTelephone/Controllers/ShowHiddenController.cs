using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace RedTelephone.Controllers
{
    public class ShowHiddenController : RedTelephoneController
    {
        public ActionResult Yes()
        {
            return sideEffectingAction(() => {
                logger.Debug("ShowHiddenController showing hidden data rows");
                var foo = new HttpCookie("ShowHidden", "yes");
                Response.SetCookie(foo);
            });
        }

        public ActionResult No()
        {
            return sideEffectingAction(() => {
                logger.Debug("ShowHiddenController hiding hidden data rows");
                var foo = new HttpCookie("ShowHidden", "no") { Expires = DateTime.Now.AddYears(-1) };
                Response.SetCookie(foo);
            });
        }
    }
}
