using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Objects;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

namespace RedTelephone.Controllers {
    [ValidateInput(false)]
    public class ContractsController : RedTelephoneController {
        ObjectSet<Contract> table = (new ModelsDataContext()).Contracts;
        String className = "ContractsController";

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
                            var code = Convert.ToDecimal(itemPair.Value["code"]);
                            Contract possibleItem = table.FirstOrDefault(p => p.code == code);

                            //VALIDATION HAPPENS HERE
                            validationLogPrefix = className + ".Index";
                            ValidateStrLen(itemPair.Value["description"], 32, "Level one issue source descriptions");
                            //AND THEN ENDS.

                            //does it exist - or do we have to add it in?
                            if (possibleItem != null) {
                                possibleItem.description = itemPair.Value["description"];
                                logger.DebugFormat(className + ".Index updating {0}", possibleItem.ToString());
                            } else {
                                possibleItem = new Contract();
                                possibleItem.code = code;
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
                        setSortIndexes(table, x => x.code.ToString());

                        updateTableTimestamp("T_CDFCTT");
                    }
           )));
        }

        public ActionResult NewRow()
        {
            return newRowAction<Decimal>(Decimal5Gen);
        }
    }
}
