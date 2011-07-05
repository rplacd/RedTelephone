using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Objects;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;

namespace RedTelephone.Controllers {
    [ValidateInput(false)]
    public class TicketSourcesController : RedTelephoneController {
        ObjectSet<TicketSource> table = (new ModelsDataContext()).TicketSources;

        public ActionResult Index()
        {
            return authenticatedAction(new String[] { "UR" }, () => formAction(
                    () => {
                        logger.Debug("TicketSourcesController.Index accessed.");
                        ViewData["TicketSources"] = table.OrderBy(p => p.sortIndex).ToList();
                        return View();
                    },
                    () => sideEffectingAction(() => {
                        logger.Debug("TicketSourcesController.Index updating.");

                        //update properties of each row, with the exception of...
                        var formVars = extractRowParams(Request.Form);
                        foreach (KeyValuePair<String, Dictionary<String, String>> ticketSource in formVars) {
                            var code = ticketSource.Value["code"];
                            TicketSource possibleTicketSource = table.FirstOrDefault(p => p.code == code);

                            //VALIDATION HAPPENS HERE
                            validationLogPrefix = "TicketSourcesController.Index";
                            ValidateStrLen(ticketSource.Value["description"], 32, "Ticket source descriptions");
                            //AND THEN ENDS.

                            //does it exist - or do we have to add it in?
                            if (possibleTicketSource != null) {
                                possibleTicketSource.description = ticketSource.Value["description"];
                                logger.DebugFormat("TicketSourcesController.Index updating {0}", possibleTicketSource.ToString());
                            } else {
                                possibleTicketSource = new TicketSource();
                                possibleTicketSource.code = ticketSource.Value["code"];
                                possibleTicketSource.description = ticketSource.Value["description"];
                                possibleTicketSource.active_p = "A";
                                table.AddObject(possibleTicketSource);
                                logger.ErrorFormat("TicketSourcesController.Index adding {0} with description {1}", ticketSource.Key, ticketSource.Value["description"]);
                            }

                            //set the "active" as well.
                            if (ticketSource.Value.ContainsKey("active")) {
                                possibleTicketSource.active_p = "A";
                            } else {
                                possibleTicketSource.active_p = "N";
                            }

                            table.Context.SaveChanges();
                        }

                        //orderingindex, which we do seperately.
                        setSortIndexes(table, x => x.code);

                        updateTableTimestamp("T_CRFSRT");
                    }
           )));
        }

        public ActionResult NewRow()
        {
            return newRowAction<String>(Str1Gen);
        }
    }
}
