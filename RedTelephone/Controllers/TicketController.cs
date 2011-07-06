using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RedTelephone.Models;


//extensions to Ticket to use in Index.
namespace RedTelephone.Models 
{
    public partial class Ticket
    {
        ModelsDataContext db = new ModelsDataContext();
        public string formatSource()
        {
            StringBuilder str = new StringBuilder();

            str.Append(db.Contracts.FirstOrDefault(c => c.code == contractCode).description);
            str.Append(" > ");
            str.Append(db.Companies.FirstOrDefault(c => c.code == companyCode).description);
            str.Append(" > ");
            str.Append(db.Offices.Where(o => o.code == officeCode).OrderByDescending(o => o.version).First().description);
            str.Append(" > ");
            var firstname = db.Employees.Where(e => e.code == employeeCode).OrderByDescending(e => e.version).First().firstName;
            var lastname = db.Employees.Where(e => e.code == employeeCode).OrderByDescending(e => e.version).First().lastName;
            str.Append(firstname + " " + lastname);

            return str.ToString();
        }

        public string formatCause()
        {
            StringBuilder str = new StringBuilder();

            str.Append(db.Causes.FirstOrDefault(c => c.code == causeCode).description);
            if (issueSourceLvl1Code != "") {
                str.Append(" > ");
                str.Append(db.IssueSourceLvl1s.FirstOrDefault(s => s.code == issueSourceLvl1Code).description);
                if (issueSourceLvl2Code != "") {
                    str.Append(" > ");
                    str.Append(db.IssueSourceLvl2s.FirstOrDefault(s => s.code == issueSourceLvl2Code).description);
                    if (issueSourceLvl3Code != "") {
                        str.Append(" > ");
                        str.Append(db.IssueSourceLvl3s.FirstOrDefault(s => s.code == _issueSourceLvl3Code).description);
                    }
                }
            }

            return str.ToString();
        }
    }
}

namespace RedTelephone.Controllers
{
    public class TicketController : RedTelephoneController
    {
        ModelsDataContext db = new ModelsDataContext();

        //constants used when creating new Tickets that shout - "instantiate me when I'm saving!"
        public readonly int INT_INSTANTIATE_ME = -1;
        public readonly string STR_INSTANTIATE_ME = "";

        //constants used when *not* instantiated - things like source which have alternative fields, or users, dates and times...
        //we use this everywhere! in the DB, as the null value in webforms...
        //this is because primary keys can't be blank.
        //use this when it's alright for something to not be filled in just-yet.
        //thankfully this won't be in the CRUD pages.
        public readonly int INT_NOT_INSTANTIATED = -1;
        public readonly string STR_NOT_INSTANTIATED = "";
        
        private ActionResult checkAndAddList(IEnumerable<dynamic> list, String pluralForm, String keyForm) {
            if (list.Count() < 1) {
                logger.Warn("TicketController.seedViewData - no active " + pluralForm + " exist in the database!");
                return Error("No active " + pluralForm + " exist in the database!");
            } else {
                ViewData[keyForm] = list;
                return default(ActionResult);
            }
        }

        public ActionResult Index()
        {
            return authenticatedAction(new String[] { "UT" }, () => {
                var username = Request.Cookies["Authentication"]["username"];
                var allActiveTickets = db.Tickets.Where(t => t.respondingTime.Equals("              ") || t.respondingTime.Equals(STR_NOT_INSTANTIATED));
                //not as straightforward as you might think - Created is allActiveTickets.filter for username - (Assigned U Responding)
                //so things don't pop up in Created and elsewhere.
                var responding = allActiveTickets.Where(t => t.respondingUserName == username);
                var assigned = allActiveTickets.Where(t => t.assignedUserName == username).Except(responding);
                ViewData["Created"] = allActiveTickets.Where(t => t.enteringUserName == username).Except(assigned).Except(responding);
                ViewData["Assigned"] = assigned;
                ViewData["Responding"] = responding;
                return View();
            });
        }


