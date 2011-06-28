using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

namespace RedTelephone.Controllers
{
    [ValidateInput(false)]
    public class PrioritiesController : RedTelephoneController
    {
        ModelsDataContext db = new ModelsDataContext();
        //
        // GET: /Priorities/

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
                        var formVars = extractRowParams(Request.Form);
                        foreach (KeyValuePair<String, Dictionary<String, String>> priority in formVars) {
                            Priority possiblepriority = db.Priorities.FirstOrDefault(p => p.code == priority.Value["code"]);

                            //VALIDATION HAPPENS HERE
                            validationLogPrefix = "PrioritiesController.Index";
                            ValidateStrLen(priority.Value["description"], 32, "Priority descriptions");
                            //AND THEN ENDS.

                            //does it exist - or do we have to add it in?
                            if (possiblepriority != null) {
                                possiblepriority.description = priority.Value["description"];
                                logger.DebugFormat("PrioritiesController.Index updating {0}", possiblepriority.ToString());
                                db.SubmitChanges();
                            } else {
                                possiblepriority = new Priority();
                                possiblepriority.code = priority.Value["code"];
                                possiblepriority.description = priority.Value["description"];
                                possiblepriority.active_p = "A";
                                db.Priorities.InsertOnSubmit(possiblepriority);
                                logger.ErrorFormat("PrioritiesController.Index adding {0} with description {1}", priority.Key, priority.Value["description"]);
                                db.SubmitChanges();
                            }
                        }

                        updateTableTimestamp("T_CRFPRI");
                    }
           )));
        }
        
        public ActionResult Create()
        {
            logger.Debug("PrioritiesController.Create accessed.");
            return authenticatedAction(new String[] { "UR" }, () => View());
        } 

        [HttpPost]
        public ActionResult Create(FormCollection collection)
        {
            return authenticatedAction(new String[] { "UR" }, () => {

                Priority newPriority = new Priority();
                newPriority.code = collection["mnemonic"];
                newPriority.description = collection["description"];
                newPriority.sortIndex = (short)(greatestSortIndex<Priority>(db.Priorities) + 1);
                newPriority.active_p = "A";

                //VALIDATION HAPPENS HERE
                validationLogPrefix = "PrioritiesController.Create";
                ValidateStrLen(newPriority.code, 1, "Priority codes");
                ValidateStrLen(newPriority.description, 32, "Descriptions");
                //AND ENDS

                db.Priorities.InsertOnSubmit(newPriority);
                db.SubmitChanges();
                updateTableTimestamp("T_CRFPRI");


                logger.DebugFormat("PrioritiesController.Create adding {0}", newPriority.ToString());

                return Redirect("/referencedata/priorities");
            });
        }
        

        public ActionResult Disable(string operand)
        {
            logger.Debug("PrioritiesController.Disable accessed");
            updateTableTimestamp("T_CRFPRI");
            return disableRowAction<Priority>(new String[] { "UR" }, (new ModelsDataContext()).Priorities, u => u.code == operand);
        }

        public ActionResult Enable(string operand)
        {
            logger.Debug("PrioritiesController.Enable accessed");
            updateTableTimestamp("T_CRFPRI");
            return enableRowAction<Priority>(new String[] { "UR" }, (new ModelsDataContext()).Priorities, u => u.code == operand);
        }


        public ActionResult IncSortIndex(string operand)
        {
            logger.Debug("PrioritiesController.IncSortIndex accessed");
            updateTableTimestamp("T_CRFPRI");
            return incSortIndexAction<Priority>(new String[] { "UR" }, db.Priorities, p => p.code == operand);
        }
        public ActionResult DecSortIndex(string operand)
        {
            logger.Debug("PrioritiesController.DecSortIndex accessed");
            updateTableTimestamp("T_CRFPRI");
            return decSortIndexAction<Priority>(new String[] { "UR" }, db.Priorities, p => p.code == operand);
        }

        public ActionResult NewRow()
        {
            ViewData["id"] = getFreshIdVal<String, Priority>(db.Priorities, Str1Gen, p => p.code);
            return View();
        }
    }
}
