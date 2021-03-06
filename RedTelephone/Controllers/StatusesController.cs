﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Objects;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

namespace RedTelephone.Controllers {
    [ValidateInput(false)]
    public class StatusesController : RedTelephoneController {
        ObjectSet<Status> table = (new ModelsDataContext()).Statuses;
        String className = "StatusesController";

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
                            Status possibleItems = table.FirstOrDefault(p => p.code == code);

                            //VALIDATION HAPPENS HERE
                            validationLogPrefix = className + ".Index";
                            ValidateStrLen(itemPair.Value["description"], 32, "Status descriptions");
                            //AND THEN ENDS.

                            //does it exist - or do we have to add it in?
                            if (possibleItems != null) {
                                possibleItems.description = itemPair.Value["description"];
                                logger.DebugFormat(className + ".Index updating {0}", possibleItems.ToString());
                            } else {
                                possibleItems = new Status();
                                possibleItems.code = itemPair.Value["code"];
                                possibleItems.description = itemPair.Value["description"];
                                possibleItems.active_p = "A";
                                table.AddObject(possibleItems);
                                logger.ErrorFormat(className + ".Index adding {0} with description {1}", itemPair.Key, itemPair.Value["description"]);
                            }

                            //set the "active" as well.
                            if (itemPair.Value.ContainsKey("active")) {
                                possibleItems.active_p = "A";
                            } else {
                                possibleItems.active_p = "N";
                            }

                            table.Context.SaveChanges();
                        }

                        //orderingindex, which we do seperately.
                        setSortIndexes(table, x => x.code);

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
