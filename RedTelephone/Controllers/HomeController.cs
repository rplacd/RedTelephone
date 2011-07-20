using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace RedTelephone.Controllers
{
    [HandleError]
    public class HomeController : RedTelephoneController
    {
        public ActionResult Index()
        {
            return authenticatedAction(new String[] { "UR", "UT", "UU", "VR" }, () => {
                logger.Debug("HomeController.Index - accessed");
                return View();
            });
        }
    }
}
