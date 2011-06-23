using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

namespace RedTelephone.Controllers
{
    [ValidateInput(false)]
    public class UsersController : RedTelephoneController
    {
        //
        // GET: /Users/

        public ActionResult Index()
        {
            var db = new ModelsDataContext();
            var usersModel = db.Users;

            return authenticatedAction(new String[] { "UU" },
                () => formAction(
                    () => {
                        logger.Debug("UsersController.Index accessed.");
                        ViewData["Users"] = usersModel.ToList();
                        return View();
                    },
                    () => sideEffectingAction(() => {
                        logger.Debug("UsersController.Index updating.");
                        var formVars = extractRowParams(Request.Form);
                        foreach (KeyValuePair<String, Dictionary<String, String>> user in formVars) {
                            User possibleUser = usersModel.FirstOrDefault(u => u.userName == user.Key);
                            if (possibleUser != null) {
                                //VALIDATION HAPPENS HERE
                                validationLogPrefix = "UsersController.Index";
                                ValidateStrLen(user.Value["firstname"], 64, "First names");
                                ValidateStrLen(user.Value["lastname"], 64, "Last names");
                                //AND THEN ENDS.

                                possibleUser.firstName = user.Value["firstname"];
                                possibleUser.lastName = user.Value["lastname"];
                                db.SubmitChanges();
                            }
                        }
                    })));
                
        }

        public ActionResult NewUser()
        {
            return authenticatedAction(new String[] { "UU" },
                () => formAction(
                    () => { 
                        logger.Debug("ActionResult.NewUser accessed.");
                        return View(); 
                    },
                    //REFACTOR: move this out into [httpPost] land, possibly let MVC routing map fields to parameters.
                    () => {
                        logger.Debug("ActionResult.NewUser updated.");
                        ViewData["Referer"] = Request.ServerVariables["http_referer"];

                        User newUser = new User();
                        newUser.userName = Request.Form["username"];
                        newUser.hashCombo = hashCombo(Request.Form["username"], Request.Form["password"]);
                        newUser.firstName = Request.Form["firstname"];
                        newUser.lastName = Request.Form["lastname"];

                        //VALIDATION HAPPENS HERE
                        validationLogPrefix = "UsersController.NewUser";
                        ValidateStrLen(newUser.userName, 8, "Usernames");
                        ValidateStrLen(newUser.firstName, 64, "First names");
                        ValidateStrLen(newUser.lastName, 64, "Last names");
                        //AND ENDS

                        var db = new ModelsDataContext();
                        var usersModel = db.Users;
                        usersModel.InsertOnSubmit(newUser);
                        db.SubmitChanges();

                        return Redirect("/users");
                    }
            ));
        }

        public ActionResult PasswordReset(String operand)
        {
            logger.Debug("UsersController.PasswordReset accessed.");
            ViewData["Username"] = operand;
            return View();
        }
        [HttpPost]
        public ActionResult PasswordReset(String operand, String password, String verifyPassword)
        {
            logger.Debug("UsersController.PasswordReset updated.");

            //VALIDATION START
            validationLogPrefix = "UsersController.PasswordReset";
            ValidateAssertion(operand == password, "Password and verification do not match.");
            //END

            var db = new ModelsDataContext();
            var usersModel = db.Users;
            User user = usersModel.FirstOrDefault(u => u.userName == operand);

            user.hashCombo = hashCombo(user.userName, password);
            db.SubmitChanges();

            return Redirect("/users");
        }
    }
}
