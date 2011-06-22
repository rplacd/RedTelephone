using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace RedTelephone.Controllers
{
    public class AuthController : RedTelephoneController
    {

        public ActionResult Login()
        {
            logger.Debug("AuthController.Login accessed");
            String formUsername = Request.Form["username"];
            String formPassword = Request.Form["password"];
            String targetHash = hashCombo(formUsername, formPassword);

            //if we're logged in, hack in a cookie in the response and redirect.
            if (hashComboExists_p(formUsername, hashCombo(formUsername, formPassword))) {
                logger.DebugFormat("AuthController.Login letting user {0} in.", formUsername);
                HttpCookie cookie = new HttpCookie("Authentication");
                cookie["username"] = formUsername;
                cookie["hashcombo"] = hashCombo(formUsername, formPassword);
                Response.SetCookie(cookie);
                return Redirect(Request.Form["destination"]);
            } else {
                //otherwise we're just going back to the same old login screen, albeit with message and manually set params.
                logger.WarnFormat("AuthController.Login failed with username {0} and password {1}",
                    Request.Form["username"], Request.Form["password"]);
                return LoginRequired(Request.Form["destination"], Request.Form["referer"], "That isn't a valid username and password combination.");
            }
        }

    }
}
