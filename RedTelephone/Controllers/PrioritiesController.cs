using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

namespace RedTelephone.Controllers
{
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
                        if (showHidden_p())
                            ViewData["Priorities"] = db.Priorities.OrderBy(p => p.sortIndex).ToList();
                        else
                            ViewData["Priorities"] = db.Priorities.Where(p => p.active_p == "A").OrderBy(p => p.sortIndex).ToList() ;
                        return View();
                    },
                    ()=> sideEffectingAction(() => {
                        logger.Debug("PrioritiesController.Index updating.");
                        var formVars = extractRowParams(Request.Form);
                        foreach (KeyValuePair<String, Dictionary<String, String>> priority in formVars) {
                            Priority possiblepriority = db.Priorities.FirstOrDefault(p => p.code == priority.Value["code"]);
                            if (possiblepriority != null) {
                                //VALIDATION HAPPENS HERE
                                validationLogPrefix = "PrioritiesController.Index";
                                ValidateStrLen(priority.Value["description"], 32, "Priority descriptions");
                                //AND THEN ENDS.

                                possiblepriority.description = priority.Value["description"];
                                db.SubmitChanges();
                            }
                        }
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
                logger.Debug("PrioritiesController.Create updated.");

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

                return Redirect("/referencedata/priorities");
            });
        }
        

        public ActionResult Disable(string operand)
        {
            logger.Debug("PrioritiesController.Disable accessed");
            return disableRowAction<Priority>(new String[] { "UR" }, (new ModelsDataContext()).Priorities, u => u.code == operand);
        }

        public ActionResult Enable(string operand)
        {
            logger.Debug("PrioritiesController.Enable accessed");
            return enableRowAction<Priority>(new String[] { "UR" }, (new ModelsDataContext()).Priorities, u => u.code == operand);
        }


        public ActionResult IncSortIndex(string operand)
        {
            logger.Debug("PrioritiesController.IncSortIndex accessed");
            return incSortIndexAction<Priority>(new String[] { "UR" }, db.Priorities, p => p.code == operand);
        }
        public ActionResult DecSortIndex(string operand)
        {
            logger.Debug("PrioritiesControllert.DecSortIndex accessed");
            return decSortIndexAction<Priority>(new String[] { "UR" }, db.Priorities, p => p.code == operand);
        }
    }
}