        private ActionResult seedViewData()
        //fill out the seed data values in the ViewData.
        //I chose Error to explicitly return an ActionValue - I'm paying the price here.
        {
            ActionResult ret = null;
            //shadow the above so we have only one exit point.
            Action<IEnumerable<dynamic>, String, String> checkAndAddList = (IEnumerable<dynamic> list, String pluralForm, String keyForm) => {
                if(list.Count() < 1) {
                    logger.Warn("TicketController.seedViewData - no active " + pluralForm + " exist in the database!");
                    ret = Error("No active " + pluralForm + " exist in the database!");
                } else {
                    ViewData[keyForm] = list;
                }
            };
            
            //"parent" seed data for drop-downs that don't change like users, contracts, level 1 ticket source...
            //error out here - no need to display a form if there's an issue.

            //get the login state.
            ViewData["CurrentUser"] = Request.Cookies["Authentication"]["username"];
            
            //get users with the UT perm.
            var eligibleUserNames = db.UserPermissionPairs.Where(upp => upp.permission == "UT");
            var eligibleUsers = db.Users.Where(u=>u.active_p=="A").Where(u => (eligibleUserNames.FirstOrDefault(upp => upp.userName == u.userName) != null)).ToList();
            checkAndAddList(eligibleUsers, "users that can update tickets", "Users");

            //get priorities and statuses.
            var priorities = db.Priorities.OrderBy(p => p.sortIndex).Where(p => p.active_p == "A").ToList();
            checkAndAddList(priorities, "priorities", "Priorities");
            var statuses = db.Statuses.OrderBy(s => s.sortIndex).Where(s => s.active_p == "A").ToList();
            checkAndAddList(statuses, "statuses", "Statuses");

            //get methods of response.
            var requestedResponses = db.RequestedResponses.OrderBy(p => p.sortIndex).Where(r => r.active_p == "A").ToList();
            checkAndAddList(requestedResponses, "methods of requested response", "RequestedResponses");
            var actualResponses = db.ActualResponses.OrderBy(p => p.sortIndex).Where(r => r.active_p == "A").ToList();
            checkAndAddList(actualResponses, "methods of actual response", "ActualResponses");

            //get causes and ticketSources.
            var causes = db.Causes.OrderBy(p => p.sortIndex).Where(p => p.active_p == "A").ToList();
            checkAndAddList(causes, "causes", "Causes");
            var ticketSources = db.TicketSources.OrderBy(s => s.sortIndex).Where(p => p.active_p == "A").ToList();
            checkAndAddList(ticketSources, "ticket sources", "TicketSources");

            //get first-level, initial second-level and third-level sources come later...
            var issueSourceLvl1s = db.IssueSourceLvl1s.OrderBy(s => s.sortIndex).Where(s => s.active_p == "A").ToList();
            checkAndAddList(issueSourceLvl1s, "first-level issue sources", "IssueSourceLvl1s");

            //contracts. just the contracts - everything else is in the JSON.
            var contracts = db.Contracts.OrderBy(c => c.sortIndex).Where(c => c.active_p == "A").ToList();
            checkAndAddList(contracts, "contracts", "Contracts");

            return ret;
        }

