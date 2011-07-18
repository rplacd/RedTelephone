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

            //set the cookie, redirect back.
            logger.DebugFormat("AuthController.Login setting cookie with username {0} hashcombo {1}", formUsername, hashCombo(formUsername, formPassword));
            HttpCookie cookie = new HttpCookie("Authentication");
            cookie["username"] = formUsername;
            cookie["hashcombo"] = hashCombo(formUsername, formPassword);
            Response.SetCookie(cookie);
            return Redirect(Request.Form["destination"]);
        }

        public ActionResult Logout()
        {
            Response.SetCookie(new HttpCookie("Authentication"));
            return Redirect("/");
        }
    }
}
