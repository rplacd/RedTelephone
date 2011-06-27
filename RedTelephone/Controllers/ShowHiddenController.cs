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
                var foo = new HttpCookie("ShowHidden", "yes");
                Response.SetCookie(foo);
            });
        }

        public ActionResult No()
        {
            return sideEffectingAction(() => {
                var foo = new HttpCookie("ShowHidden", "no") { Expires = DateTime.Now.AddYears(-1) };
                Response.SetCookie(foo);
            });
        }
    }
}