        //The actions New and Edit merely fill out a Ticket structure and show the same view.
        public ActionResult New()
        //fill a ticket out with default values and show it.
        {
            return authenticatedAction(new String[] { "UT" }, () => {
                //fill out parent seed values
                var seedingError_p = seedViewData();
                if (seedingError_p != null)
                    return seedingError_p;

                //two possible behaviors are seen here:
                //- either we select a sensible default for a field
                //- or we go blank with X_NOT_INSTANTIATED.

                //now do the actual ticket.
                //try to find sensible defaults - this is specific to new, since we're assuming existing Tickets
                //are consistent - for each of the *children*.
                var ticket = new Ticket();
                ticket.code = STR_INSTANTIATE_ME;
                ticket.version = 0;
                ticket.enteringUserName = Request.Cookies["Authentication"]["username"]; //the behavior here
                ticket.enteringTime = char14Timestamp();                                 //is only specific
                ticket.updatingUserName = Request.Cookies["Authentication"]["username"];     //to creation.
                ticket.updatingTime = char14Timestamp();                                 //existing tickets will have the "last" values.
                ticket.assignedUserName = STR_NOT_INSTANTIATED;
                ticket.solvedTime = STR_NOT_INSTANTIATED;
                ticket.respondingUserName = STR_NOT_INSTANTIATED;
                ticket.respondingTime = STR_NOT_INSTANTIATED;

                //the real exceptions are the items that use descriptions - but that's because we don't want to
                //be doing SQL queries in the view. 
                //viewdata lists are guaranteed to have members at this point.
                Priority priority = ((IEnumerable<Priority>)ViewData["Priorities"]).First();
                ticket.priorityCode = priority.code;
                Status status = ((IEnumerable<Status>)ViewData["Statuses"]).First();
                ticket.statusCode = status.description;

                //a user will give a requested response on the actual call - the actual response, however, will wait.
                RequestedResponse response = ((IEnumerable<RequestedResponse>)ViewData["RequestedResponses"]).First();
                ticket.requestedResponseCode = response.code;
                ticket.actualResponseCode = STR_NOT_INSTANTIATED;
                Cause cause = ((IEnumerable<Cause>)ViewData["Causes"]).First();
                ticket.causeCode = cause.code;

                ticket.ticketSourceCode = ((IEnumerable<TicketSource>)ViewData["TicketSources"]).First().code;
                ticket.ticketSourceAlt = STR_NOT_INSTANTIATED;

                ticket.issueSourceLvl1Code = STR_NOT_INSTANTIATED;
                ticket.issueSourceLvl2Code = STR_NOT_INSTANTIATED;
                ViewData["InitIssueSourceLvl2s"] = new List<IssueSourceLvl2>();
                ticket.issueSourceLvl3Code = STR_NOT_INSTANTIATED;
                ViewData["InitIssueSourceLvl3s"] = new List<IssueSourceLvl3>();
                ticket.issueSourceAlt = STR_NOT_INSTANTIATED;

                //select the first contract and compan that comes to mind - everything else is not instantiated
                //(although the page load shouldn't fail even then - we'll have a javascript doodad that *does* fail verification)
                Contract contract = ((IEnumerable<Contract>)ViewData["Contracts"]).First();
                ticket.contractCode = contract.code;
                IEnumerable<Company> companies = (IEnumerable<Company>)db.Companies.Where(c => c.active_p == "A").Where(c => c.active_p == "A").OrderBy(c => c.sortIndex).ToList();
                ActionResult act = checkAndAddList(companies, "companies", "InitCompanies");
                if (act != null) return act;
                ticket.companyCode = companies.First().code;
                ticket.officeCode = STR_NOT_INSTANTIATED;
                ticket.officeVersion = INT_NOT_INSTANTIATED;
                ViewData["InitOffices"] = new List<Office>();
                ticket.employeeCode = STR_NOT_INSTANTIATED;
                ViewData["InitEmployees"] = new List<Employee>();
                ticket.employeeVersion = INT_NOT_INSTANTIATED;

                ViewData["Notes"] = new List<TicketNote>();

                ViewData["Ticket"] = ticket;
                return View("Ticket");
            });
        }

        public ActionResult Edit(string operand)
        {
            return authenticatedAction(new String[] { "UT" }, () => {
                Ticket ticket = db.Tickets.FirstOrDefault(t => t.code == operand);
                if (ticket == null) {
                    logger.WarnFormat("Can't find a ticket with the the code {0} to edit.", operand);
                    return Error(String.Format("Can't find a ticket with the the code {0} to edit.", operand));
                }

                var seedingError_p = seedViewData();
                if (seedingError_p != null)
                    return seedingError_p;

                var initIssueSourceLvl2s = db.IssueSourceLvl2s.Where(i => i.parentCode == ticket.issueSourceLvl1Code).Where(o => o.active_p == "A").OrderBy(o => o.sortIndex).ToList();
                ViewData["InitIssueSourceLvl2s"] = initIssueSourceLvl2s;
                var initIssueSourceLvl3s = db.IssueSourceLvl3s.Where(i => i.parentCode == ticket.issueSourceLvl2Code && i.grandparentCode == ticket.issueSourceLvl1Code).Where(o => o.active_p == "A").OrderBy(o => o.sortIndex).ToList();
                ViewData["InitIssueSourceLvl3s"] = initIssueSourceLvl3s;

                var companies = db.Companies.Where(c => c.contractCode == ticket.contractCode);
                checkAndAddList(companies, "companies matching the contract of the ticket", "InitCompanies");
                ViewData["InitOffices"] = db.newestOffices().Where(o => o.contractCode == ticket.contractCode && o.companyCode == ticket.companyCode).Where(o => o.active_p == "A").OrderBy(o => o.sortIndex).ToList();
                ViewData["InitEmployees"] = db.newestEmployees().Where(e => e.contractCode == ticket.contractCode && e.companyCode == ticket.companyCode).Where(o => o.active_p == "A").OrderBy(o => o.sortIndex).ToList();

                ViewData["Notes"] = db.TicketNotes.Where(n => n.ticketCode == ticket.code).OrderBy(n => n.sortIndex).ToList();

                ViewData["Ticket"] = ticket;
                return View("Ticket");
            });
        }

