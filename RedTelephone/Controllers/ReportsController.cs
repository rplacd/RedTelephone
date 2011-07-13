using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text;
using System.IO;
using RedTelephone.Models;
using RedTelephone.Extensions;

namespace RedTelephone.Extensions {
    public static partial class Extensions {
        private static Dictionary<TextWriter, int> indentLevels = new Dictionary<TextWriter, int>();
        public static void indent(this TextWriter subject)
        {
            int dictValue;
            indentLevels.TryGetValue(subject, out dictValue);
            indentLevels[subject] = ++dictValue;
        }
        public static void dedent(this TextWriter subject)
        {
            int dictValue;
            indentLevels.TryGetValue(subject, out dictValue);
            indentLevels[subject] = --dictValue;
        }
        //REFACTOR: fixing up < 0 indent levels here is crude but workable.
        public static int getIndentLevel(this TextWriter subject)
        {
            int rawVal;
            indentLevels.TryGetValue(subject, out rawVal);
            if (rawVal < 0)
                return 0;
            else
                return rawVal;
        }

        //print a row in our repor'.
        public static void printRow(this TextWriter subject, params object[] contents)
        {
            for (int _ = 0; _ < subject.getIndentLevel(); ++_) {
                subject.Write("\t");
            }
            
            var idx = 0;
            foreach (object e in contents) {
                idx++;
                subject.Write(e.ToString());
                if (idx != contents.Count()) {
                    subject.Write("\t");
                }
            }
            subject.Write("\n");
        }

        public class TicketCount {
            public Int64 createdTickets = 0;
            public Int64 outstandingTickets = 0;
            public Int64 resolvedTickets = 0;
        }

        public class TicketCategories {
            public IEnumerable<Ticket> createdTickets;
            public IEnumerable<Ticket> outstandingTickets;
            public IEnumerable<Ticket> resolvedTickets;
        }

        //X "" "" x.where(ticketPredicate) y.where(ticketPredicate) z.where(ticketPredicate)
        //I don't expect ticketPredicate to change.
        public static void printEnumeration<Category>(this TextWriter writer, TicketCategories tickets, IEnumerable<Category> subjects, Func<Category, String> descriptor, Func<Category, Func<Ticket, Boolean>> makePredicate)
        {
            var count = new TicketCount();
            foreach (var subject in subjects) {
                var pred = makePredicate(subject);
                var ct = tickets.createdTickets.Where(pred).Count();
                var ot = tickets.outstandingTickets.Where(pred).Count();
                var rt = tickets.resolvedTickets.Where(pred).Count();
                writer.printRow(descriptor(subject), "", "", ct, ot, rt);
                count.createdTickets += ct;
                count.outstandingTickets += ot;
                count.resolvedTickets += rt;
            }
            writer.printTicketCount(count);
        }

        public static void printTicketCount(this TextWriter writer, TicketCount count)
        {
            writer.printRow("-");
            writer.printRow("Totals", "", "", count.createdTickets, count.outstandingTickets, count.resolvedTickets);
        }
    }
}

namespace RedTelephone.Controllers
{
    public class ReportsController : RedTelephoneController
    {
        ModelsDataContext db = new ModelsDataContext();

