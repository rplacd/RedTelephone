using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

//REFACTOR: for consistency, use the new table var scheme instead of a db var.
namespace RedTelephone.Controllers
{
    [ValidateInput(false)]
    public class PrioritiesController : RedTelephoneController
    {
        ModelsDataContext db = new ModelsDataContext();

        public ActionResult Index()
        {
            return authenticatedAction(new String[] { "UR" }, () => formAction(
                    () => {
                        logger.Debug("PrioritiesController.Index accessed.");
                        ViewData["Priorities"] = db.Priorities.OrderBy(p => p.sortIndex).ToList();
                        return View();
                    },
                    ()=> sideEffectingAction(() => {
                        logger.Debug("PrioritiesController.Index updating.");

                        //update properties of each row, with the exception of...
                        var formVars = extractRowParams(Request.Form);
                        foreach (KeyValuePair<String, Dictionary<String, String>> priority in formVars) {
                            var code = priority.Value["code"];
                            Priority possiblepriority = db.Priorities.FirstOrDefault(p => p.code == code);

                            //VALIDATION HAPPENS HERE
                            validationLogPrefix = "PrioritiesController.Index";
                            ValidateStrLen(priority.Value["description"], 32, "Priority descriptions");
                            //AND THEN ENDS.

                            //does it exist - or do we have to add it in?
                            if (possiblepriority != null) {
                                possiblepriority.description = priority.Value["description"];
                                logger.DebugFormat("PrioritiesController.Index updating {0}", possiblepriority.ToString());
                            } else {
                                possiblepriority = new Priority();
                                possiblepriority.code = priority.Value["code"];
                                possiblepriority.description = priority.Value["description"];
                                possiblepriority.active_p = "A";
                                db.Priorities.AddObject(possiblepriority);
                                logger.ErrorFormat("PrioritiesController.Index adding {0} with description {1}", priority.Key, priority.Value["description"]);
                            }

                            //set the "active" as well.
                            if (priority.Value.ContainsKey("active")) {
                                possiblepriority.active_p = "A";
                            } else {
                                possiblepriority.active_p = "N";
                            }

                            db.SaveChanges();
                        }

                        //orderingindex, which we do seperately.
                        setSortIndexes(db.Priorities, x => x.code);

                        updateTableTimestamp("T_CRFPRI");
                    }
           )));
        }

        public ActionResult NewRow()
        {
            return newRowAction<String>(Str1Gen);
        }
    }
}
