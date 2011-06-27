﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
    // ...especially some context-free ViewActions that do common stuff, and some Action combinators.

    // Inheriting isn't too essential here, but this is a coverall since I'm not sure if 
    // I might have to inherit-and-override any methods. The cost is minimal anyway.

    // But whatever you do - only make the combinator public if possible.
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

        private bool userHasPerms_p(String username, String[] perms)
        {
            return (new ModelsDataContext().UserPermissionPairs)
                                .Where(up => up.userName == username)
                                .Where(up => perms.Contains(up.permission)).Count()
                   == perms.Count();
        }

        private bool userAuthed_p(String[] perms)
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
            return hashComboExists_p(cookie["username"], cookie["hashcombo"]) && userHasPerms_p(cookie["username"], perms);
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
        // The combinator you need.
        protected ActionResult authenticatedAction(String[] perms, Func<ActionResult> action)
            //provides a combinator that checks whether the user is authenticated, then runs the innter action.
        {
            if (!userAuthed_p(perms))
                return LoginRequired();
            else
                return action();
        }

        //A combinator for controllers that only need to side-effect - SQL updates, cookie jiggery - and then
        //goes straight back to referer.
        protected ActionResult sideEffectingAction(Action action)
        {
            action();
            if (Request.ServerVariables["http_referer"] == null)
                return Redirect("/");
            else
                return Redirect(Request.ServerVariables["http_referer"]);
        }

        //A combinator that dispatches for GET and POST actions - this is when we don't want form variables
        //to be passed in as parameters (which will happen, especially when displaying collections.)
        protected ActionResult formAction(Func<ActionResult> getAction, Func<ActionResult> postAction)
        {
            if (Request.HttpMethod == "GET") {
                logger.Debug("RedTelephoneController.formAction dispatching GET request");
                return getAction();
            } else if (Request.HttpMethod == "POST") {
                logger.Debug("RedTelephoneController.formAction dispatching POST request");
                return postAction();
            } else {
                logger.ErrorFormat("RedTelephoneController.formAction doesn't know how to dispatch HTTP method {0}", Request.HttpMethod);
                return Error("The programming doesn't know how to deal with the HTTP request you've just made.");
            }
        }

        //Takes raw form variables in the format subkey?key (? delimits), and returns the map (key -> (subkey -> x))
        //This allows us to display collections together.
        //Why subkey_key? Because subkeys are determined by the programmer and won't have extra delimiters - WON'T THEY?
        protected Dictionary<String, Dictionary<String, String>> extractRowParams(NameValueCollection formVars)
        {
            Dictionary<String, Dictionary<String, String>> ret = new Dictionary<String, Dictionary<String, String>>();
            foreach (String key in formVars.AllKeys) {
                if (key.Contains('?')) {
                    String[] subkey_key = key.Split(new Char[] { '?' }, 2, StringSplitOptions.None);

                    if (!ret.ContainsKey(subkey_key[1])) {
                        ret[subkey_key[1]] = new Dictionary<String, String>();
                    }
                    ret[subkey_key[1]].Add(subkey_key[0], formVars[key]);
                }
            }
            return ret;
        }

        //Quick thing that acts like an assert for validating - on failure, logs an error, tosses an exception up...
        //To the accompanying error filter, who'll intervene and dump the error.
        //Set the logPrefix, call Validate or one of its special cases.
        protected override void OnException(ExceptionContext ctx)
        //perhaps we can catch LINQ<->SQL errors here as well...
        {
            if (ctx.Exception.GetType().Name == "ValidateFailException") {
                ctx.ExceptionHandled = true;
                ctx.Result = Error(ctx.Exception.Message);
            }
            return;
        }
        private class ValidateFailException : Exception {
            public ValidateFailException(String msg):
                base(msg)
            {}
        }
        protected String validationLogPrefix = "derp"; //because we're usually validating multiple vars simultaneously.
        protected void Validate(Object subject, Func<Object, bool> pred, String msg)
            //the message has a single assumed format argument.
        {
            if (!pred(subject)) {
                String formatted = String.Format(msg, subject);
                logger.Warn(validationLogPrefix + " - " + formatted);
                throw new ValidateFailException("You've misentered something into a form." + " " + formatted);
            }
        }
        //Some quick utility specialized functions with formulaic log sentences.
        protected void ValidateStrLen(String subject, int maxlen, String stringNamePlural)
        {
            Validate(subject, s => s.ToString().Length <= maxlen,
                stringNamePlural + " can't be larger than " + maxlen.ToString() + " chars - {0}");
        }
        protected void ValidateAssertion(bool asn, String msg, params Object[] args)
        {
            Validate(asn, x => (bool)x, String.Format(msg, args));
        }

        //some shared behavior for CRUD controllers - check whether we're showing hidden items, for ex.
        //also some "dynamic" accessors on models - inc/dec sorting idxes, show/hide (and is shown/hidden), etc.
        //and that "ting".
        protected bool showHidden_p()
            //show hidden rows?
        {
            return Request.Cookies["ShowHidden"] != null && Request.Cookies["ShowHidden"].Value == "yes";
        }
        protected void setRowActive<Model>(System.Data.Linq.Table<Model> table, Func<Model, bool> pred, String active_p) where Model : class
        {
            Model target = table.FirstOrDefault(pred);
                if (target != null) {
                    dynamic dyn_target = target;
                    dyn_target.active_p = active_p;
                    table.Context.SubmitChanges();
                }
        }
        protected ActionResult disable_row<Model>(String[] perms, System.Data.Linq.Table<Model> table, Func<Model, bool> pred) where Model : class
        {
            return authenticatedAction(perms, () => sideEffectingAction(() => {
                setRowActive<Model>(table, pred, "N");
            }));
        }
        protected ActionResult enable_row<Model>(String[] perms, System.Data.Linq.Table<Model> table, Func<Model, bool> pred) where Model : class
        {
            return authenticatedAction(perms, () => sideEffectingAction(() => {
                setRowActive<Model>(table, pred, "A");
            }));
        }
    }
}
