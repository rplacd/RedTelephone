using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

namespace RedTelephone.Controllers {
    [ValidateInput(false)]
    public class StatusesController : RedTelephoneController {
        ModelsDataContext db = new ModelsDataContext();

        public ActionResult Index()
        {
            return authenticatedAction(new String[] { "UR" }, () => formAction(
                    () => {
                        logger.Debug("StatusesController.Index accessed.");
                        ViewData["Statuses"] = db.Statuses.OrderBy(p => p.sortIndex).ToList();
                        return View();
                    },
                    () => sideEffectingAction(() => {
                        logger.Debug("StatusesController.Index updating.");

                        //update properties of each row, with the exception of...
                        var formVars = extractRowParams(Request.Form);
                        foreach (KeyValuePair<String, Dictionary<String, String>> status in formVars) {
                            var code = status.Value["code"];
                            Status possibleStatus = db.Statuses.FirstOrDefault(p => p.code == code);

                            //VALIDATION HAPPENS HERE
                            validationLogPrefix = "StatusesController.Index";
                            ValidateStrLen(status.Value["description"], 32, "Priority descriptions");
                            //AND THEN ENDS.

                            //does it exist - or do we have to add it in?
                            if (possibleStatus != null) {
                                possibleStatus.description = status.Value["description"];
                                logger.DebugFormat("StatusesController.Index updating {0}", possibleStatus.ToString());
                            } else {
                                possibleStatus = new Status();
                                possibleStatus.code = status.Value["code"];
                                possibleStatus.description = status.Value["description"];
                                possibleStatus.active_p = "A";
                                db.Statuses.AddObject(possibleStatus);
                                logger.ErrorFormat("StatusesController.Index adding {0} with description {1}", status.Key, status.Value["description"]);
                            }

                            //set the "active" as well.
                            if (status.Value.ContainsKey("active")) {
                                possibleStatus.active_p = "A";
                            } else {
                                possibleStatus.active_p = "N";
                            }

                            db.SaveChanges();
                        }

                        //orderingindex, which we do seperately.
                        setSortIndexes(db.Statuses, x => x.code);

                        updateTableTimestamp("T_CRFSTS");
                    }
           )));
        }

        public ActionResult NewRow()
        {
            return newRowAction<String>(Str1Gen);
        }
    }
}
