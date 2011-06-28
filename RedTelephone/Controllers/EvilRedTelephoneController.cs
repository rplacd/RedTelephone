using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RedTelephone.EvilLinq;

//LINQ automatically defines methods that take Expression trees - so we're unable to use vars declared "dynamic".
//Nor can we use <parameterized> vars with extension methods.
//Let's change that for a bit. (Stolen from http://jrwren.wrenfam.com/blog/2010/03/04/linq-abuse-with-the-c-4-dynamic-type/)
//Compatible with Lists.
namespace RedTelephone.EvilLinq {
    public static class EvilLinq {
        public static IEnumerable<dynamic> Select(this object src, Func<dynamic, dynamic> map)
        {
            foreach (dynamic item in (dynamic)src) {
                yield return map(item);
            }
        }
        public static IEnumerable<dynamic> Where(this object src, Func<dynamic, dynamic> pred)
        {
            foreach (dynamic item in (dynamic)src) {
                if (pred(item))
                    yield return item;
            }
        }
        //can you keep track of all the types? I f**king can't - no thanks for forcing me to cast to (dynamic, dynamic -> dynamic).
        public static IEnumerable<dynamic> OrderByDescending(this object src, Func<dynamic, dynamic> accessor)
        {
            ((dynamic)src).Sort((Comparison<dynamic>)((x, y) => (accessor(x).CompareTo(accessor(y)))));
            ((dynamic)src).Reverse();
            return (dynamic)src;
        }
    }
}

//Defines any generic methods dependent on LINQ.
namespace RedTelephone.Controllers {
    public abstract partial class RedTelephoneController : Controller {
        protected enum TargetRelationship { GreaterThan, LesserThan };
        protected Model getSwap<Model>(System.Data.Linq.Table<Model> table, dynamic toSwap, TargetRelationship tgtrel) where Model : class
        {
            Model eligibleSwap = default(Model);
            if (toSwap != null) {
                //order by ascending here
                IEnumerable<dynamic> eligibleSwaps = null;
                if (tgtrel == TargetRelationship.GreaterThan)
                    eligibleSwaps = ((object)table).Where(p => p.sortIndex > toSwap.sortIndex).OrderBy(p => p.sortIndex);
                else
                    eligibleSwaps = ((object)table).Where(p => p.sortIndex < toSwap.sortIndex).OrderByDescending(p => p.sortIndex);

                if (showHidden_p()) {
                    eligibleSwap = eligibleSwaps.FirstOrDefault();
                }
                else {
                    eligibleSwap = eligibleSwaps.Where(p => p.active_p == "A").FirstOrDefault();
                }
            }
            return eligibleSwap;
        }
        protected void swapSortIndices<Model>(System.Data.Linq.Table<Model> table, Func<Model, bool> pred, TargetRelationship tgtrel) where Model : class
        {
            dynamic toSwap = table.FirstOrDefault(pred);
            dynamic eligibleSwap = getSwap<Model>(table, toSwap, tgtrel);
            if (eligibleSwap != null) {
                var intermediate = toSwap.sortIndex;
                toSwap.sortIndex = eligibleSwap.sortIndex;
                eligibleSwap.sortIndex = intermediate;
                table.Context.SubmitChanges();
                logger.DebugFormat("RedTelephoneController.swapSortIndices swapping {0} {1} with {2}, with a relationship of {3}",
                    table.ToString(), toSwap.ToString(), eligibleSwap.ToString(), tgtrel.ToString());
            } else {
                logger.ErrorFormat("RedTelephoneController.swapSortIndices failed to swap {0} {1} with {2}, with a relationship of {3}",
                    table.ToString(), toSwap.ToString(), eligibleSwap.ToString(), tgtrel.ToString());
            }
        }
        protected ActionResult incSortIndexAction<Model>(String[] perms, System.Data.Linq.Table<Model> table, Func<Model, bool> pred) where Model : class
        {
            return authenticatedAction(perms, () => sideEffectingAction(() => {
                swapSortIndices<Model>(table, pred, TargetRelationship.GreaterThan);
            }));
        }
        protected ActionResult decSortIndexAction<Model>(String[] perms, System.Data.Linq.Table<Model> table, Func<Model, bool> pred) where Model : class
        {
            return authenticatedAction(perms, () => sideEffectingAction(() => {
                swapSortIndices<Model>(table, pred, TargetRelationship.LesserThan);
            }));
        }
        protected short greatestSortIndex<Model>(System.Data.Linq.Table<Model> table) where Model : class
        {
            var allModels = table.ToList();
            if (allModels.Count() < 1) {
                return 0;
            } else {
                return ((object)allModels).OrderByDescending(x => x.sortIndex).Select(x => x.sortIndex).FirstOrDefault();
            }
        }

    }
}
