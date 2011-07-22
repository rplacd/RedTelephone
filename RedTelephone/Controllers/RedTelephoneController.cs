using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Data.Objects;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Text;
using System.Text.RegularExpressions;
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
    public abstract partial class RedTelephoneController : Controller
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

            //is the user enabled?
            if (users.First().active_p != "A") {
                logger.DebugFormat("RedTelephoneController.hashComboExists_p falling out because user {0} is disabled",
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
            if (perms.Count() < 1) {
                return true;
            } else {
                return (new ModelsDataContext().UserPermissionPairs)
                                    .Where(up => up.userName == username)
                                    .Where(up => perms.Contains(up.permission)).Count()
                       > 0;
            }
        }

        public bool userAuthed_p(String[] perms)
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
        // The combinator you need. Checks if user has at least one of the perms required - this is because while we expect
        // to share resources that require authentication, fewer use-cases require a combination of permissions.
        protected ActionResult authenticatedAction(String[] perms, Func<ActionResult> action)
            //provides a combinator that checks whether the user is authenticated, then runs the inner action.
        {
            if (!userAuthed_p(perms))
                return LoginRequired();
            else
                return action();
        }

        //Sends along user auth info to the main layout so we can have menu items enable and disable themselves.
        //REFACTOR: can we have authenticatedAction feed off this instead? too much duplication here.
        protected override void OnActionExecuted(ActionExecutedContext _)
        {
            //disable caching!
            HttpContext.Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            HttpContext.Response.Cache.SetValidUntilExpires(false);
            HttpContext.Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            HttpContext.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            HttpContext.Response.Cache.SetNoStore();

            //set up some main menu state
            List<String> perms = new List<String>();
            String username = null;
            bool fail = false;
            do {
                if (userAuthed_p(new String[0])) {
                    HttpCookie cookie = Request.Cookies["Authentication"];
                    if (cookie == null)
                       break;
                    //does it contains the subkeys we need?
                    if (cookie["username"] == null)
                       break;
                    var userName = cookie["username"];
                    username = userName;
                    perms = (new ModelsDataContext()).UserPermissionPairs.Where(pp => pp.userName == userName).Select(pp => pp.permission).ToList();
                }
            } while(false);
            if (fail)
                perms = new List<String>();               

            ViewData["SessionUserPermissions"] = perms;
            ViewData["SessionUserName"] = username;
            base.OnActionExecuted(_);
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
        protected String[] extractQMarkParams(String qmarkparam)
        {
            return qmarkparam.Split(new Char[] { '?' }, 2, StringSplitOptions.None);
        }
        protected Dictionary<String, Dictionary<String, String>> extractRowParams(NameValueCollection formVars)
        {
            Dictionary<String, Dictionary<String, String>> ret = new Dictionary<String, Dictionary<String, String>>();
            foreach (String key in formVars.AllKeys) {
                if (key.Contains('?')) {
                    String[] subkey_key = extractQMarkParams(key);

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
            } else {
                logger.Error("RedTelephoneController.OnException - unhandled exception!", ctx.Exception);
                ctx.ExceptionHandled = false;
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

        //get a 14-char timestamp for the local time in the en-US locale.
        //formatted in chronologically reverse order - YYYYMMDDHHMMSS.
        //why couldn't we just use a Date? Search me.
        protected string char14Timestamp()
        {
            return DateTime.Now.ToString("yyyyMMddHHmmss", new CultureInfo("en-US"));
        }

        //parse a char14timestamp into a datetime.
        public DateTime parseChar14Timestamp(String timestamp)
        {            
            var string_year = timestamp.Substring(0, 4);
            var string_month = timestamp.Substring(4, 2);
            var string_day = timestamp.Substring(6, 2);
            var string_hour = timestamp.Substring(8, 2);
            var string_minute = timestamp.Substring(10, 2);
            var string_second = timestamp.Substring(12, 2);
            try {
                var year = Convert.ToInt32(string_year);
                var month = Convert.ToInt32(string_month);
                var day = Convert.ToInt32(string_day);
                var hour = Convert.ToInt32(string_hour);
                var minute = Convert.ToInt32(string_minute);
                var second = Convert.ToInt32(string_second);
                return new DateTime(year, month, day, hour, minute, second);
            } catch (FormatException e) {
                throw new FormatException("Illegal characters that don't exist in timestamps were passed in.", e);
            } catch (OverflowException e) {
                throw new FormatException("Invalid dates - although properly formatted - were passed.", e);
            }
        }

        //now format that 14-char timestamp.
        public string presentChar14Timestamp(String timestamp)
        {
            var year = timestamp.Substring(0, 4);
            var month = timestamp.Substring(4, 2);
            var day = timestamp.Substring(6, 2);
            var hour = timestamp.Substring(8, 2);
            var minute = timestamp.Substring(10, 2);
            var second = timestamp.Substring(12, 2);
            return String.Format("{0}:{1} {2}/{3}/{4}", hour, minute, year, month, day);
        }

        //parse a DatePicker date in the form DD/MM/YY.
        public DateTime parseDatePickerDate(String str)
        {
            String[] dateMonthYear = str.Split(new char[] { '/' });
            ValidateAssertion(dateMonthYear.Count() == 3, "A date parameter was formatted incorrectly ({0}) - this is a programmer's issue.", str);
            try {
                Int32 day = Convert.ToInt32(dateMonthYear[0]);
                Int32 month = Convert.ToInt32(dateMonthYear[1]);
                Int32 year = Convert.ToInt32(dateMonthYear[2]);
                return new DateTime(year, month, day);
            } catch (FormatException e) {
                ValidateAssertion(false, "A date parameter was formatted incorrectly ({0}) - this is a programmer's issue.", str);
                return new DateTime(1, 1, 1); //this will never be reached.
            }
        }

        //refresh the timestamp for a particular table.
        protected void updateTableTimestamp(String tableName)
        {
            var ctx = new ModelsDataContext();
            ReferenceTable table = ctx.ReferenceTables.FirstOrDefault(rf => rf.name == tableName);
            if (table != null) {
                String newTimeStamp = char14Timestamp();
                table.lastUpdate = newTimeStamp;
                ctx.SaveChanges();
                logger.DebugFormat("RedTelephoneController.updateTableTimestamp - updating timestamp for {0} to {1}", tableName, newTimeStamp);
            } else {
                logger.ErrorFormat("RedTelephoneController.updateTableTimestamp - couldn't find the table {0} to update for", tableName);
            }

        }

        //decode the #ordering parameter.
        protected Dictionary<String, short> extractOrderingParam(String encoded)
        {
            if (encoded.Length < 1) {
                return new Dictionary<String, short>();
            }

            String[] split = encoded.Split(new String[] { "table[]=", "&" }, StringSplitOptions.RemoveEmptyEntries);
            var ret = new Dictionary<String, short>();
            var counter = 0;
            foreach (String subparam in split) {
                ret.Add(HttpUtility.UrlDecode(subparam), (short)counter);
                counter++;
            }

            return ret;
        }
        protected String[] extractDnDSerializedParam(String encoded)
        {
            String[] split = encoded.Split(new String[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            return split.Select(s => HttpUtility.UrlDecode(s)).ToArray();
        }


        //Generating rows loaded in dynamically via AJAX, and their IDs. This is used in the CRUD pages.
        //generating fresh IDs.
        private static String[] str1strs = new String[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
        protected static Func<String[], String> Str1Gen = (e) => {
            var intersect = str1strs.Except(e);
            if (intersect.Count() == 0) {
                return default(string);
            } else {
                return intersect.First();
            }
        };
        //adapted from http://stackoverflow.com/questions/1122483/c-random-string-generator.
        private static Random random = new Random();
        private static string randomString(int length)
        {
            StringBuilder builder = new StringBuilder();
            char ch;
            for (int i = 0; i < length; i++) {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));                 
                builder.Append(ch);
            }
            return builder.ToString();
        }
        //REFACTOR: if you're going to write one more of these, just make a generic combinator that factors out
        //the existing-objects limit and the generator...
        protected static Func<String[], String> Str8Gen = (e) => {
            //bit of a hack, but surely we can't tell whether e contains every single type of string in the universe
            if (e.Count() > 99999) {
                return default(String);
            } else {
                var temp = randomString(8);
                while (e.FirstOrDefault(s => s == temp) != default(String)) {
                    temp = randomString(8);
                }
                return temp;
            }
        };
        protected static Func<String[], short> Short3Gen = (e) => {
            if (e.Count() > 999) {
                return default(short);
            } else {
                var temp = (short)random.Next(1, 999);
                while (e.FirstOrDefault(s => s == temp.ToString()) != default(String)) {
                    temp = (short)random.Next(1, 999);
                }
                return temp;
            }
        };
        protected static Func<String[], Decimal> Decimal5Gen = (e) => {
            if (e.Count() > 99999) {
                return default(Decimal);
            } else {
                var temp = Convert.ToDecimal(random.Next(1, 99999));
                while (e.FirstOrDefault(s => s == temp.ToString()) != default(String)) {
                    temp = Convert.ToDecimal(random.Next(1, 99999));
                }
                return temp;
            }
        };
        protected T getFreshIdVal<T>(Func<String[], T> generator, String[] existing)
        {
            return generator(existing);
        }

        protected ActionResult newRowAction<T>(Func<String[], T> gen)
        {
            T frob;
            if(Request.QueryString["table[]"] != null)
                frob = getFreshIdVal<T>(gen, extractDnDSerializedParam(Request.QueryString["table[]"]));
            else
                frob = getFreshIdVal<T>(gen, extractDnDSerializedParam(""));
            if (EqualityComparer<T>.Default.Equals(frob, default(T))) {
                logger.Warn("RedTelephoneController.NewRow hasn't been able to find a fresh ID!");
                return View("RedTelephoneNewRowError");
            } else {
                logger.DebugFormat("RedTelephoneController.NewRow generating a new row with ID {0}", frob.ToString());
                ViewData["id"] = frob;
                return View();
            }
        }
    }


}
