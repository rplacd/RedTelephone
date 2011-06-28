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
                                logger.DebugFormat("UsersController.Index updating {0}", possibleUser.ToString());
                            } else {
                                logger.ErrorFormat("UsersController.Index couldn't update for {0}", user.Key);
                            }
                        }

                        db.SubmitChanges();
                    })));
                
        }

        //REFACTOR: just make this "Create"
        public ActionResult NewUser()
        {
            return authenticatedAction(new String[] { "UU" },
                () => formAction(
                    () => {
                        logger.Debug("UsersController.NewUser accessed.");
                        return View(); 
                    },
                    //REFACTOR: move this out into [httpPost] land, possibly let MVC routing map fields to parameters.
                    () => {
                        ViewData["Referer"] = Request.ServerVariables["http_referer"];

                        User newUser = new User();
                        newUser.userName = Request.Form["username"];
                        newUser.hashCombo = hashCombo(Request.Form["username"], Request.Form["password"]);
                        newUser.firstName = Request.Form["firstname"];
                        newUser.lastName = Request.Form["lastname"];
                        newUser.active_p = "A";

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


                        logger.DebugFormat("UsersController.NewUser creating the new user {0}", newUser.ToString());

                        return Redirect("/users");
                    }
            ));
        }

        public ActionResult PasswordReset(String operand)
        {
            return authenticatedAction(new String[] { "UU" }, () => {
                logger.Debug("UsersController.PasswordReset accessed.");
                ViewData["Username"] = operand;
                return View();
            });
        }
        [HttpPost]
        public ActionResult PasswordReset(String operand, String password, String verifyPassword)
        {
            return authenticatedAction(new String[] { "UU" }, () => {

                //VALIDATION START
                validationLogPrefix = "UsersController.PasswordReset";
                ValidateAssertion(operand == password, "Password and verification do not match.");
                //END

                var db = new ModelsDataContext();
                var usersModel = db.Users;
                User user = usersModel.FirstOrDefault(u => u.userName == operand);

                user.hashCombo = hashCombo(user.userName, password);
                db.SubmitChanges();

                logger.DebugFormat("UsersController.PasswordReset resetting for {0} to {1}", operand, user.hashCombo);

                return Redirect("/users");
            });
        }

        public ActionResult Permissions(String operand)
        {
            var db = new ModelsDataContext();
            var permsModel = db.UserPermissionPairs;
            var allPermsModel = db.Permissions;

            return authenticatedAction(new String[] { "UU" }, ()=> formAction(() => {
                logger.DebugFormat("UsersController.Permissions accessed for {0}", operand);

                IEnumerable<String> raw_selectedPerms = permsModel
                    .Where(upp => upp.userName == operand)
                    .Select(upp => upp.permission);
                IEnumerable<String> raw_restPerms = allPermsModel
                    .Where(p => !(raw_selectedPerms.Contains(p.permission)))
                    .Select(p => p.permission);

                //(permission, description).
                List<Tuple<String, String>> selectedPerms_desc = new List<Tuple<String, String>>();
                foreach (String perm in raw_selectedPerms) {
                    selectedPerms_desc.Add(new Tuple<String, String>(
                        perm,
                        allPermsModel.FirstOrDefault(p => p.permission == perm).description
                    ));
                }

                List<Tuple<String, String>> restPerms_desc = new List<Tuple<String, String>>();
                foreach (String perm in raw_restPerms) {
                    restPerms_desc.Add(new Tuple<String, String>(
                        perm,
                        allPermsModel.FirstOrDefault(p => p.permission == perm).description
                    ));
                }

                ViewData["Username"] = operand;
                ViewData["SelectedPerms"] = selectedPerms_desc;
                ViewData["RestPerms"] = restPerms_desc;

                return View();
            },
            () => {
                //get the names for each permission.
                String raw_perm_descs = Request.Form["selectedperms"];
                List<String> perms;

                if (raw_perm_descs == null) {
                    perms = new List<String>();
                } else {
                    //split the raw_perm_descs up.
                    perms = new List<String>();
                    var encoded_perms = raw_perm_descs.Split(new Char[]{','});
                    var decoded_perms = new List<String>();

                    //urldecode them.
                    foreach (String encoded_perm in encoded_perms) {
                        decoded_perms.Add(HttpUtility.UrlDecode(encoded_perm));
                    }

                    //fetch their ID names.
                    foreach (String decoded_desc in decoded_perms) {
                        perms.Add(allPermsModel.FirstOrDefault(p => p.description == decoded_desc).permission);
                    }
                }

                //remove all the existing permissions for the user in question...
                IEnumerable<UserPermissionPair> permsForUser = permsModel.Where(upp => upp.userName == operand);
                foreach(UserPermissionPair permPair in permsForUser) {
                    permsModel.DeleteOnSubmit(permPair);
                }
                
                //...and readd the ones we need.
                permsModel.InsertAllOnSubmit(perms.Select((String p) => {
                    var foo = new UserPermissionPair();
                    foo.userName = operand;
                    foo.permission = p;
                    return foo;
                }));

                db.SubmitChanges();

                logger.DebugFormat("UsersController.Permissions updated for {0} with new perms {1}", operand, perms.ToString());
                updateTableTimestamp("T_CRFPNM");

                return Redirect("/users");
            }));
        }

        public ActionResult Disable(string operand)
        {
            logger.DebugFormat("UsersController.Disable accessed for {0}", operand);
            return disableRowAction<User>(new String[] { "UU" }, (new ModelsDataContext()).Users, u => u.userName == operand);
        }

        public ActionResult Enable(string operand)
        {
            logger.DebugFormat("UsersController.Enable accessed for {0}", operand);
            return enableRowAction<User>(new String[] { "UU" }, (new ModelsDataContext()).Users, u => u.userName == operand);
        }
    }
}