        [HttpPost]
        public ActionResult Index(FormCollection collection)
        //Commits ticket to disk - reads some data from form values, and autogenerates some of its own like updatingTime and respondingTime.
        {
            return authenticatedAction(new String[] { "UT" }, () => {
                //first create the ticket...
                bool newTicket_p = collection["code"] == STR_INSTANTIATE_ME;
                Ticket target = null;
                if (newTicket_p) {
                    target = new Ticket();
                    //STUB
                    target.code = getFreshIdVal<String>(Str8Gen, db.Tickets.Select(t => t.code).ToArray());
                } else {
                    var code = collection["code"];
                    target = db.Tickets.First(t => t.code == code);
                }
                target.version++;

                target.contractCode = Convert.ToInt32(collection["contract"]);
                target.companyCode = Convert.ToInt32(collection["company"]);
                var eCode_version = extractQMarkParams(collection["employee"]);
                target.employeeCode = eCode_version[0];
                target.employeeVersion = Convert.ToInt32(eCode_version[1]);
                var oCode_version = extractQMarkParams(collection["office"]);
                target.officeCode = oCode_version[0];
                target.officeVersion = Convert.ToInt32(oCode_version[1]);

                target.statusCode = collection["status"];
                target.priorityCode = collection["priority"];

                target.ticketSourceCode = collection["ticketSource"];
                target.ticketSourceAlt = collection["ticketSourceAlt"];
                target.requestedResponseCode = collection["requestedResponse"];
                target.actualResponseCode = collection["actualResponse"];
                target.causeCode = collection["cause"];
                target.issueSourceLvl1Code = collection["issueSourceLvl1"];
                target.issueSourceLvl2Code = collection["issueSourceLvl2"];
                target.issueSourceLvl3Code = collection["issueSourceLvl3"];
                target.issueSourceAlt = collection["issueSourceAlt"];

                target.enteringUserName = collection["enteringUser"];
                target.enteringTime = collection["enteringTime"];
                target.updatingUserName = collection["updatingUser"];
                target.updatingTime = collection["updatingTime"];
                target.assignedUserName = collection["assignedUserName"];
                target.respondingUserName = collection["respondingUserName"];
                if (collection["solvedTime"] == null) {
                    if (collection["solved_p"] == null) {
                        target.solvedTime = STR_NOT_INSTANTIATED;
                    } else {
                        target.solvedTime = char14Timestamp();
                    }
                } else {
                    target.solvedTime = collection["solvedTime"];
                }
                if (collection["respondingTime"] == null) {
                    if (collection["responded_p"] == null) {
                        target.respondingTime = STR_NOT_INSTANTIATED;
                    } else {
                        target.respondingTime = char14Timestamp();
                    }
                } else {
                    target.respondingTime = collection["respondingTime"];
                }

                if(newTicket_p)
                    db.Tickets.AddObject(target);

                //now deal with the notes.
                var noteParams = extractRowParams(collection);
                foreach (KeyValuePair<String, Dictionary<String, String>> note in noteParams) {
                    var noteValues = note.Value;
                    TicketNote possibleNote = db.TicketNotes.FirstOrDefault(n => n.sortIndex == Convert.ToInt16(note.Key));
                    if (possibleNote != default(TicketNote)) {
                        possibleNote.content = noteValues["noteContent"];
                    } else {
                        possibleNote = new TicketNote();
                        possibleNote.ticketCode = target.code;
                        possibleNote.sortIndex = Convert.ToInt16(note.Key);
                        possibleNote.type = noteValues["noteType"];
                        possibleNote.enteringUserName = noteValues["noteEnteringUser"];
                        possibleNote.enteringTime = noteValues["noteEnteringTime"];
                        possibleNote.content = noteValues["noteContent"];
                        db.TicketNotes.AddObject(possibleNote);
                    }
                }

                db.SaveChanges();
                return Redirect("/ticket");
            });
        }

        public ActionResult NewRow(string operand)
        {
            //some portions copied right off the generic newRowAction<> because it doesn't obey the same #table assumptions
            //as the CRUD pages.
            return authenticatedAction(new String[] { "UT" }, () => {
                ViewData["Type"] = operand;
                ViewData["User"] = Request.Cookies["Authentication"]["username"];
                ViewData["Time"] = char14Timestamp();

                short nId;
                if (Request.QueryString["rows[]"] != null) {
                    String[] rowParams = extractDnDSerializedParam(Request.QueryString["rows[]"]);
                    nId = getFreshIdVal<short>(Short3Gen, rowParams);
                } else {
                    nId = getFreshIdVal<short>(Short3Gen, new String[0]);
                }
                if (nId == default(short)) {
                    logger.Warn("TicketController.NewRow hasn't been able to find a fresh ID!");
                    return View("RedTelephoneNewRowError");
                } else {
                    logger.DebugFormat("TicketController.NewRow generating a new row with ID {0}", nId.ToString());
                    ViewData["id"] = nId;
                    return View();
                }
            });
        }
    }
}
