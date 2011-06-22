using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text;
using System.Security.Cryptography;
using log4net;
using log4net.Config;
using RedTelephone.Models;

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

        //Authorization methods - provide helper functions that check whether a user is authed with the right permissions,
        //and a means to throw up a login screen.
        //Login screen will discreetly pass to the AuthController, which will then decide whether to shove a cookie in that
        //positively identifies the user, and decides whether to continue living happily or throw up the same screen with an error message.
        
        protected String hashCombo(String username, String password)
            //produces a string of length 64 that contains what ostensibly is the hash of the username and password combo used.
        {
            byte[] rawBytes = Encoding.UTF8.GetBytes(username + password);
            String rawHash = Convert.ToBase64String(new SHA512Managed().ComputeHash(rawBytes));
            if(rawHash.Length < 64) {
                return rawHash.PadRight(64);
            } else if(rawHash.Length == 64) {
                return rawHash;
            } else /*rawHash.length > 64*/ {
                return rawHash.Remove(64);
            }
        }

        protected bool hashComboExists_p(String username, String hashCombo)
        {
            //does the username exist?
            var users = (new ModelsDataContext().Users)
                                        .Where(u => u.userName == username)
                                        .Select(u => u);
            if (users.Count() < 1) {
                logger.DebugFormat("RedTelephoneController.hashComboExists_p falling out because we don't have the user {0}",
                    username);
                return false;
            }

            //does the hash-combo match?
            Func<IEnumerator<User>, User> lambda = (enm) => { enm.MoveNext(); return enm.Current; };
            User user = lambda(users.GetEnumerator());
            if (hashCombo != user.hashCombo) {
                logger.DebugFormat("RedTelephoneController.userAuthed_p falling out because user hash-combo and db hash-combo don't match for the user {0}", username);
                return false;
            }

            logger.DebugFormat("RedTelephoneController.hashComboExists_p is letting the user {0} in.", username);
            return true;
        }
        
        protected bool userAuthed_p()
        {
            logger.Debug("RedTelephoneController.userAuthed_p called.");
            HttpCookie cookie = Request.Cookies["Authentication"];
            //do we have the Authentication cookie?
            if(cookie == null) {
                logger.Debug("RedTelephoneController.userAuthed_p falling out on lack of cookie on the side of the user.");
                return false;
            }
            //does it contains the subkeys we need?
            if (cookie["username"] == null || cookie["hashcombo"] == null) {
                logger.Debug("RedTelephoneController.userAuthed_p falling out on malformed cookie.");
                return false;
            }
            //does the username and hashcombo exist + match?
            return hashComboExists_p(cookie["username"], cookie["hashcombo"]);
        }

        protected ActionResult LoginRequired()
            //use this when the path requested is where we need to go to,
            //and when the referer's set properly.
        {
            return LoginRequired(Request.RawUrl, Request.ServerVariables["http_referer"], "");
        }
        protected ActionResult LoginRequired(String destinationPath, String referer, String message)
            //just protecting my shiznit if the login state-machine changes.
        {
            logger.WarnFormat("RedTelephoneController.LoginRequired accessed with destinationPath {0}, referer {1}, message {2}",
                        destinationPath, referer, message);
            ViewData["Destination"] = destinationPath;
            ViewData["Referer"] = referer;
            ViewData["Message"] = message;
            return View("RedTelephoneLogin");
        }
    }
}
