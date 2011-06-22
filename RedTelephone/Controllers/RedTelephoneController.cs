using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using log4net;
using log4net.Config;

namespace RedTelephone.Controllers
{
    // This class adds some common functionality used by all RedTelephone controllers.
    // ...especially some context-free ViewActions that do common stuff.
    // Inheriting isn't too essential here, but this is a coverall since I'm not sure if 
    // I might have to inherit-and-override any methods. The cost is minimal anyway.
    public abstract class RedTelephoneController : Controller
    {

        //The log4net logger.
        //We do some fancy work behind the scenes to make the property as stateless as possible.
        private static Object _loggerLock = new Object();
        private static bool _configuredLoggerP = false;
        private static ILog _logger;
        protected ILog logger
        {
            get
            {
                lock (_loggerLock) {
                    if (_configuredLoggerP) {
                        return logger;
                    }
                    else {
                        XmlConfigurator.Configure();
                        _logger = LogManager.GetLogger("RedTelephone");
                        return _logger;
                    }
                }
            }
        }

        //Catch-all error out method - displays an error screen and asks to either:
        //go back to referer or go to the top-level.
        protected ActionResult Error(String errorMessage)
        {
            logger.ErrorFormat("RedTelephoneController.Error accessed with message {0}", errorMessage);
            ViewData["Message"] = errorMessage;
            ViewData["Referer"] = Request.ServerVariables["http_referer"];
            return View("RedTelephoneError");
        }

        //Authorization methods - 
    }
}
