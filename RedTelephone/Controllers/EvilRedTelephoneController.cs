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
        public static dynamic FirstOrDefault(this object src, Func<dynamic, dynamic> pred)
        {
            IEnumerable<dynamic> results = src.Where(pred);
            if (results.Count() < 1)
                return default(dynamic); //null, of course
            else
                return results.First();
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
        //generic methods for swapping sort IDs.
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

                eligibleSwap = eligibleSwaps.FirstOrDefault();
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
                logger.ErrorFormat("RedTelephoneController.swapSortIndices failed to swap {0} {1} with a relationship of {3}",
                    table.ToString(), pred.ToString(), tgtrel.ToString());
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

        //generic methods for getting fresh primary keys.
        protected Func<String> Str1Gen = () => (Convert.ToChar((new Random()).Next(97, 122))).ToString();
        protected T getFreshIdVal<T, Model>(System.Data.Linq.Table<Model> table, Func<T> generator, Func<Model, T> accessor) 
            where T : class 
            where Model : class
        {
            T id = generator();
            while (((object)table).FirstOrDefault(obj => accessor(obj) == id) != null) {
                id = generator();
            }
            return id;
        }
        protected ActionResult getFreshId<T, Model>(System.Data.Linq.Table<Model> table, Func<T> generator, Func<Model, T> accessor)
            where T : class
            where Model : class
        {
            ContentResult result = new ContentResult();
            result.ContentType = "text/plain";
            result.Content = getFreshIdVal<T, Model>(table, generator, accessor).ToString();
            return result;
        }
    }
}
