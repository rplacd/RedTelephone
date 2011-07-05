using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Objects;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

namespace RedTelephone.Controllers {
    [ValidateInput(false)]
    public class ActualResponsesController : RedTelephoneController {
        ObjectSet<ActualResponse> table = (new ModelsDataContext()).ActualResponses;
        String className = "ActualResponsesController";

        public ActionResult Index()
        {
            return authenticatedAction(new String[] { "UR" }, () => formAction(
                    () => {
                        logger.Debug(className + ".Index accessed.");
                        ViewData["Items"] = table.OrderBy(p => p.sortIndex).ToList();
                        return View();
                    },
                    () => sideEffectingAction(() => {
                        logger.Debug(className + ".Index updating.");

                        //update properties of each row, with the exception of...
                        var formVars = extractRowParams(Request.Form);
                        foreach (KeyValuePair<String, Dictionary<String, String>> itemPair in formVars) {
                            var code = itemPair.Value["code"];
                            ActualResponse possibleItem = table.FirstOrDefault(p => p.code == code);

                            //VALIDATION HAPPENS HERE
                            validationLogPrefix = className + ".Index";
                            ValidateStrLen(itemPair.Value["description"], 32, "Actual response descriptions");
                            //AND THEN ENDS.

                            //does it exist - or do we have to add it in?
                            if (possibleItem != null) {
                                possibleItem.description = itemPair.Value["description"];
                                logger.DebugFormat(className + ".Index updating {0}", possibleItem.ToString());
                            } else {
                                possibleItem = new ActualResponse();
                                possibleItem.code = itemPair.Value["code"];
                                possibleItem.description = itemPair.Value["description"];
                                possibleItem.active_p = "A";
                                table.AddObject(possibleItem);
                                logger.ErrorFormat(className + ".Index adding {0} with description {1}", itemPair.Key, itemPair.Value["description"]);
                            }

                            //set the "active" as well.
                            if (itemPair.Value.ContainsKey("active")) {
                                possibleItem.active_p = "A";
                            } else {
                                possibleItem.active_p = "N";
                            }

                            table.Context.SaveChanges();
                        }

                        //orderingindex, which we do seperately.
                        setSortIndexes(table, x => x.code);

                        updateTableTimestamp("T_CRFRAM");
                    }
           )));
        }

        public ActionResult NewRow()
        {
            return newRowAction<String>(Str1Gen);
        }
    }
}