        public ActionResult Index()
        {
            return authenticatedAction(new String[] { "VR" }, () => {
                logger.Debug("ReportsController.Index accessed.");
                return View();
            });
        }

        
        [HttpPost]
        public ActionResult Index(FormCollection collection)
        {
            return authenticatedAction(new String[] { "VR" }, () => {
                logger.DebugFormat("ReportsController.Index generating a report from {0} to {1}", collection["from"], collection["to"]);
                validationLogPrefix = "ReportsController.Index";
                StringWriter output = new StringWriter();

                //set up the date
                Func<String, DateTime> parse = (str) => {
                    String[] dateMonthYear = str.Split(new char[] { '/' });
                    ValidateAssertion(dateMonthYear.Count() == 3, "A date parameter was formatted incorrectly ({0}) - this is a programmer's issue.", str);
                    try {
                        Int32 day = Convert.ToInt32(dateMonthYear[0]);
                        Int32 month = Convert.ToInt32(dateMonthYear[1]);
                        Int32 year = Convert.ToInt32(dateMonthYear[2]);
                        return new DateTime(year, month, day);
                    } catch (FormatException e) {
                        ValidateAssertion(false, "A date parameter was formatted incorrectly ({0}) - this is a programmer's issue.", str);
                        return new DateTime(1, 1, 1); //this will never be reached.
                    }
                };
                var startDate = parse(collection["from"]);
                var endDate = parse(collection["to"]);
                //jquery helps keep the startDate < endDate constraint, so no worries.


                //slimming the whole table of tickets down into the three categories we really need:
                //tickets created within the time-period, tickets solved within the time-period, tickets solved after the time-period.
                IEnumerable<Ticket> createdTickets = db.Tickets.ToList()
                    .Where(t => { var et = parseChar14Timestamp(t.enteringTime); return et > startDate && et < endDate; });
                IEnumerable<Ticket> outstandingTickets = db.Tickets.ToList()
                    .Where(t => t.respondingTime != "              ")
                    .Where(t => {
                        if (t.respondingTime == "") {
                            return true;
                        } else {
                            var et = parseChar14Timestamp(t.enteringTime);
                            var rt = parseChar14Timestamp(t.respondingTime);
                            return et > startDate && rt > endDate;
                        }
                    });
                IEnumerable<Ticket> resolvedTickets = db.Tickets.ToList()
                    .Where(t => t.respondingTime != "              ")
                    .Where(t => {
                        if (t.respondingTime == "") {
                            return true;
                        } else {
                            var et = parseChar14Timestamp(t.enteringTime);
                            var rt = parseChar14Timestamp(t.respondingTime);
                            return et > startDate && rt < endDate;
                        }
                    });
                var tktCats = new Extensions.Extensions.TicketCategories {
                    createdTickets = createdTickets,
                    outstandingTickets = outstandingTickets,
                    resolvedTickets = resolvedTickets
                };

                //now print the damn report. 6 columns wide.
                output.printRow("Statistics", "", "From", startDate.ToString(), "To", startDate.ToString());
                output.printRow("Company", "Contract", "", "Tickets created", "Tickets outstanding", "Tickets responded to");

                //contract-company, unlike the rest of the categories, requires us to keep track of both contract and company
                //when printing. so we haven't abstracted this out.
                {
                    Extensions.Extensions.TicketCount count = new Extensions.Extensions.TicketCount();
                    foreach (Contract con in db.Contracts.OrderBy(c => c.code)) {
                        output.printRow(con.description);
                        output.indent();
                        foreach (Company com in db.Companies.OrderBy(c => c.code)) {
                            output.printRow();
                            output.printRow(com.description);
                            output.indent();

                            var cmpTktCats = new Extensions.Extensions.TicketCategories {
                                createdTickets = createdTickets.Where(t => t.contractCode == con.code && t.companyCode == com.code),
                                outstandingTickets = outstandingTickets.Where(t => t.contractCode == con.code && t.companyCode == com.code),
                                resolvedTickets = resolvedTickets.Where(t => t.contractCode == con.code && t.companyCode == com.code),
                            };

                            output.printRow("Source");
                            output.printEnumeration(cmpTktCats, db.TicketSources, (src) => src.description, (src) => (tkt) => tkt.ticketSourceCode == src.code);

                            output.printRow();
                            output.printRow("Priority");
                            output.printEnumeration(cmpTktCats, db.Priorities, (p) => p.description, (p) => (tkt) => tkt.priorityCode == p.code);

                            output.printRow();
                            output.printRow("Requested response");
                            output.printEnumeration(cmpTktCats, db.RequestedResponses, (r) => r.description, (r) => (tkt) => tkt.requestedResponseCode == r.code);

                            output.printRow();
                            output.printRow("Actual response");
                            output.printEnumeration(cmpTktCats, db.ActualResponses, (r) => r.description, (r) => (tkt) => tkt.actualResponseCode == r.code);


                            output.printRow();
                            output.printRow("Cause");
                            output.printEnumeration(cmpTktCats, db.Causes, (c) => c.description, (c) => (tkt) => tkt.causeCode == c.code);
                            output.dedent();
                        }
                        output.dedent();
                    }
                    //output.printTicketCount(count);
                }



                var ret = new ContentResult { ContentType = "text/plain", Content = output.GetStringBuilder().ToString() };
                output.printRow();

                //now do single-level enumerations and print.

                return ret;
            });
        }

    }
}
