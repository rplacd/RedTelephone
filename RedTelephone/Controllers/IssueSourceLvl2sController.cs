﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Objects;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

namespace RedTelephone.Controllers {
    [ValidateInput(false)]
    public class IssueSourceLvl2sController : RedTelephoneController {
        String className = "IssueSourceLvl2sController";

        public ActionResult Index(String operand)
        {
            return authenticatedAction(new String[] { "UR" }, () => { 
                ObjectSet<IssueSourceLvl2> table = (new ModelsDataContext()).IssueSourceLvl2s;
                IssueSourceLvl1 parent = (new ModelsDataContext()).IssueSourceLvl1s.Where(s => s.code == operand).First();
                return formAction(
                    () => {
                        logger.Debug(className + ".Index accessed.");
                        ViewData["Parent"] = parent; 
                        ViewData["Items"] = table.Where(s => s.parentCode == operand).OrderBy(i => i.sortIndex).ToList();
                        return View();
                    },
                    () => sideEffectingAction(() => {

                        logger.Debug(className + ".Index updating.");
                        //update properties of each row, with the exception of...
                        var formVars = extractRowParams(Request.Form);
                        foreach (KeyValuePair<String, Dictionary<String, String>> itemPair in formVars) {
                            var code = itemPair.Value["code"];
                            IssueSourceLvl2 possibleItem = table.Where(s => s.parentCode == operand).FirstOrDefault(p => p.code == code);

                            //VALIDATION HAPPENS HERE
                            validationLogPrefix = className + ".Index";
                            ValidateStrLen(itemPair.Value["description"], 32, "Level two issue source descriptions");
                            //AND THEN ENDS.

                            //does it exist - or do we have to add it in?
                            if (possibleItem != null) {
                                possibleItem.description = itemPair.Value["description"];
                                logger.DebugFormat(className + ".Index updating {0}", possibleItem.ToString());
                            } else {
                                possibleItem = new IssueSourceLvl2();
                                possibleItem.parentCode = operand;
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

                        updateTableTimestamp("T_CRFSRP2");
                    }));
            });
        }

        public ActionResult NewRow()
        {
            return newRowAction<String>(Str1Gen);
        }
    }
}
