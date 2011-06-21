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
            ViewData["Message"] = "Welcome to ASP.NET MVC!";
            logger.Debug("yay");
            return View();
        }
    }
}
