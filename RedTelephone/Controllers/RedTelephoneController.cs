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
    }
}
